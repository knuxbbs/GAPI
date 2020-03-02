// GtkSharp.Generation.StructBase.cs - The Structure/Boxed Base Class.
//
// Authors:
//   Mike Kestner <mkestner@speakeasy.net>
//   Stephan Sundermann <stephansundermann@gmail.com>
//
// Copyright (c) 2001-2003 Mike Kestner
// Copyright (c) 2013 Stephan Sundermann
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
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Text;
using System.Xml;
using GapiCodegen.Interfaces;
using GapiCodegen.Utils;

namespace GapiCodegen.Generatables
{
    /// <summary>
    /// Abstract base class for types that will be translated to C# structs.
    /// </summary>
    public abstract class StructBase : ClassBase, IManualMarshaler
    {
        private readonly IList<StructField> _fields = new List<StructField>();
        private bool _needReadNative;

        protected StructBase(XmlElement namespaceElement, XmlElement element) : base(namespaceElement, element)
        {
            foreach (XmlNode node in element.ChildNodes)
            {
                if (!(node is XmlElement)) continue;
                var member = (XmlElement)node;

                switch (node.Name)
                {
                    case Constants.Field:
                        _fields.Add(new StructField(member, this));
                        break;

                    case Constants.Callback:
                        Statistics.IgnoreCount++;
                        break;

                    default:
                        if (!IsNodeNameHandled(node.Name)){
                            Console.WriteLine($"Unexpected node {node.Name} in {CName}");}
                        break;
                }
            }
        }

        private bool DisableNew => Element.GetAttributeAsBoolean(Constants.DisableNew);

        public override string DefaultValue => $"{QualifiedName}.Zero";

        public override string MarshalType => "IntPtr";

        public override string AssignToName => throw new NotImplementedException();

        public override string CallByName()
        {
            return "this_as_native";
        }

        public override string CallByName(string varName)
        {
            return $"{varName}_as_native";
        }

        public override string FromNative(string varName)
        {
            if (DisableNew)
                return string.Format(
                    "{0} == IntPtr.Zero ? {1}.Zero : ({1}) System.Runtime.InteropServices.Marshal.PtrToStructure ({0}, typeof ({1}))",
                    varName, QualifiedName);
            
            return $"{QualifiedName}.New ({varName})";
        }

        public string AllocNative(string var)
        {
            return $"GLib.Marshaller.StructureToPtrAlloc ({var})";
        }

        public string ReleaseNative(string var)
        {
            return $"Marshal.FreeHGlobal ({var})";
        }

        public virtual bool Union => false;

        public override bool CanGenerateAbiStruct(LogWriter logWriter)
        {
            logWriter.Info("Not generating any ABI structs for managed structures");

            return false;
        }

        public override void Generate(GenerationInfo generationInfo)
        {
            var needClose = false;

            if (generationInfo.Writer == null)
            {
                generationInfo.Writer = generationInfo.OpenStream(Name, Namespace);
                needClose = true;
            }

            var sw = generationInfo.Writer;

            sw.WriteLine($"namespace {Namespace} {{");
            sw.WriteLine();
            sw.WriteLine("\tusing System;");
            sw.WriteLine("\tusing System.Collections;");
            sw.WriteLine("\tusing System.Collections.Generic;");
            sw.WriteLine("\tusing System.Runtime.InteropServices;");
            sw.WriteLine();

            sw.WriteLine("#region Autogenerated code");

            if (IsDeprecated)
                sw.WriteLine("\t[Obsolete]");

            sw.WriteLine(Union ? "\t[StructLayout(LayoutKind.Explicit)]" : "\t[StructLayout(LayoutKind.Sequential)]");

            var access = IsInternal ? "internal" : "public";
            sw.WriteLine("\t{1} partial struct {0} : IEquatable<{0}> {{", Name, access);
            sw.WriteLine();

            _needReadNative = false;

            GenerateFields(generationInfo);
            sw.WriteLine();

            GenerateConstructors(generationInfo);
            GenerateMethods(generationInfo, null, this);

            if (_needReadNative)
                GenerateReadNative(sw);
            
            GenerateEqualsAndHash(sw);

            if (!needClose)
                return;

            sw.WriteLine("#endregion");

            sw.WriteLine("\t}");
            sw.WriteLine("}");
            sw.Close();
            generationInfo.Writer = null;
        }

        private new void GenerateFields(GenerationInfo generationInfo)
        {
            var sw = generationInfo.Writer;
            var bitfields = 0;
            var needField = true;

            foreach (var field in _fields)
            {
                if (Union)
                    sw.WriteLine("\t\t[FieldOffset(0)]");

                if (field.IsBitfield)
                {
                    if (needField)
                    {
                        sw.WriteLine("\t\tprivate uint _bitfield{0};\n", bitfields++);
                        needField = false;
                    }
                }
                else
                    needField = true;

                field.Generate(generationInfo, "\t\t");
            }
        }

        protected override void GenerateConstructors(GenerationInfo generationInfo)
        {
            var sw = generationInfo.Writer;

            sw.WriteLine("\t\tpublic static {0} Zero = new {0} ();", QualifiedName);
            sw.WriteLine();

            if (!DisableNew)
            {
                sw.WriteLine($"\t\tpublic static {QualifiedName} New(IntPtr raw) {{");
                sw.WriteLine("\t\t\tif (raw == IntPtr.Zero)");
                sw.WriteLine("\t\t\t\treturn {0}.Zero;", QualifiedName);
                sw.WriteLine("\t\t\treturn ({0}) Marshal.PtrToStructure (raw, typeof ({0}));", QualifiedName);
                sw.WriteLine("\t\t}");
                sw.WriteLine();
            }

            foreach (var ctor in Constructors)
                ctor.IsStatic = true;

            base.GenerateConstructors(generationInfo);
        }

        private void GenerateReadNative(TextWriter sw)
        {
            sw.WriteLine("\t\tstatic void ReadNative (IntPtr native, ref {0} target)", QualifiedName);
            sw.WriteLine("\t\t{");
            sw.WriteLine("\t\t\ttarget = New (native);");
            sw.WriteLine("\t\t}");
            sw.WriteLine();
        }

        protected void GenerateEqualsAndHash(StreamWriter sw)
        {
            var equals = new StringBuilder("true");
            var hashCode = new StringBuilder("this.GetType ().FullName.GetHashCode ()");

            var bitfields = 0;
            var needField = true;

            foreach (var field in _fields)
            {
                if (field.IsPadding || field.Hidden)
                    continue;

                if (field.IsBitfield)
                {
                    if (!needField) continue;

                    equals.Append(" && _bitfield");
                    equals.Append(bitfields);
                    equals.Append(".Equals (other._bitfield");
                    equals.Append(bitfields);
                    equals.Append(")");

                    hashCode.Append(" ^ ");
                    hashCode.Append("_bitfield");
                    hashCode.Append(bitfields++);
                    hashCode.Append(".GetHashCode ()");

                    needField = false;
                }
                else
                {
                    equals.Append(" && ");
                    equals.Append(field.EqualityName);
                    equals.Append(".Equals (other.");
                    equals.Append(field.EqualityName);
                    equals.Append(")");

                    hashCode.Append(" ^ ");
                    hashCode.Append(field.EqualityName);
                    hashCode.Append(".GetHashCode ()");

                    needField = true;
                }
            }

            if (!Element.GetAttributeAsBoolean("noequals"))
            {
                sw.WriteLine("\t\tpublic bool Equals ({0} other)", Name);
                sw.WriteLine("\t\t{");
                sw.WriteLine("\t\t\treturn {0};", equals);
                sw.WriteLine("\t\t}");
                sw.WriteLine();
            }

            sw.WriteLine("\t\tpublic override bool Equals (object other)");
            sw.WriteLine("\t\t{");
            sw.WriteLine("\t\t\treturn other is {0} && Equals (({0}) other);", Name);
            sw.WriteLine("\t\t}");
            sw.WriteLine();

            if (Element.GetAttributeAsBoolean("nohash"))
                return;

            sw.WriteLine("\t\tpublic override int GetHashCode ()");
            sw.WriteLine("\t\t{");
            sw.WriteLine("\t\t\treturn {0};", hashCode);
            sw.WriteLine("\t\t}");
            sw.WriteLine();
        }

        public override void Prepare(StreamWriter sw, string indent)
        {
            sw.WriteLine(
                $"{indent}IntPtr this_as_native = System.Runtime.InteropServices.Marshal.AllocHGlobal (System.Runtime.InteropServices.Marshal.SizeOf (this));");
            sw.WriteLine($"{indent}System.Runtime.InteropServices.Marshal.StructureToPtr (this, this_as_native, false);");
        }

        public override void Finish(StreamWriter sw, string indent)
        {
            _needReadNative = true;
            
            sw.WriteLine($"{indent}ReadNative (this_as_native, ref this);");
            sw.WriteLine($"{indent}System.Runtime.InteropServices.Marshal.FreeHGlobal (this_as_native);");
        }

        public override bool Validate()
        {
            var log = new LogWriter(QualifiedName);

            return _fields.Where(field => !field.Validate(log)).All(field => field.IsPointer) && base.Validate();
        }
    }
}
