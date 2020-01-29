// GtkSharp.Generation.Property.cs - The Property Generatable.
//
// Author: Mike Kestner <mkestner@speakeasy.net>
//
// Copyright (c) 2001-2003 Mike Kestner
// Copyright (c) 2004 Novell, Inc.
//
// This program is free software; you can redistribute it and/or
// modify it under the terms of version 2 of the GNU General Public
// License as published by the Free Software Foundation.
//
// This program is distributed in the hope that it will be useful,
// but WITHOUT ANY WARRANTY; without even the implied warranty of
// MERCHANTABILITY or FITNESS FOR A PARTICULAR PURPOSE.  See the GNU
// General Public License for more details.
//
// You should have received a copy of the GNU General Public
// License along with this program; if not, write to the
// Free Software Foundation, Inc., 59 Temple Place - Suite 330,
// Boston, MA 02111-1307, USA.

using System.IO;
using System.Xml;
using GapiCodegen.Generatables;
using GapiCodegen.Utils;

namespace GapiCodegen
{
    /// <summary>
    /// Handles 'property' elements.
    /// </summary>
    public class Property : PropertyBase
    {
        public Property(XmlElement element, ClassBase containerType) : base(element, containerType) { }

        public bool Validate(LogWriter logWriter)
        {
            if (CsType != "" || Hidden) return true;

            logWriter.Member = Name;
            logWriter.Warn($"Property has unknown type '{CType}'.");

            Statistics.ThrottledCount++;
            return false;
        }

        private bool Readable => Element.GetAttributeAsBoolean(Constants.Readable);

        private bool Writeable =>
            Element.GetAttributeAsBoolean(Constants.Writeable) &&
            !Element.GetAttributeAsBoolean(Constants.ConstructOnly);

        private bool IsDeprecated =>
            !ContainerType.IsDeprecated &&
            Element.GetAttributeAsBoolean(Constants.Deprecated);

        protected virtual string PropertyAttribute(string name)
        {
            return $"[GLib.Property({name})]";
        }

        protected virtual string RawGetter(string name)
        {
            return ContainerType is InterfaceGen
                ? $"implementor.GetProperty({name})"
                : $"GetProperty({name})";
        }

        protected virtual string RawSetter(string name)
        {
            return ContainerType is InterfaceGen
                ? $"implementor.SetProperty({name}, val)"
                : $"SetProperty({name}, val)";
        }

        public void GenerateDeclaration(StreamWriter streamWriter, string indent)
        {
            if (Hidden || !Readable && !Writeable)
                return;

            var name = Name;

            if (name == ContainerType.Name)
                name += "Prop";

            streamWriter.WriteLine($"{indent}{CsType} {name} {{");
            streamWriter.Write($"{indent}\t");

            if (Readable || Getter != null)
                streamWriter.Write("get; ");

            if (Writeable || Setter != null)
                streamWriter.Write("set;");

            streamWriter.WriteLine();
            streamWriter.WriteLine($"{indent}}}");
        }

        public void Generate(GenerationInfo generationInfo, string indent, ClassBase implementor)
        {
            if (Hidden || !Readable && !Writeable)
                return;

            var name = Name;

            if (name == ContainerType.Name)
            {
                name += "Prop";
            }

            GenerateImports(generationInfo, indent);

            var streamWriter = generationInfo.Writer;
            var qpname = $"\"{CName}\"";
            var modifiers = "";

            if (IsNew || ContainerType.Parent?.GetPropertyRecursively(Name) != null ||
                implementor?.Parent?.GetPropertyRecursively(Name) != null)
                modifiers = "new ";

            if (IsDeprecated || Getter != null && Getter.IsDeprecated ||
                Setter != null && Setter.IsDeprecated)
            {
                streamWriter.WriteLine($"{indent}[Obsolete]");
            }

            streamWriter.WriteLine("{0}{1}", indent, PropertyAttribute(qpname));
            streamWriter.WriteLine($"{indent}public {modifiers}{CsType} {name} {{");
            indent += "\t";

            var table = SymbolTable.Table;
            var vType = "";

            if (table.IsInterface(CType))
            {
                vType = "(GLib.Object)";
            }
            else if (table.IsOpaque(CType))
            {
                vType = "(GLib.Opaque)";
            }
            else if (table.IsEnum(CType))
            {
                vType = "(Enum)";
            }

            if (Getter != null)
            {
                streamWriter.Write($"{indent}get ");
                Getter.GenerateBody(generationInfo, implementor, "\t");
                streamWriter.WriteLine();
            }
            else if (Readable)
            {
                streamWriter.WriteLine($"{indent}get {{");
                streamWriter.WriteLine($"{indent}\tGLib.Value val = {RawGetter(qpname)};");

                if (table.IsOpaque(CType) || table.IsBoxed(CType))
                {
                    streamWriter.WriteLine($"{indent}\t{CsType} ret = ({CsType})val;");
                }
                else if (table.IsInterface(CType))
                {
                    var interfaceGen = table.GetInterfaceGen(CType);

                    // Do we have to dispose the GLib.Object from the GLib.Value?
                    streamWriter.WriteLine("{2}\t{0} ret = {1}.GetObject((GLib.Object)val);",
                        interfaceGen.QualifiedName, interfaceGen.QualifiedAdapterName, indent);
                }
                else
                {
                    streamWriter.Write($"{indent}\t{CsType} ret = ");
                    streamWriter.Write($"({CsType}) ");

                    if (vType != "")
                    {
                        streamWriter.Write($"{vType} ");
                    }

                    streamWriter.WriteLine("val;");
                }

                streamWriter.WriteLine($"{indent}\tval.Dispose();");
                streamWriter.WriteLine($"{indent}\treturn ret;");
                streamWriter.WriteLine($"{indent}}}");
            }

            if (Setter != null)
            {
                streamWriter.Write($"{indent}set ");
                Setter.GenerateBody(generationInfo, implementor, "\t");
                streamWriter.WriteLine();
            }
            else if (Writeable)
            {
                streamWriter.WriteLine($"{indent}set {{");
                streamWriter.Write($"{indent}\tGLib.Value val = ");

                if (table.IsBoxed(CType))
                {
                    streamWriter.WriteLine("(GLib.Value)value;");
                }
                else if (table.IsOpaque(CType))
                {
                    streamWriter.WriteLine("new GLib.Value(value, \"{0}\");", CType);
                }
                else
                {
                    streamWriter.Write("new GLib.Value(");

                    if (vType != "" && !(table.IsObject(CType) || table.IsInterface(CType) || table.IsOpaque(CType)))
                    {
                        streamWriter.Write($"{vType} ");
                    }

                    streamWriter.WriteLine("value);");
                }

                streamWriter.WriteLine($"{indent}\t{RawSetter(qpname)};");
                streamWriter.WriteLine($"{indent}\tval.Dispose();");
                streamWriter.WriteLine($"{indent}}}");
            }

            streamWriter.WriteLine($"{indent.Substring(1)}}}");
            streamWriter.WriteLine();

            Statistics.PropCount++;
        }
    }
}
