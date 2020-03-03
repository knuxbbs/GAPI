// GtkSharp.Generation.ConstStringGen.cs - The Const String type Generatable.
//
// Author: Rachel Hestilow <rachel@nullenvoid.com>
//         Mike Kestner <mkestner@novell.com>
//
// Copyright (c) 2003 Rachel Hestilow
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
    /// <summary>
    /// Handles conversion between "const char *" and System.String
    /// </summary>
    public class ConstStringGen : SimpleBase, IManualMarshaler
    {
        public ConstStringGen(string cName) : base(cName, "string", "null") { }

        public override string MarshalType => "IntPtr";

        public override string FromNative(string varName)
        {
            return $"GLib.Marshaller.Utf8PtrToString ({varName})";
        }

        public string AllocNative(string managedVar)
        {
            return $"GLib.Marshaller.StringToPtrGStrdup ({managedVar})";
        }

        public string ReleaseNative(string nativeVar)
        {
            return $"GLib.Marshaller.Free ({nativeVar})";
        }
    }
}
