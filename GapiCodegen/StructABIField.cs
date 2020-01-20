using System.IO;
using System.Xml;
using GapiCodegen.Generatables;
using GapiCodegen.Interfaces;
using GapiCodegen.Util;

namespace GapiCodegen
{
    public class StructAbiField : StructField
    {
        protected new ClassBase container_type;
        public string parent_structure_name;
        public string abi_info_name;

        public StructAbiField(XmlElement element, ClassBase container_type,
                string info_name) : base(element, container_type)
        {
            this.container_type = container_type;
            getOffsetName = null;
            abi_info_name = info_name;
        }

        public override string CName
        {

            get
            {
                if (parent_structure_name != null)
                    return parent_structure_name + '.' + Element.GetAttribute("cname");
                return Element.GetAttribute("cname");
            }
        }

        // All field are visible and private
        // as the goal is to respect the ABI
        protected override string Access
        {
            get
            {
                return "private";
            }
        }

        public override bool Hidden
        {
            get
            {
                return false;
            }
        }

        public override bool Validate(LogWriter log)
        {
            string cstype = SymbolTable.Table.GetCsType(CType, true);

            if (Element.GetAttributeAsBoolean("is_callback"))
                return true;

            if (cstype == null || cstype == "")
            {
                log.Warn(" field \"" + CName + "\" has no cstype, can't generate ABI field.");
                return false;
            }

            if (!base.Validate(log))
                return false;

            return true;
        }

        public void SetGetOffseName()
        {
            getOffsetName = "Get" + CName + "Offset";
        }

        public override string GenerateGetSizeOf(string indent)
        {
            return base.GenerateGetSizeOf(indent) + " // " + CName;
        }

        public virtual StructAbiField Generate(GenerationInfo gen_info, string indent,
                StructAbiField prev_field, StructAbiField next_field, string parent_name,
                TextWriter structw)
        {
            StreamWriter sw = gen_info.Writer;
            IGeneratable gen = SymbolTable.Table[CType];

            sw.WriteLine("{0}\tnew GLib.AbiField(\"{1}\"", indent, CName);

            indent = indent + "\t\t";
            if (prev_field != null)
            {
                sw.WriteLine(indent + ", -1");
            }
            else
            {
                if (parent_name != "")
                    sw.WriteLine(indent + ", " + parent_name + "." + abi_info_name + ".Fields");
                else
                    sw.WriteLine(indent + ", 0");
            }

            sw.WriteLine(indent + ", " + GenerateGetSizeOf(""));

            var prev_field_name = prev_field != null ? "\"" + prev_field.CName + "\"" : "null";
            sw.WriteLine(indent + ", " + prev_field_name);

            var container_name = container_type.CName.Replace(".", "_");
            var sanitized_name = CName.Replace(".", "_");
            var alig_struct_name = container_name + "_" + sanitized_name + "Align";
            var next_field_name = next_field != null ? "\"" + next_field.CName + "\"" : "null";
            sw.WriteLine(indent + ", " + next_field_name);

            if (structw != null)
            {
                string min_align = gen != null ? gen.GenerateAlign() : null;

                // Do not generate structs if the type is a simple pointer.
                if (IsCPointer())
                    min_align = "(uint) Marshal.SizeOf(typeof(IntPtr))";

                if (IsBitfield)
                    min_align = "1";

                if (min_align == null)
                {
                    var tmpindent = "\t\t";
                    structw.WriteLine(tmpindent + "[StructLayout(LayoutKind.Sequential)]");
                    structw.WriteLine(tmpindent + "public struct " + alig_struct_name);
                    structw.WriteLine(tmpindent + "{");
                    structw.WriteLine(tmpindent + "\tsbyte f1;");
                    base.Generate(gen_info, tmpindent + "\t", true, structw);
                    structw.WriteLine(tmpindent + "}");
                    structw.WriteLine();

                    var fieldname = SymbolTable.Table.MangleName(CName).Replace(".", "_");
                    if (IsArray && IsNullTermArray)
                        fieldname += "Ptr";
                    sw.WriteLine(indent + ", (long) Marshal.OffsetOf(typeof(" + alig_struct_name + "), \"" + fieldname + "\")");
                }
                else
                {
                    sw.WriteLine(indent + ", " + min_align);
                }
            }

            gen_info.Writer = sw;

            uint bits = 0;
            var bitsstr = Element.GetAttribute("bits");
            if (bitsstr != null && bitsstr != "")
                bits = (uint)int.Parse(bitsstr);

            sw.WriteLine(indent + ", " + bits);
            sw.WriteLine(indent + "),");

            return this;
        }
    }
}
