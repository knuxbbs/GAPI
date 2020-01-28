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


using GapiCodegen.Interfaces;

namespace GapiCodegen.Generatables {
	public class OwnableGen : SimpleBase, IOwnable {
		
		public OwnableGen (string cName, string type) : base (cName, type, "null") {}

		public override string MarshalType {
			get { return "IntPtr"; }
		}

		public override string CallByName (string varName)
		{
			return varName + " == null ? IntPtr.Zero : " + varName + ".Handle";
		}
		
		public override string FromNative (string varName)
		{
			return string.Format ("new {0} ({1})", QualifiedName, varName);
		}
		
		public string FromNative (string varName, bool owned)
		{
			return string.Format ("new {0} ({1}, {2})", QualifiedName, varName, owned ? "true" : "false");
		}
	}
}

