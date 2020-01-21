// GtkSharp.Generation.FieldBase.cs - base class for struct and object
// fields
//
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

using System.Xml;
using GapiCodegen.Generatables;
using GapiCodegen.Interfaces;
using GapiCodegen.Utils;

namespace GapiCodegen
{
    /// <summary>
    /// Base class for struct and object.
    /// </summary>
    public abstract class FieldBase : PropertyBase
    {
        public FieldBase AbiField = null;
        private string _getterName, _setterName;
        protected string GetOffsetName, OffsetName;

        protected FieldBase(XmlElement element, ClassBase containerType) : base(element, containerType)
        {
        }

        public virtual bool Validate(LogWriter logWriter)
        {
            logWriter.Member = Name;

            if (Ignored || Hidden || CsType != "") return true;

            if (Name == "Priv")
                return false;

            logWriter.Warn($"Field has unknown type: {CType}.");
            Statistics.ThrottledCount++;

            return false;
        }

        internal virtual bool Readable
        {
            get
            {
                if (Parser.GetVersion(Element.OwnerDocument.DocumentElement) <= 2)
                {
                    return Element.GetAttribute("readable") != "false";
                }

                return Element.HasAttribute("readable") && Element.GetAttributeAsBoolean("readable");
            }
        }

        internal virtual bool Writable
        {
            get
            {
                if (Parser.GetVersion(Element.OwnerDocument.DocumentElement) <= 2)
                {
                    return Element.GetAttribute("writeable") != "false";
                }

                return Element.HasAttribute("writeable") && Element.GetAttributeAsBoolean("writeable");
            }
        }

        protected abstract string DefaultAccess { get; }

        protected virtual string Access => Element.HasAttribute("access")
            ? Element.GetAttribute("access")
            : DefaultAccess;

        public bool IsArray => Element.HasAttribute("array_len") || Element.GetAttributeAsBoolean("array");

        public bool IsBitfield => Element.HasAttribute("bits");

        public bool Ignored
        {
            get
            {
                if (ContainerType.GetProperty(Name) != null)
                    return true;

                if (IsArray)
                    return true;

                return Access == "private" && Getter == null && Setter == null;
            }
        }

        private bool UseAbiStruct(GenerationInfo generationInfo)
        {
            if (!ContainerType.CanGenerateABIStruct(new LogWriter(ContainerType.CName)))
                return false;

            return AbiField?.GetOffsetName != null && generationInfo.GlueWriter == null;
        }

        private void CheckGlue(GenerationInfo generationInfo)
        {
            _getterName = _setterName = GetOffsetName = null;

            if (Access != "public")
                return;

            if (UseAbiStruct(generationInfo))
            {
                GetOffsetName = AbiField.GetOffsetName;
                OffsetName = ((StructAbiField)AbiField).abi_info_name + ".GetFieldOffset(\"" +
                             ((StructField)AbiField).CName + "\")";

                return;
            }

            if (generationInfo.GlueWriter == null)
                return;

            var prefix = (ContainerType.Namespace + "Sharp_" + ContainerType.Namespace + "_" + ContainerType.Name)
                .Replace(".", "__").ToLower();

            if (IsBitfield)
            {
                if (Readable && Getter == null)
                    _getterName = $"{prefix}_get_{CName}";

                if (Writable && Setter == null)
                    _setterName = $"{prefix}_set_{CName}";
            }
            else
            {
                if ((!Readable || Getter != null) && (!Writable || Setter != null)) return;

                OffsetName = $"{CName}_offset";
                GetOffsetName = $"{prefix}_get_{OffsetName}";
            }
        }

        protected override void GenerateImports(GenerationInfo generationInfo, string indent)
        {
            if (generationInfo.GlueWriter == null)
            {
                base.GenerateImports(generationInfo, indent);
                return;
            }

            var streamWriter = generationInfo.Writer;
            var table = SymbolTable.Table;

            if (_getterName != null)
            {
                streamWriter.WriteLine(indent + "[DllImport (\"{0}\")]", generationInfo.GlueLibName);
                streamWriter.WriteLine(indent + "extern static {0} {1} ({2} raw);",
                    table.GetMarshalType(CType), _getterName,
                    ContainerType.MarshalType);
            }

            if (_setterName != null)
            {
                streamWriter.WriteLine(indent + "[DllImport (\"{0}\")]", generationInfo.GlueLibName);
                streamWriter.WriteLine(indent + "extern static void {0} ({1} raw, {2} value);",
                    _setterName, ContainerType.MarshalType, table.GetMarshalType(CType));
            }

            if (GetOffsetName != null)
            {
                streamWriter.WriteLine(indent + "[DllImport (\"{0}\")]", generationInfo.GlueLibName);
                streamWriter.WriteLine(indent + "extern static uint {0} ();", GetOffsetName);
                streamWriter.WriteLine();
                streamWriter.WriteLine($"{indent}static uint {OffsetName} = {GetOffsetName} ();");
            }

            base.GenerateImports(generationInfo, indent);
        }

        public virtual void Generate(GenerationInfo generationInfo, string indent)
        {
            if (Ignored || Hidden)
                return;

            CheckGlue(generationInfo);

            GenerateImports(generationInfo, indent);

            if (Getter == null && _getterName == null && OffsetName == null &&
                Setter == null && _setterName == null)
            {
                return;
            }

            var streamWriter = generationInfo.Writer;
            var modifiers = Element.GetAttributeAsBoolean("new_flag") ? "new " : "";

            streamWriter.WriteLine($"{indent}public {modifiers}{CsType} {Name} {{");

            var table = SymbolTable.Table;
            var generatable = table[CType];

            if (Getter != null)
            {
                streamWriter.Write($"{indent}\tget ");
                Getter.GenerateBody(generationInfo, ContainerType, "\t");
                streamWriter.WriteLine("");
            }
            else if (_getterName != null)
            {
                streamWriter.WriteLine($"{indent}\tget {{");
                ContainerType.Prepare(streamWriter, $"{indent}\t\t");

                streamWriter.WriteLine(indent + "\t\t" + CsType + " result = " +
                             table.FromNative(CType, _getterName + " (" + ContainerType.CallByName() + ")") + ";");

                ContainerType.Finish(streamWriter, $"{indent}\t\t");
                streamWriter.WriteLine($"{indent}\t\treturn result;");
                streamWriter.WriteLine($"{indent}\t}}");
            }
            else if (Readable && OffsetName != null)
            {
                streamWriter.WriteLine($"{indent}\tget {{");
                streamWriter.WriteLine($"{indent}\t\tunsafe {{");

                if (generatable is CallbackGen)
                {
                    streamWriter.WriteLine(indent + "\t\t\tIntPtr* raw_ptr = (IntPtr*)(((byte*)" + ContainerType.CallByName() +
                                 ") + " + OffsetName + ");");

                    streamWriter.WriteLine(
                        indent + "\t\t\t {0} del = ({0})Marshal.GetDelegateForFunctionPointer(*raw_ptr, typeof({0}));",
                        table.GetMarshalType(CType));

                    streamWriter.WriteLine($"{indent}\t\t\treturn {table.FromNative(CType, "(del)")};");
                }
                else
                {
                    streamWriter.WriteLine(indent + "\t\t\t" + table.GetMarshalType(CType) + "* raw_ptr = (" +
                                 table.GetMarshalType(CType) + "*)(((byte*)" + ContainerType.CallByName() + ") + " +
                                 OffsetName + ");");

                    streamWriter.WriteLine($"{indent}\t\t\treturn {table.FromNative(CType, "(*raw_ptr)")};");
                }

                streamWriter.WriteLine($"{indent}\t\t}}");
                streamWriter.WriteLine($"{indent}\t}}");
            }

            var toNative = generatable is IManualMarshaler marshaler
                ? marshaler.AllocNative("value")
                : generatable.CallByName("value");

            if (Setter != null)
            {
                streamWriter.Write($"{indent}\tset ");
                Setter.GenerateBody(generationInfo, ContainerType, "\t");
                streamWriter.WriteLine("");
            }
            else if (_setterName != null)
            {
                streamWriter.WriteLine($"{indent}\tset {{");
                ContainerType.Prepare(streamWriter, $"{indent}\t\t");

                streamWriter.WriteLine(
                    $"{indent}\t\t{_setterName} ({ContainerType.CallByName()}, {toNative});");

                ContainerType.Finish(streamWriter, $"{indent}\t\t");
                streamWriter.WriteLine($"{indent}\t}}");
            }
            else if (Writable && OffsetName != null)
            {
                streamWriter.WriteLine($"{indent}\tset {{");
                streamWriter.WriteLine($"{indent}\t\tunsafe {{");

                if (generatable is CallbackGen callbackGen)
                {
                    streamWriter.WriteLine(indent + "\t\t\t{0} wrapper = new {0} (value);", callbackGen.WrapperName);
                    streamWriter.WriteLine(
                        $"{indent}\t\t\tIntPtr* raw_ptr = (IntPtr*)(((byte*){ContainerType.CallByName()}) + {OffsetName});");
                    streamWriter.WriteLine(
                        $"{indent}\t\t\t*raw_ptr = Marshal.GetFunctionPointerForDelegate (wrapper.NativeDelegate);");
                }
                else
                {
                    streamWriter.WriteLine(indent + "\t\t\t" + table.GetMarshalType(CType) + "* raw_ptr = (" +
                                 table.GetMarshalType(CType) + "*)(((byte*)" + ContainerType.CallByName() + ") + " +
                                 OffsetName + ");");
                    streamWriter.WriteLine($"{indent}\t\t\t*raw_ptr = {toNative};");
                }

                streamWriter.WriteLine($"{indent}\t\t}}");
                streamWriter.WriteLine($"{indent}\t}}");
            }

            streamWriter.WriteLine($"{indent}}}");
            streamWriter.WriteLine("");

            if ((_getterName != null || _setterName != null || GetOffsetName != null) && generationInfo.GlueWriter != null)
                GenerateGlue(generationInfo);
        }

        protected void GenerateGlue(GenerationInfo generationInfo)
        {
            var table = SymbolTable.Table;

            var fieldCType = CType.Replace("-", " ");
            var byRef = table[CType] is ByRefGen || table[CType] is StructGen;
            var glueCType = byRef ? $"{fieldCType} *" : fieldCType;
            var containerCType = ContainerType.CName;
            var containerCName = ContainerType.Name.ToLower();

            var streamWriter = generationInfo.GlueWriter;

            if (_getterName != null)
            {
                streamWriter.WriteLine("{0} {1} ({2} *{3});",
                    glueCType, _getterName, containerCType, containerCName);
            }

            if (_setterName != null)
            {
                streamWriter.WriteLine("void {0} ({1} *{2}, {3} value);",
                    _setterName, containerCType, containerCName, glueCType);
            }

            if (GetOffsetName != null)
                streamWriter.WriteLine("guint {0} (void);", GetOffsetName);

            streamWriter.WriteLine("");

            if (_getterName != null)
            {
                streamWriter.WriteLine(glueCType);
                streamWriter.WriteLine("{0} ({1} *{2})", _getterName, containerCType, containerCName);
                streamWriter.WriteLine("{");
                streamWriter.WriteLine("\treturn ({0}){1}{2}->{3};", glueCType,
                    byRef ? "&" : "", containerCName, CName);
                streamWriter.WriteLine("}");
                streamWriter.WriteLine("");
            }

            if (_setterName != null)
            {
                streamWriter.WriteLine("void");
                streamWriter.WriteLine("{0} ({1} *{2}, {3} value)",
                    _setterName, containerCType, containerCName, glueCType);
                streamWriter.WriteLine("{");
                streamWriter.WriteLine("\t{0}->{1} = ({2}){3}value;", containerCName, CName,
                    fieldCType, byRef ? "*" : "");
                streamWriter.WriteLine("}");
                streamWriter.WriteLine("");
            }

            if (GetOffsetName == null) return;

            streamWriter.WriteLine("guint");
            streamWriter.WriteLine("{0} (void)", GetOffsetName);
            streamWriter.WriteLine("{");
            streamWriter.WriteLine("\treturn (guint)G_STRUCT_OFFSET ({0}, {1});",
                containerCType, CName);
            streamWriter.WriteLine("}");
            streamWriter.WriteLine("");
        }
    }
}
