using System.Reflection;
using System.Reflection.Metadata;
using System.Reflection.PortableExecutable;

static class NativeBoundaryAudit
{
    public static void Verify(string folder) {
        var allowed=new HashSet<string>(StringComparer.OrdinalIgnoreCase) {
            "kernel32.dll!GetCurrentPackageFamilyName","kernel32.dll!GetModuleHandleW",
            "user32.dll!RegisterClassW","user32.dll!UnregisterClassW","user32.dll!CreateWindowExW",
            "user32.dll!RegisterRawInputDevices","user32.dll!GetRawInputData","user32.dll!GetMessageW",
            "user32.dll!TranslateMessage","user32.dll!DispatchMessageW","user32.dll!DefWindowProcW",
            "user32.dll!PostMessageW","user32.dll!DestroyWindow","user32.dll!PostQuitMessage"
        };
        int total=0;
        foreach(var relative in new[]{"ReticleWidget.dll","Core.dll","Bridge/ReticleBridge.dll","Bridge/Core.dll"}) {
            using var stream=File.OpenRead(Path.Combine(folder,relative));using var pe=new PEReader(stream);
            var reader=pe.GetMetadataReader();
            foreach(var handle in reader.MethodDefinitions) {
                var method=reader.GetMethodDefinition(handle);
                if((method.Attributes&MethodAttributes.PinvokeImpl)==0)continue;
                var import=method.GetImport();string call=reader.GetString(reader.GetModuleReference(import.Module).Name)+"!"+reader.GetString(import.Name);
                if(relative!="Bridge/ReticleBridge.dll"||!allowed.Contains(call))throw new Exception("Unreviewed native import: "+relative+" "+call);
                Console.WriteLine(relative+": "+call);total++;
            }
        }
        if(total!=allowed.Count)throw new Exception("Native boundary changed; review the import inventory.");
        Console.WriteLine("PASS: first-party compiled native imports match the 14 reviewed package/window/mouse functions. Not a VAC certification.");
    }
}
