// GtkSharp.Generation.PropertyBase.cs - base class for properties and
// fields
//
// Copyright (c) 2005 Novell, Inc.
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
using GapiCodegen.Util;

namespace GapiCodegen {
	public abstract class PropertyBase {

		protected XmlElement Element;
		public ClassBase container_type;

		public PropertyBase (XmlElement element, ClassBase container_type)
		{
			this.Element = element;
			this.container_type = container_type;
		}

		public string Name {
			get {
				return Element.GetAttribute ("name");
			}
		}

		public virtual string CName {
			get {
				return Element.GetAttribute ("cname");
			}
		}

		protected string ctype;
		public virtual string CType {
			get {
				if (ctype == null) {
					if (Element.GetAttribute("bits") == "1")
						ctype = "gboolean";
					else
						ctype = Element.GetAttribute("type");
				}
				return ctype;
			}
		}

		protected string cstype;
		public string CSType {
			get {
				if (Getter != null)
					return Getter.Signature.IsAccessor ? Getter.Signature.AccessorType : Getter.ReturnType;
				else if (Setter != null)
					return Setter.Signature.Types;
				else if (cstype == null)
					cstype = SymbolTable.Table.GetCsType (CType);
				return cstype;
			}
		}

		public virtual bool Hidden {
			get {
				return Element.GetAttributeAsBoolean ("hidden");
			}
		}

		protected bool IsNew {
			get {
				return Element.GetAttributeAsBoolean ("new_flag");
			}
		}

		protected Method Getter {
			get {
				Method getter = container_type.GetMethod ("Get" + Name);
				if (getter != null && getter.Name == "Get" + Name && getter.IsGetter)
					return getter;
				else
					return null;
			}
		}

		protected Method Setter {
			get {
				Method setter = container_type.GetMethod ("Set" + Name);
				if (setter != null && setter.Name == "Set" + Name && setter.IsSetter && (Getter == null || setter.Signature.Types == CSType))
					return setter;
				else
					return null;
			}
		}

		protected virtual void GenerateImports (GenerationInfo gen_info, string indent)
		{
			if (Getter != null)
				Getter.GenerateImport (gen_info.Writer);
			if (Setter != null)
				Setter.GenerateImport (gen_info.Writer);
		}
	}
}

