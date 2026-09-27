using System.Reflection;
using System.Reflection.Emit;
using System.Reflection.Metadata;
using System.Reflection.Metadata.Ecma335;
using System.Reflection.PortableExecutable;

static class StartupOrderAudit
{
    public static void Verify(PEReader pe, MetadataReader reader)
    {
        var opcodes = typeof(OpCodes).GetFields(BindingFlags.Public | BindingFlags.Static)
            .Where(f => f.FieldType == typeof(OpCode)).Select(f => (OpCode)f.GetValue(null)!)
            .ToDictionary(o => unchecked((ushort)o.Value));
        bool found = false;
        foreach (var handle in reader.MethodDefinitions) {
            var method = reader.GetMethodDefinition(handle);
            string name = reader.GetString(method.Name);
            if (name != "InitializeApplication" && !name.StartsWith("<Main>b__")) continue;
            found = true;
            var bytes = pe.GetMethodBody(method.RelativeVirtualAddress).GetILBytes()!;
            for (int offset = 0; offset < bytes.Length;) {
                ushort code = bytes[offset++];
                if (code == 0xfe) code = (ushort)(0xfe00 | bytes[offset++]);
                var op = opcodes[code];
                if (op.OperandType == OperandType.InlineMethod) {
                    var target = MetadataTokens.EntityHandle(BitConverter.ToInt32(bytes, offset));
                    string owner = "";
                    if (target.Kind == HandleKind.MethodDefinition) {
                        var definition = reader.GetMethodDefinition((MethodDefinitionHandle)target);
                        var type = reader.GetTypeDefinition(definition.GetDeclaringType());
                        owner = reader.GetString(type.Namespace) + "." + reader.GetString(type.Name);
                    } else if (target.Kind == HandleKind.MemberReference) {
                        var member = reader.GetMemberReference((MemberReferenceHandle)target);
                        if (member.Parent.Kind == HandleKind.TypeReference) {
                            var type = reader.GetTypeReference((TypeReferenceHandle)member.Parent);
                            owner = reader.GetString(type.Namespace) + "." + reader.GetString(type.Name);
                        }
                    }
                    if (owner is "Reticle.Widget.UiThread" or "Windows.UI.Core.CoreWindow" or "Windows.UI.Xaml.Window")
                        throw new Exception("Initialization callback accesses a view before activation: " + owner);
                }
                offset += op.OperandType switch {
                    OperandType.InlineNone => 0,
                    OperandType.ShortInlineBrTarget or OperandType.ShortInlineI or OperandType.ShortInlineVar => 1,
                    OperandType.InlineVar => 2,
                    OperandType.InlineI8 or OperandType.InlineR => 8,
                    OperandType.InlineSwitch => 4 + 4 * BitConverter.ToInt32(bytes, offset),
                    _ => 4
                };
            }
        }
        if (!found) throw new Exception("Cannot locate the initialization callback for startup validation.");
        Console.WriteLine("PASS: compiled initialization callback does not access a window or its dispatcher before activation.");
    }
}
