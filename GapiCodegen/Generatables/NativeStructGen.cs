// Authors:
//   Stephan Sundermann <stephansundermann@gmail.com>
//
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

using System.Collections.Generic;
using System.IO;
using System.Text;
using System.Xml;
using GapiCodegen.Utils;

namespace GapiCodegen.Generatables
{
	public class NativeStructGen : HandleBase
	{
		IList<StructField> fields = new List<StructField> ();

		public NativeStructGen (XmlElement namespaceElement, XmlElement element) : base (namespaceElement, element)
		{
			LogWriter log = new LogWriter (QualifiedName);

			foreach (XmlNode node in element.ChildNodes) {

				if (!(node is XmlElement)) continue;
				XmlElement member = (XmlElement) node;

				switch (node.Name) {
				case "field":
					fields.Add (new StructField (member, this));
					break;

				default:
					if (!IsNodeNameHandled (node.Name))
						log.Warn ("Unexpected node " + node.Name + " in " + CName);
					break;
				}
			}
		}

		public override string MarshalType {
			get {
				return "IntPtr";
			}
		}

		public override string AssignToName {
			get { return "Handle"; }
		}

		public override string CallByName ()
		{
			return "Handle";
		}

		public override string CallByName (string var)
		{
			return string.Format ("{0} == null ? IntPtr.Zero : {0}.{1}", var, "Handle");
		}

		public override string FromNative (string var, bool owned)
		{
			return "new " + QualifiedName + "( " + var + " )";
		}

		public override void Generate (GenerationInfo generationInfo)
		{
			bool need_close = false;
			if (generationInfo.Writer == null) {
				generationInfo.Writer = generationInfo.OpenStream (Name, Namespace);
				need_close = true;
			}

			StreamWriter sw = generationInfo.Writer;
			
			sw.WriteLine ("namespace " + Namespace + " {");
			sw.WriteLine ();
			sw.WriteLine ("\tusing System;");
			sw.WriteLine ("\tusing System.Collections;");
			sw.WriteLine ("\tusing System.Collections.Generic;");
			sw.WriteLine ("\tusing System.Runtime.InteropServices;");
			sw.WriteLine ();

			sw.WriteLine ("#region Autogenerated code");
			if (IsDeprecated)
				sw.WriteLine ("\t[Obsolete]");
			string access = IsInternal ? "internal" : "public";
			sw.WriteLine ("\t" + access + " partial class {0} : {1} IEquatable<{0}> {{", Name, Parent == null ? "GLib.IWrapper," : Parent.QualifiedName + ",");
			sw.WriteLine ();

			GenNativeStruct (generationInfo);
			GenNativeAccessor (generationInfo);
			GenFields (generationInfo);
			sw.WriteLine ();
			GenCtors (generationInfo);
			GenMethods (generationInfo, null, this);
			GenEqualsAndHash (sw);

			if (!need_close)
				return;

			sw.WriteLine ("#endregion");

			sw.WriteLine ("\t}");
			sw.WriteLine ("}");
			sw.Close ();
			generationInfo.Writer = null;
		}

		private void GenNativeStruct (GenerationInfo gen_info)
		{
			StreamWriter sw = gen_info.Writer;

			sw.WriteLine ("\t\t[StructLayout(LayoutKind.Sequential)]");
			sw.WriteLine ("\t\tprivate partial struct NativeStruct {");
			foreach (StructField field in fields) {
				field.Generate (gen_info, "\t\t\t");
			}
			sw.WriteLine ("\t\t}");
			sw.WriteLine ();
		}

		private void GenNativeAccessor (GenerationInfo gen_info)
		{
			StreamWriter sw = gen_info.Writer;

			sw.WriteLine ("\t\tNativeStruct Native {{", QualifiedName);
			sw.WriteLine ("\t\t\tget { return (NativeStruct) Marshal.PtrToStructure (Handle, typeof (NativeStruct)); }");
			sw.WriteLine ("\t\t}");
			sw.WriteLine ();
		}

		protected override void GenCtors (GenerationInfo gen_info)
		{
			StreamWriter sw = gen_info.Writer;

			if (Parent == null) {
				sw.WriteLine ("\t\tpublic {0} (IntPtr raw)", Name);
				sw.WriteLine ("\t\t{");
				sw.WriteLine ("\t\t\tthis.Handle = raw;");
				sw.WriteLine ("\t\t}");
			}
			else
				sw.Write ("\t\tpublic {0} (IntPtr raw) : base (raw) {{}}", Name);

			sw.WriteLine ();

			base.GenCtors (gen_info);
		}

		protected new void GenFields (GenerationInfo gen_info)
		{
			StreamWriter sw = gen_info.Writer;
			sw.WriteLine ("\t\tprivate IntPtr Raw;");

			sw.WriteLine ("\t\tpublic IntPtr Handle {");
			sw.WriteLine ("\t\t\tget { return Raw; }");
			sw.WriteLine ("\t\t\tset { Raw = value; }");
			sw.WriteLine ("\t\t}");
			sw.WriteLine ();

			foreach (StructField field in fields) {
				if (!field.Visible)
					continue;
				sw.WriteLine ("\t\tpublic {0} {1} {{", SymbolTable.Table.GetCsType (field.CType), field.StudlyName);
				sw.WriteLine ("\t\t\tget {{ return Native.{0}; }}", field.StudlyName);
				if (!(SymbolTable.Table [field.CType] is CallbackGen))
					sw.WriteLine ("\t\t\tset {{ NativeStruct native = Native; native.{0} = value;  Marshal.StructureToPtr (native, this.Handle, false); }}", field.StudlyName);
				sw.WriteLine ("\t\t}");
			}
		}

		protected void GenEqualsAndHash (StreamWriter sw)
		{
			StringBuilder hashcode = new StringBuilder ();
			StringBuilder equals = new StringBuilder ();

			sw.WriteLine ("\t\tpublic bool Equals ({0} other)", Name);
			sw.WriteLine ("\t\t{");
			hashcode.Append ("this.GetType().FullName.GetHashCode()");
			equals.Append ("true");

			foreach (StructField field in fields) {
				if (field.IsPadding || !field.Visible || field.IsBitfield)
					continue;

				equals.Append (" && ");
				equals.Append (field.EqualityName);
				equals.Append (".Equals (other.");
				equals.Append (field.EqualityName);
				equals.Append (")");
				hashcode.Append (" ^ ");
				hashcode.Append (field.EqualityName);
				hashcode.Append (".GetHashCode ()");
			}
			sw.WriteLine ("\t\t\treturn {0};", equals.ToString ());
			sw.WriteLine ("\t\t}");
			sw.WriteLine ();
			sw.WriteLine ("\t\tpublic override bool Equals (object other)");
			sw.WriteLine ("\t\t{");
			sw.WriteLine ("\t\t\treturn other is {0} && Equals (({0}) other);", Name);
			sw.WriteLine ("\t\t}");
			sw.WriteLine ();
			if (Element.GetAttribute ("nohash") == "true")
				return;
			sw.WriteLine ("\t\tpublic override int GetHashCode ()");
			sw.WriteLine ("\t\t{");
			sw.WriteLine ("\t\t\treturn {0};", hashcode.ToString ());
			sw.WriteLine ("\t\t}");
			sw.WriteLine ();

		}
	}
}

