using System.Collections.Generic;
using System.IO;
using System.Xml;
using GapiCodegen.Generatables;
using GapiCodegen.Util;

namespace GapiCodegen
{
    public class UnionAbiField : StructAbiField
    {
        bool is_valid;
        XmlElement _element;
        protected List<UnionSubstruct> substructs = new List<UnionSubstruct>();

        public UnionAbiField(XmlElement element, ClassBase container_type, string info_name) :
                base(element, container_type, info_name)
        {
            _element = element;
            is_valid = true;
            foreach (XmlElement union_child in element.ChildNodes)
            {
                substructs.Add(new UnionSubstruct(union_child, container_type, abi_info_name));
            }
        }

        public override StructAbiField Generate(GenerationInfo gen_info, string indent,
                StructAbiField prev_field, StructAbiField next_field, string parent_name,
                TextWriter tw)
        {
            StreamWriter sw = gen_info.Writer;
            var name = _element.GetAttribute("name");
            var cname = _element.GetAttribute("cname");

            foreach (UnionSubstruct _struct in substructs)
                _struct.EnsureParentStructName(cname);

            foreach (UnionSubstruct _struct in substructs)
            {
                _struct.Generate(gen_info, indent + "\t", cname, prev_field,
                        next_field, parent_name, tw);
            }

            base.Generate(gen_info, indent, prev_field, next_field, parent_name, null);

            return this;
        }

        public override string GenerateGetSizeOf(string indent)
        {
            string res = indent + "new List<List<string>>() {  // union " + _element.GetAttribute("cname") + "\n";
            bool first = true;

            indent += "\t\t\t";
            foreach (UnionSubstruct _struct in substructs)
            {
                if (!first)
                    res += ",\n";
                first = false;
                res += _struct.GenerateGetSize(indent + "\t\t\t");
            }
            res += "\n" + indent + "\t\t  }";

            return res;
        }

        public override bool Validate(LogWriter log)
        {

            if (!is_valid)
            {
                log.Warn("Can't generate ABI compatible union");
            }
            return is_valid;
        }
    }

    public class UnionSubstruct
    {
        List<StructAbiField> fields;
        XmlElement Elem;
        private bool _isValid;
        bool unique_field;

        public UnionSubstruct(XmlElement elem, ClassBase container_type, string abi_info_name)
        {
            fields = new List<StructAbiField>();
            Elem = elem;
            _isValid = true;
            unique_field = false;

            if (Elem.Name == "struct")
            {
                foreach (XmlElement child_field in elem.ChildNodes)
                {
                    if (child_field.Name != "field")
                    {
                        _isValid = false;
                        continue;
                    }

                    fields.Add(new StructAbiField(child_field, container_type, abi_info_name));
                }
            }
            else if (Elem.Name == "field")
            {
                fields.Add(new StructAbiField(Elem, container_type, abi_info_name));
                unique_field = true;
            }
        }

        public string GenerateGetSize(string indent)
        {
            var size = indent += "new List<string>() {";
            var is_first = true;
            foreach (StructAbiField field in fields)
            {
                if (!is_first)
                    size += ",";
                is_first = false;

                size += "\"" + field.CName + "\"";
            }

            return size + "}";
        }

        public void EnsureParentStructName(string parent_name)
        {
            var name = Elem.GetAttribute("name");

            if (!unique_field)
            {
                parent_name = parent_name + '.' + name;
            }

            foreach (var field in fields)
            {
                field.parent_structure_name = parent_name;
            }
        }

        public StructField Generate(GenerationInfo gen_info, string indent,
                string parent_name, StructAbiField prev_field,
                StructAbiField next, string struct_parent_name,
                TextWriter tw)
        {
            StreamWriter sw = gen_info.Writer;
            var name = Elem.GetAttribute("name");
            var cname = Elem.GetAttribute("cname");

            if (!unique_field)
            {
                parent_name = parent_name + '.' + name;
            }

            StructAbiField next_field = null;
            sw.WriteLine(indent + "// union struct " + parent_name);
            for (int i = 0; i < fields.Count; i++)
            {
                var field = fields[i];
                next_field = fields.Count > i + 1 ? fields[i + 1] : null;

                field.parent_structure_name = parent_name;

                field.Generate(gen_info, indent, prev_field, next_field, struct_parent_name,
                        tw);

                prev_field = field;
            }

            sw.WriteLine(indent + "// End " + parent_name);
            sw.WriteLine();

            return prev_field;
        }
    }
}
