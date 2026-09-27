using System.Reflection;
using System.Reflection.Metadata;
using System.Reflection.PortableExecutable;
using System.Xml.Linq;

static class GameBarPackaging
{
    const string PublicName = "Microsoft.Gaming.XboxGameBar.winmd";
    const string PrivateName = "Microsoft.Gaming.XboxGameBar.Private.winmd";
    const string Dll = "Microsoft.Gaming.XboxGameBar.dll";
    const string Mbm = "00000355-0000-0000-C000-000000000046";
    static readonly XNamespace Ns = "http://schemas.microsoft.com/appx/manifest/foundation/windows10";
    sealed record Contract(string Name, Guid? Id, bool Class);
    static IEnumerable<Contract> Read(string path)
    {
        using var stream = File.OpenRead(path);
        using var pe = new PEReader(stream);
        var reader = pe.GetMetadataReader(MetadataReaderOptions.None);
        foreach (var handle in reader.TypeDefinitions) {
            var type = reader.GetTypeDefinition(handle);
            if ((type.Attributes & TypeAttributes.WindowsRuntime) == 0) continue;
            string name = reader.GetString(type.Namespace) + "." + reader.GetString(type.Name);
            Guid? id = null;
            foreach (var item in type.GetCustomAttributes()) {
                var attribute = reader.GetCustomAttribute(item);
                if (attribute.Constructor.Kind != HandleKind.MemberReference) continue;
                var parent = reader.GetMemberReference((MemberReferenceHandle)attribute.Constructor).Parent;
                if (parent.Kind != HandleKind.TypeReference) continue;
                var attributeType = reader.GetTypeReference((TypeReferenceHandle)parent);
                if (reader.GetString(attributeType.Namespace) != "Windows.Foundation.Metadata" || reader.GetString(attributeType.Name) != "GuidAttribute") continue;
                var bytes = reader.GetBlobBytes(attribute.Value);
                if (bytes.Length < 20 || bytes[0] != 1 || bytes[1] != 0) throw new Exception("Malformed GUID metadata: " + name);
                id = new Guid(bytes.AsSpan(2, 16));
            }
            bool runtimeClass = false;
            if ((type.Attributes & TypeAttributes.Interface) == 0 && type.BaseType.Kind == HandleKind.TypeReference) {
                var parent = reader.GetTypeReference((TypeReferenceHandle)type.BaseType);
                runtimeClass = reader.GetString(parent.Namespace) == "System" && reader.GetString(parent.Name) == "Object";
            }
            yield return new(name, id, runtimeClass);
        }
    }
    static string Source(string sdk, string name) => Path.Combine(sdk, name == PublicName ? "lib/uap10.0" : "private", name);
    static List<Contract> Contracts(string sdk) => Read(Source(sdk, PublicName)).Concat(Read(Source(sdk, PrivateName))).ToList();
    public static void Prepare(string package, string sdk)
    {
        foreach (var name in new[] { PublicName, PrivateName }) File.Copy(Source(sdk, name), Path.Combine(package, name), true);
        var contracts = Contracts(sdk);
        var manifest = XDocument.Load(Path.Combine(package, "AppxManifest.xml"));
        var extensions = manifest.Root!.Element(Ns + "Extensions")!;
        foreach (var extension in extensions.Elements().Where(e => e.Descendants(Ns + "Path").Any(p => p.Value == Dll || p.Value == PublicName)).ToList()) extension.Remove();
        var classes = contracts.Where(c => c.Class && !c.Name.Contains(".Private.")).ToList();
        extensions.Add(new XElement(Ns + "Extension", new XAttribute("Category", "windows.activatableClass.inProcessServer"),
            new XElement(Ns + "InProcessServer", new XElement(Ns + "Path", Dll),
                classes.Select(c => new XElement(Ns + "ActivatableClass", new XAttribute("ActivatableClassId", c.Name), new XAttribute("ThreadingModel", "both"))))));
        var interfaces = contracts.Where(c => c.Id.HasValue).ToList();
        if (interfaces.GroupBy(c => c.Id).Any(g => g.Count() != 1)) throw new Exception("SDK contains duplicate GUIDs; inspect metadata before packaging.");
        extensions.Add(new XElement(Ns + "Extension", new XAttribute("Category", "windows.activatableClass.proxyStub"),
            new XElement(Ns + "ProxyStub", new XAttribute("ClassId", Mbm), new XElement(Ns + "Path", PublicName),
                interfaces.Select(c => new XElement(Ns + "Interface", new XAttribute("Name", c.Name), new XAttribute("InterfaceId", c.Id!.Value.ToString("D").ToUpperInvariant()))))));
        manifest.Save(Path.Combine(package, "AppxManifest.xml"));
        Verify(package, sdk);
    }
    public static void Verify(string package, string sdk)
    {
        foreach (var name in new[] { PublicName, PrivateName }) {
            if (!File.ReadAllBytes(Path.Combine(package, name)).AsSpan().SequenceEqual(File.ReadAllBytes(Source(sdk, name)))) throw new Exception("Missing or altered SDK metadata: " + name);
        }
        var manifest = XDocument.Load(Path.Combine(package, "AppxManifest.xml"));
        var contracts = Contracts(sdk);
        var stubs = manifest.Root!.Element(Ns + "Extensions")!.Elements(Ns + "Extension").Elements(Ns + "ProxyStub").Where(e => (string?)e.Attribute("ClassId") == Mbm);
        var registrations = stubs.SelectMany(e => e.Elements(Ns + "Interface")).ToDictionary(e => (string)e.Attribute("Name")!, e => Guid.Parse((string)e.Attribute("InterfaceId")!));
        foreach (var contract in contracts.Where(c => c.Id.HasValue))
            if (!registrations.TryGetValue(contract.Name, out var value) || value != contract.Id) throw new Exception("Missing or wrong COM registration: " + contract.Name);
        var classes = manifest.Descendants(Ns + "InProcessServer").Where(e => e.Element(Ns + "Path")?.Value == Dll).Elements(Ns + "ActivatableClass").Select(e => (string)e.Attribute("ActivatableClassId")!).ToHashSet();
        foreach (var contract in contracts.Where(c => c.Class && !c.Name.Contains(".Private.")))
            if (!classes.Contains(contract.Name)) throw new Exception("Missing SDK class registration: " + contract.Name);
        Console.WriteLine($"PASS: exact SDK WinMD payload; {registrations.Count} COM interfaces; {classes.Count} runtime classes.");
    }
}
