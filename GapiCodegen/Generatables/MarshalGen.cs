// GtkSharp.Generation.MarshalGen.cs - Simple marshaling Generatable.
//
// Author: Mike Kestner <mkestner@novell.com>
//
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

namespace GapiCodegen.Generatables
{
    /// <summary>
    /// Handles types that must be manually marshalled between managed and unmanaged code via special CallByName/FromNative syntax.
    /// </summary>
    public class MarshalGen : SimpleBase
    {
        private readonly string _callFmt;
        private readonly string _fromFmt;

        public MarshalGen(string cName, string type, string mtype, string callFmt, string fromFmt, string defaultValue)
            : base(cName, type, defaultValue)
        {
            MarshalType = mtype;
            _callFmt = callFmt;
            _fromFmt = fromFmt;
        }

        public MarshalGen(string cName, string type, string mtype, string callFmt, string fromFmt)
            : this(cName, type, mtype, callFmt, fromFmt, "null") { }

        public override string MarshalType { get; }

        public override string CallByName(string varName)
        {
            return string.Format(_callFmt, varName);
        }

        public override string FromNative(string varName)
        {
            return string.Format(_fromFmt, varName);
        }
    }
}
