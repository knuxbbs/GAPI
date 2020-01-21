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

namespace GapiCodegen {
	public class Property : PropertyBase {

		public Property (XmlElement element, ClassBase container_type) : base (element, container_type) {}

		public bool Validate (LogWriter log)
		{
			if (CsType == "" && !Hidden) {
				log.Member = Name;
				log.Warn ("property has unknown type '{0}' ", CType);
				Statistics.ThrottledCount++;
				return false;
			}

			return true;
		}

		bool Readable {
			get {
				return Element.GetAttributeAsBoolean ("readable");
			}
		}

		bool Writable {
			get {
				return Element.GetAttributeAsBoolean ("writeable") &&
					!Element.GetAttributeAsBoolean ("construct-only");
			}
		}

		bool IsDeprecated {
			get {
				return !ContainerType.IsDeprecated &&
					Element.GetAttributeAsBoolean ("deprecated");
			}
		}

		protected virtual string PropertyAttribute (string qpname) {
			return "[GLib.Property (" + qpname + ")]";
		}

		protected virtual string RawGetter (string qpname) {
            if (ContainerType is InterfaceGen)
                return "implementor.GetProperty (" + qpname + ")";
			return "GetProperty (" + qpname + ")";
		}

		protected virtual string RawSetter (string qpname) {
            if (ContainerType is InterfaceGen)
                return "implementor.SetProperty(" + qpname + ", val)";
			return "SetProperty(" + qpname + ", val)";
		}

		public void GenerateDecl (StreamWriter sw, string indent)
		{
			if (Hidden || !Readable && !Writable)
				return;

			string name = Name;
			if (name == ContainerType.Name)
				name += "Prop";

			sw.WriteLine (indent + CsType + " " + name + " {");
			sw.Write (indent + "\t");
			if (Readable || Getter != null)
				sw.Write ("get; ");
			if (Writable || Setter != null)
				sw.Write ("set;");
			sw.WriteLine ();
			sw.WriteLine (indent + "}");
		}

		public void Generate (GenerationInfo gen_info, string indent, ClassBase implementor)
		{
			SymbolTable table = SymbolTable.Table;
			StreamWriter sw = gen_info.Writer;

			if (Hidden || !Readable && !Writable)
				return;

			string modifiers = "";

			if (IsNew || ContainerType.Parent != null && ContainerType.Parent.GetPropertyRecursively (Name) != null)
				modifiers = "new ";
			else if (implementor != null && implementor.Parent != null && implementor.Parent.GetPropertyRecursively (Name) != null)
				modifiers = "new ";

			string name = Name;
			if (name == ContainerType.Name) {
				name += "Prop";
			}
			string qpname = "\"" + CName + "\"";

			string v_type = "";
			if (table.IsInterface (CType)) {
				v_type = "(GLib.Object)";
			} else if (table.IsOpaque (CType)) {
				v_type = "(GLib.Opaque)";
			} else if (table.IsEnum (CType)) {
				v_type = "(Enum)";
			}

			GenerateImports (gen_info, indent);

			if (IsDeprecated ||
			    Getter != null && Getter.IsDeprecated ||
			    Setter != null && Setter.IsDeprecated)
				sw.WriteLine (indent + "[Obsolete]");
			sw.WriteLine (indent + PropertyAttribute (qpname));
			sw.WriteLine (indent + "public " + modifiers + CsType + " " + name + " {");
			indent += "\t";

			if (Getter != null) {
				sw.Write(indent + "get ");
				Getter.GenerateBody(gen_info, implementor, "\t");
				sw.WriteLine();
			} else if (Readable) {
				sw.WriteLine(indent + "get {");
				sw.WriteLine(indent + "\tGLib.Value val = " + RawGetter (qpname) + ";");
				if (table.IsOpaque (CType) || table.IsBoxed (CType)) {
					sw.WriteLine(indent + "\t" + CsType + " ret = (" + CsType + ") val;");
				} else if (table.IsInterface (CType)) {
					var igen = table.GetInterfaceGen (CType);

					// Do we have to dispose the GLib.Object from the GLib.Value?
					sw.WriteLine (indent + "\t{0} ret = {1}.GetObject ((GLib.Object) val);",
					              igen.QualifiedName, igen.QualifiedAdapterName);
				} else {
					sw.Write(indent + "\t" + CsType + " ret = ");
					sw.Write ("(" + CsType + ") ");
					if (v_type != "") {
						sw.Write(v_type + " ");
					}
					sw.WriteLine("val;");
				}

				sw.WriteLine(indent + "\tval.Dispose ();");
				sw.WriteLine(indent + "\treturn ret;");
				sw.WriteLine(indent + "}");
			}

			if (Setter != null) {
				sw.Write(indent + "set ");
				Setter.GenerateBody(gen_info, implementor, "\t");
				sw.WriteLine();
			} else if (Writable) {
				sw.WriteLine(indent + "set {");
				sw.Write(indent + "\tGLib.Value val = ");
				if (table.IsBoxed (CType)) {
					sw.WriteLine("(GLib.Value) value;");
				} else if (table.IsOpaque (CType)) {
					sw.WriteLine("new GLib.Value(value, \"{0}\");", CType);
				} else {
					sw.Write("new GLib.Value(");
					if (v_type != "" && !(table.IsObject (CType) || table.IsInterface (CType) || table.IsOpaque (CType))) {
						sw.Write(v_type + " ");
					}
					sw.WriteLine("value);");
				}
				sw.WriteLine(indent + "\t" + RawSetter (qpname) + ";");
				sw.WriteLine(indent + "\tval.Dispose ();");
				sw.WriteLine(indent + "}");
			}

			sw.WriteLine(indent.Substring (1) + "}");
			sw.WriteLine();

			Statistics.PropCount++;
		}
	}
}

