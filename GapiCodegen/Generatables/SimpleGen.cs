// GtkSharp.Generation.SimpleGen.cs - The Simple type Generatable.
//
// Author: Mike Kestner <mkestner@speakeasy.net>
//
// Copyright (c) 2003 Mike Kestner
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
    /// Handles types that can be simply converted from an unmanaged type to a managed type (int, byte, short, etc…​)
    /// </summary>
    public class SimpleGen : SimpleBase
    {
        private readonly string _size;

        public SimpleGen(string cName, string type, string defaultValue) : base(cName, type, defaultValue) { }

        public SimpleGen(string cName, string type, string defaultValue, string size) : base(cName, type, defaultValue)
        {
            _size = size;
        }

        public override string GenerateGetSizeOf()
        {
            return _size;
        }
    }
}
