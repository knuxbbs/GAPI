// GtkSharp.Generation.StructField.cs - The Structure Field generation
// Class.
//
// Author: Mike Kestner <mkestner@ximian.com>
//
// Copyright (c) 2004-2005 Novell, Inc.
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

using System;
using System.IO;
using System.Xml;
using GapiCodegen.Generatables;
using GapiCodegen.Interfaces;
using GapiCodegen.Utils;

namespace GapiCodegen
{
    /// <summary>
    /// Handles 'field' elements in structs.
    /// </summary>
    public class StructField : FieldBase
    {
        public StructField(XmlElement element, ClassBase containerType) : base(element, containerType) { }

        protected override string DefaultAccess => IsPadding ? "private" : "public";

        public int ArrayLength
        {
            get
            {
                if (!IsArray)
                    return 0;

                int result;

                try
                {
                    result = int.Parse(Element.GetAttribute(Constants.ArrayLen));
                }
                catch (Exception)
                {
                    var logWriter = new LogWriter($"{ContainerType.Name}.{Name}");

                    logWriter.Warn($"Non-numeric array_len: \"{Element.GetAttribute(Constants.ArrayLen)}\" incorrectly generated.");

                    result = 0;
                }

                return result;
            }
        }

        public bool IsNullTermArray => Element.GetAttributeAsBoolean(Constants.NullTermArray);

        public new string CsType
        {
            get
            {
                var type = base.CsType;

                if (IsArray)
                {
                    type += "[]";
                }
                else if ((IsPointer || SymbolTable.Table.IsOpaque(CType)) && type != "string")
                {
                    type = "IntPtr";
                }

                return type;
            }
        }

        internal bool Visible { get; private set; }

        public string EqualityName
        {
            get
            {
                var table = SymbolTable.Table;
                var wrappedName = SymbolTable.Table.MangleName(CName);
                var generatable = table[CType];

                if (IsArray || generatable is IPropertyAccessor)
                    return Access == "public" ? StudlyName : Name;

                if (IsBitfield)
                    return Name;

                if (IsPointer && (generatable is StructGen || generatable is BoxedGen || generatable is UnionGen))
                    return Access != "private" ? wrappedName : Name;

                if (IsPointer && CsType != "string")
                    return Name;

                return Access == "public" ? StudlyName : Name;
            }
        }

        private bool IsFixedSizeArray()
        {
            return IsArray && !IsNullTermArray && ArrayLength != 0;
        }

        public virtual bool IsCPointer()
        {
            var generatable = SymbolTable.Table[CType];

            return CType.EndsWith("*") ||
                   CType.EndsWith("pointer") ||
                   generatable is CallbackGen ||
                   CsType == "string" ||
                   CType == "guint8" && IsArray && IsNullTermArray ||
                   Element.GetAttributeAsBoolean("is_callback");
        }

        public virtual string GenerateGetSizeOf(string indent)
        {
            var generatable = SymbolTable.Table[CType];
            var csType = SymbolTable.Table.GetCsType(CType, true);
            var isPointer = false;
            var size = "";

            if (IsCPointer())
            {
                isPointer = true;
                csType = "IntPtr";
            }
            else if (generatable != null)
            {
                size = generatable.GenerateGetSizeOf();
            }

            if (!string.IsNullOrEmpty(size))
            {
                if (IsFixedSizeArray())
                    size += $" * {ArrayLength}";

                return $"{indent}{size}";
            }

            if (generatable is EnumGen && !isPointer)
            {
                size = $"(uint) Marshal.SizeOf(System.Enum.GetUnderlyingType(typeof({csType})))";
            }
            else
            {
                size = $"(uint) Marshal.SizeOf(typeof({csType}))";
            }

            if (IsFixedSizeArray())
                size += $" * {ArrayLength}";

            return size;
        }

        public bool IsPadding
        {
            get
            {
                if (Element.GetAttributeAsBoolean("is-padding"))
                    return Element.GetAttributeAsBoolean("is-padding");

                return Element.GetAttribute(Constants.Access) == "private" &&
                       (CName.StartsWith("dummy") || CName.StartsWith("padding"));
            }
        }

        public bool IsPointer => IsCPointer();

        public new string Name
        {
            get
            {
                var result = "";

                if ((IsPointer || SymbolTable.Table.IsOpaque(CType)) && CsType != "string")
                    result = "_";

                result += SymbolTable.Table.MangleName(CName);

                return result;
            }
        }

        public virtual string StudlyName
        {
            get
            {
                var studly = base.Name;

                if (studly == "")
                    throw new Exception(string.Format(
                        "{0} API file must be regenerated with a current version of the GAPI parser. It is incompatible with this version of the GAPI code generator.",
                        CName));

                return studly;
            }
        }

        public override void Generate(GenerationInfo generationInfo, string indent)
        {
            Generate(generationInfo, indent, false, generationInfo.Writer);
        }

        public void Generate(GenerationInfo generationInfo, string indent, bool useCNames,
                TextWriter textWriter)
        {
            if (Hidden && !useCNames)
                return;

            Visible = Access != "private";

            var table = SymbolTable.Table;

            var wrapped = table.GetCsType(CType);

            var wrappedName = SymbolTable.Table.MangleName(CName);
            var name = Name;
            var studlyName = StudlyName;
            var csType = CsType;

            var generatable = table[CType];

            if (useCNames)
            {
                name = studlyName = wrappedName = SymbolTable.Table.MangleName(CName).Replace(".", "_");

                if (generatable is ManualGen manualGen)
                {
                    if (manualGen.AbiType != null)
                        csType = manualGen.AbiType;
                }

                if (IsCPointer())
                    csType = "IntPtr";
            }

            if (IsArray && !IsNullTermArray)
            {
                textWriter.WriteLine($"{indent}[MarshalAs (UnmanagedType.ByValArray, SizeConst={ArrayLength})]");
                textWriter.WriteLine(indent + "{0} {1} {2};", Access, csType, studlyName);
            }
            else if (IsArray && IsNullTermArray)
            {
                textWriter.WriteLine(indent + "private {0} {1};", "IntPtr", studlyName + "Ptr");

                if (!Readable && !Writable || Access != "public") return;

                textWriter.WriteLine(indent + "public {0} {1} {{", csType, studlyName);

                if (Readable)
                {
                    textWriter.WriteLine(indent + "\tget {{ return GLib.Marshaller.StructArrayFromNullTerminatedIntPtr<{0}> ({1}); }}",
                        base.CsType, studlyName + "Ptr");
                }
                if (Writable)
                {
                    textWriter.WriteLine(indent + "\tset {{ {0} = GLib.Marshaller.StructArrayToNullTerminatedStructArrayIntPtr<{1}> (value); }}",
                        studlyName + "Ptr", base.CsType);
                }

                textWriter.WriteLine($"{indent}}}");
            }
            else if (IsBitfield)
            {
                base.Generate(generationInfo, indent);
            }
            else if (generatable is IPropertyAccessor)
            {
                textWriter.WriteLine(indent + "private {0} {1};", generatable.MarshalType, name);

                if (Access == "private") return;

                var propertyAccessor = table[CType] as IPropertyAccessor;
                textWriter.WriteLine($"{indent}{Access} {wrapped} {studlyName} {{");
                propertyAccessor?.WriteAccessors(textWriter, $"{indent}\t", name);
                textWriter.WriteLine($"{indent}}}");
            }
            else if (IsPointer && (generatable is StructGen || generatable is BoxedGen || generatable is UnionGen))
            {
                textWriter.WriteLine(indent + "private {0} {1};", csType, name);
                textWriter.WriteLine();

                if (Access == "private") return;

                textWriter.WriteLine($"{indent}{Access} {wrapped} {wrappedName} {{");
                textWriter.WriteLine($"{indent}\tget {{ return {table.FromNative(CType, name)}; }}");
                textWriter.WriteLine($"{indent}}}");
            }
            else if (IsPointer && csType != "string")
            {
                //TODO: FIXME: probably some fields here which should be visible.
                Visible = false;
                textWriter.WriteLine(indent + "private {0} {1};", csType, name);
            }
            else
            {
                textWriter.WriteLine(indent + "{0} {1} {2};", Access, csType, Access == "public" ? studlyName : name);
            }
        }
    }
}
