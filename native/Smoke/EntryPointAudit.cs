using System.Reflection.Metadata;
using System.Reflection.Metadata.Ecma335;
using System.Reflection.PortableExecutable;

static class EntryPointAudit
{
    // Inspect the published CLR entry point, without loading or activating the app.
    public static void Verify(string path)
    {
        using var stream = File.OpenRead(path);
        using var pe = new PEReader(stream);
        var metadata = pe.GetMetadataReader();
        int token = pe.PEHeaders.CorHeader?.EntryPointTokenOrRelativeVirtualAddress ?? 0;
        var handle = MetadataTokens.EntityHandle(token);
        if (handle.Kind != HandleKind.MethodDefinition) throw new Exception("Missing managed entry point.");
        var method = metadata.GetMethodDefinition((MethodDefinitionHandle)handle);
        var attributes = new List<string>();
        foreach (var item in method.GetCustomAttributes()) {
            var attribute = metadata.GetCustomAttribute(item);
            if (attribute.Constructor.Kind != HandleKind.MemberReference) continue;
            var parent = metadata.GetMemberReference((MemberReferenceHandle)attribute.Constructor).Parent;
            if (parent.Kind != HandleKind.TypeReference) continue;
            var type = metadata.GetTypeReference((TypeReferenceHandle)parent);
            attributes.Add(metadata.GetString(type.Namespace) + "." + metadata.GetString(type.Name));
        }
        if (!attributes.Contains("System.MTAThreadAttribute") || attributes.Contains("System.STAThreadAttribute"))
            throw new Exception("UWP bootstrap requires an MTA entry point; STA causes Application.Start failure 0x8001010E.");
        Console.WriteLine("PASS: published UWP entry point has MTA and no STA attribute.");
        StartupOrderAudit.Verify(pe, metadata);
        foreach(var item in metadata.MemberReferences) {
            var member=metadata.GetMemberReference(item);
            if(member.Parent.Kind!=HandleKind.TypeReference)continue;
            var type=metadata.GetTypeReference((TypeReferenceHandle)member.Parent);
            if(metadata.GetString(type.Name)=="CompositionTarget" && metadata.GetString(member.Name)=="add_Rendering")
                throw new Exception("Shared CompositionTarget.Rendering subscription can dispatch animation to another view; use the per-view dispatcher timer.");
        }
        Console.WriteLine("PASS: published widget has no shared CompositionTarget.Rendering animation subscription.");
    }
}
