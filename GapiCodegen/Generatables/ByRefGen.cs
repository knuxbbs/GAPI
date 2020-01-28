// GtkSharp.Generation.ByRefGen.cs - The ByRef type Generatable.
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


using GapiCodegen.Interfaces;

namespace GapiCodegen.Generatables {
	public class ByRefGen : SimpleBase, IManualMarshaler {
		
		public ByRefGen (string cName, string type) : base (cName, type, type + ".Empty") {}
		
		public override string MarshalType {
			get {
				return "IntPtr";
			}
		}

		public override string CallByName (string varName)
		{
			return "native_" + varName;
		}
		
		public string AllocNative ()
		{
			return "Marshal.AllocHGlobal (Marshal.SizeOf (typeof (" + QualifiedName + ")))";
		}

		public string AllocNative (string var_name)
		{
			return "GLib.Marshaller.StructureToPtrAlloc (" + var_name + ")";
		}

		public override string FromNative (string varName)
		{
			return string.Format ("({0}) Marshal.PtrToStructure ({1}, typeof ({0}))", QualifiedName, varName);
		}

		public string ReleaseNative (string var_name)
		{
			return "Marshal.FreeHGlobal (" + var_name + ")";
		}
	}
}

