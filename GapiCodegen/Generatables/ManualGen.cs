// GtkSharp.Generation.ManualGen.cs - Ungenerated handle type Generatable.
//
// Author: Mike Kestner <mkestner@novell.com>
//
// Copyright (c) 2003 Mike Kestner
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


namespace GapiCodegen.Generatables {
	public class ManualGen : SimpleBase {
		
		string from_fmt;
		string abi_type;

		public ManualGen (string cType, string type) : base (cType, type, "null")
		{
			from_fmt = "new " + QualifiedName + "({0})";
		}

		public ManualGen (string cType, string type, string from_fmt) : base (cType, type, "null")
		{
			this.from_fmt = from_fmt;
		}

		public ManualGen (string cType, string type, string from_fmt, string abi_type) : base (cType, type, "null")
		{
			this.from_fmt = from_fmt;
			this.abi_type = abi_type;
		}

		public override string MarshalType {
			get {
				return "IntPtr";
			}
		}

		public string AbiType {
			get {
				return abi_type;
			}
		}

		public override string CallByName (string varName)
		{
			return varName + " == null ? IntPtr.Zero : " + varName + ".Handle";
		}
		
		public override string FromNative(string var)
		{
			return string.Format (from_fmt, var);
		}

		public override string GenerateGetSizeOf () {
			return "(uint) Marshal.SizeOf(typeof(" + abi_type + "))";
		}
	}
}

