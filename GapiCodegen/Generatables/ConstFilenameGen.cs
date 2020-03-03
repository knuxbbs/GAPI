// ConstFilenameGen.cs - The Const Filename type Generatable.
//
// Author:  Mike Kestner <mkestner@novell.com>
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

using GapiCodegen.Interfaces;

namespace GapiCodegen.Generatables
{
    public class ConstFilenameGen : SimpleBase, IManualMarshaler
    {
        public ConstFilenameGen(string cName) : base(cName, "string", "null") { }

        public override string MarshalType => "IntPtr";

        public override string FromNative(string varName)
        {
            return $"GLib.Marshaller.FilenamePtrToString ({varName})";
        }

        public string AllocNative(string managedVar)
        {
            return $"GLib.Marshaller.StringToFilenamePtr ({managedVar})";
        }

        public string ReleaseNative(string nativeVar)
        {
            return $"GLib.Marshaller.Free ({nativeVar})";
        }
    }
}
