// GtkSharp.Generation.LPGen.cs - long/pointer Generatable.
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

using System.IO;
using GapiCodegen.Interfaces;

namespace GapiCodegen.Generatables
{
    /// <summary>
    /// Marshals system specific long and "size" types.
    /// </summary>
    public class LPGen : SimpleGen, IPropertyAccessor
    {
        public LPGen(string cType) : base(cType, "long", "0L") { }

        public override string MarshalType => "IntPtr";

        public override string CallByName(string varName)
        {
            return $"new IntPtr ({varName})";
        }

        public override string FromNative(string varName)
        {
            return $"(long) {varName}";
        }

        public void WriteAccessors(TextWriter textWriter, string indent, string fieldName)
        {
            textWriter.WriteLine($"{indent}get {{");
            textWriter.WriteLine($"{indent}\treturn {FromNative(fieldName)};");
            textWriter.WriteLine($"{indent}}}");
            textWriter.WriteLine($"{indent}set {{");
            textWriter.WriteLine($"{indent}\t{fieldName} = {CallByName("value")};");
            textWriter.WriteLine($"{indent}}}");
        }
    }
}
