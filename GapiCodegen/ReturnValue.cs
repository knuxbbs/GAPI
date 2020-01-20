// GtkSharp.Generation.ReturnValue.cs - The ReturnValue Generatable.
//
// Author: Mike Kestner <mkestner@novell.com>
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

using System.Xml;
using GapiCodegen.Generatables;
using GapiCodegen.Interfaces;

namespace GapiCodegen {
	public class ReturnValue  {

		
		bool is_null_term;
		bool is_array;
		bool elements_owned;
		bool owned;
		string array_length_param = string.Empty;
		int array_length_param_index = -1;
		string ctype = string.Empty;
		string default_value = string.Empty;
		string element_ctype = string.Empty;
		Parameter count_param;

		public ReturnValue (XmlElement elem)
		{
			if (elem != null) {
				is_null_term = elem.GetAttributeAsBoolean ("null_term_array");
				is_array = elem.GetAttributeAsBoolean ("array") || elem.HasAttribute ("array_length_param");
				array_length_param = elem.GetAttribute ("array_length_param");
				if (elem.HasAttribute ("array_length_param_length"))
					array_length_param_index = int.Parse (elem.GetAttribute ("array_length_param_index"));
				elements_owned = elem.GetAttributeAsBoolean ("elements_owned");
				owned = elem.GetAttributeAsBoolean ("owned");
				ctype = elem.GetAttribute("type");
				default_value = elem.GetAttribute ("default_value");
				element_ctype = elem.GetAttribute ("element_type");
			}
		}

		public Parameter CountParameter {
			get { return count_param; }
			set { count_param = value; }
		}

		public string CountParameterName {
			get { return array_length_param; }
		}

		public int CountParameterIndex {
			get { return array_length_param_index; }
		}

		public string CType {
			get {
				return ctype;
			}
		}

		public string CSType {
			get {
				if (IGen == null)
					return string.Empty;

				if (ElementType != string.Empty)
					return ElementType + "[]";

				return IGen.QualifiedName + (is_array || is_null_term ? "[]" : string.Empty);
			}
		}

		public string DefaultValue {
			get {
				if (!string.IsNullOrEmpty (default_value))
					return default_value;
				if (IGen == null)
					return string.Empty;
				return IGen.DefaultValue;
			}
		}

		string ElementType {
			get {
				if (element_ctype.Length > 0)
					return SymbolTable.Table.GetCsType (element_ctype);

				return string.Empty;
			}
		}

		IGeneratable igen;
		public IGeneratable IGen {
			get {
				if (igen == null)
					igen = SymbolTable.Table [CType];
				return igen;
			}
		}

		public bool IsVoid {
			get {
				return CSType == "void";
			}
		}

		public string MarshalType {
			get {
				if (IGen == null)
					return string.Empty;
				else if (is_array || is_null_term)
					return "IntPtr";
				return IGen.MarshalType;
			}
		}

		public string ToNativeType {
			get {
				if (IGen == null)
					return string.Empty;
				if (is_array || is_null_term)
					return "IntPtr";
				return IGen.MarshalType;
			}
		}

		public string FromNative (string var)
		{
			if (IGen == null)
				return string.Empty;

			if (ElementType != string.Empty) {
				string args = (owned ? "true" : "false") + ", " + (elements_owned ? "true" : "false");
				if (IGen.QualifiedName == "GLib.PtrArray")
					return string.Format ("({0}[]) GLib.Marshaller.PtrArrayToArray ({1}, {2}, typeof({0}))", ElementType, var, args);
				else
					return string.Format ("({0}[]) GLib.Marshaller.ListPtrToArray ({1}, typeof({2}), {3}, typeof({4}))", ElementType, var, IGen.QualifiedName, args, element_ctype == "gfilename*" ? "GLib.ListBase.FilenameString" : ElementType);
			} else if (IGen is IOwnable)
				return ((IOwnable)IGen).FromNative (var, owned);
			else if (is_null_term)
				return string.Format ("GLib.Marshaller.NullTermPtrToStringArray ({0}, {1})", var, owned ? "true" : "false");
			else if (is_array)
				return string.Format ("({0}) GLib.Marshaller.ArrayPtrToArray ({1}, typeof ({2}), (int){3}native_{4}, true)", CSType, var, IGen.QualifiedName, CountParameter.CSType == "int" ? string.Empty : "(" + CountParameter.CSType + ")", CountParameter.Name);
			else
				return IGen.FromNative (var);
		}
			
		public string ToNative (string var)
		{
			if (IGen == null)
				return string.Empty;

			if (ElementType.Length > 0) {
				string args = ", typeof (" + ElementType + "), " + (owned ? "true" : "false") + ", " + (elements_owned ? "true" : "false");
				var = "new " + IGen.QualifiedName + "(" + var + args + ")";
			} else if (is_null_term)
				return string.Format ("GLib.Marshaller.StringArrayToNullTermStrvPointer ({0})", var);
			else if (is_array)
				return string.Format ("GLib.Marshaller.ArrayToArrayPtr ({0})", var);

			if (IGen is IManualMarshaler)
				return (IGen as IManualMarshaler).AllocNative (var);
			else if (IGen is ObjectGen && owned)
				return var + " == null ? IntPtr.Zero : " + var + ".OwnedHandle";
			else if (IGen is OpaqueGen && owned)
				return var + " == null ? IntPtr.Zero : " + var + ".OwnedCopy";
			else
				return IGen.CallByName (var);
		}

		public bool Validate (LogWriter log)
		{
			if (MarshalType == "" || CSType == "") {
				log.Warn ("Unknown return type: {0}", CType);
				return false;
			} else if ((CSType == "GLib.List" || CSType == "GLib.SList") && string.IsNullOrEmpty (ElementType))
				log.Warn ("Returns {0} with unknown element type.  Add element_type attribute with gapi-fixup.", CType);

			if (is_array && !is_null_term && string.IsNullOrEmpty (array_length_param)) {
				log.Warn ("Returns an array with undeterminable length. Add null_term_array or array_length_param attribute with gapi-fixup.");
				return false;
			}

			return true;
		}
	}
}

