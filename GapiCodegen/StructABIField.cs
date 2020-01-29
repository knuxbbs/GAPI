using System.IO;
using System.Xml;
using GapiCodegen.Generatables;
using GapiCodegen.Utils;

namespace GapiCodegen
{
    /// <summary>
    /// Handles 'fields' to generate ABI compatible structures.
    /// </summary>
    public class StructAbiField : StructField
    {
        public string ParentStructureName;

        public StructAbiField(XmlElement element, ClassBase containerType,
                string infoName) : base(element, containerType)
        {
            AbiInfoName = infoName;

            GetOffsetName = null;
        }

        public string AbiInfoName;

        public override string CName =>
            ParentStructureName != null
                ? $"{ParentStructureName}{'.'}{Element.GetAttribute(Constants.CName)}"
                : Element.GetAttribute(Constants.CName);

        // All field are visible and private
        // as the goal is to respect the ABI
        protected override string Access => "private";

        public override bool Hidden => false;

        public override bool Validate(LogWriter logWriter)
        {
            var csType = SymbolTable.Table.GetCsType(CType, true);

            if (Element.GetAttributeAsBoolean("is_callback"))
                return true;

            if (!string.IsNullOrEmpty(csType)) return base.Validate(logWriter);

            logWriter.Warn($" field \"{CName}\" has no C# type, can't generate ABI field.");
            return false;
        }

        public void SetGetOffsetName()
        {
            GetOffsetName = $"Get{CName}Offset";
        }

        public override string GenerateGetSizeOf(string indent)
        {
            return $"{base.GenerateGetSizeOf(indent)} // {CName}";
        }

        public virtual StructAbiField Generate(GenerationInfo generationInfo, string indent,
            StructAbiField previousField, StructAbiField nextField, string parentName,
            TextWriter textWriter)
        {
            var streamWriter = generationInfo.Writer;

            streamWriter.WriteLine("{0}\tnew GLib.AbiField(\"{1}\"", indent, CName);

            indent = $"{indent}\t\t";

            if (previousField != null)
            {
                streamWriter.WriteLine($"{indent}, -1");
            }
            else
            {
                streamWriter.WriteLine(parentName != ""
                    ? $"{indent}, {parentName}.{AbiInfoName}.Fields"
                    : $"{indent}, 0");
            }

            streamWriter.WriteLine($"{indent}, {GenerateGetSizeOf(string.Empty)}");

            var previousFieldName = previousField != null ? $"\"{previousField.CName}\"" : "null";
            streamWriter.WriteLine($"{indent}, {previousFieldName}");
            
            var nextFieldName = nextField != null ? $"\"{nextField.CName}\"" : "null";
            streamWriter.WriteLine($"{indent}, {nextFieldName}");

            var generatable = SymbolTable.Table[CType];

            var containerName = ContainerType.CName.Replace(".", "_");
            var sanitizedName = CName.Replace(".", "_");
            var alignStructName = $"{containerName}_{sanitizedName}Align";

            if (textWriter != null)
            {
                var minAlign = generatable?.GenerateAlign();

                // Do not generate structs if the type is a simple pointer.
                if (IsCPointer())
                    minAlign = "(uint) Marshal.SizeOf(typeof(IntPtr))";

                if (IsBitfield)
                    minAlign = "1";

                if (minAlign == null)
                {
                    const string fixedIndent = "\t\t";
                    textWriter.WriteLine($"{fixedIndent}[StructLayout(LayoutKind.Sequential)]");
                    textWriter.WriteLine($"{fixedIndent}public struct {alignStructName}");
                    textWriter.WriteLine($"{fixedIndent}{{");
                    textWriter.WriteLine($"{fixedIndent}\tsbyte f1;");
                    
                    Generate(generationInfo, $"{fixedIndent}\t", true, textWriter);

                    textWriter.WriteLine($"{fixedIndent}}}");
                    textWriter.WriteLine();

                    var fieldname = SymbolTable.Table.MangleName(CName).Replace(".", "_");

                    if (IsArray && IsNullTermArray)
                        fieldname += "Ptr";

                    streamWriter.WriteLine(
                        $"{indent}, (long) Marshal.OffsetOf(typeof({alignStructName}), \"{fieldname}\")");
                }
                else
                {
                    streamWriter.WriteLine($"{indent}, {minAlign}");
                }
            }

            generationInfo.Writer = streamWriter;

            var bitsstr = Element.GetAttribute(Constants.Bits);
            uint bits = 0;
            
            if (!string.IsNullOrEmpty(bitsstr))
                bits = uint.Parse(bitsstr);

            streamWriter.WriteLine($"{indent}, {bits}");
            streamWriter.WriteLine($"{indent}),");

            return this;
        }
    }
}
