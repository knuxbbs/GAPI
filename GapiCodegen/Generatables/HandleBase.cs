// HandleBase.cs - Base class for Handle types
//
// Authors:  Mike Kestner <mkestner@novell.com>
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

using System.IO;
using System.Xml;
using GapiCodegen.Interfaces;

namespace GapiCodegen.Generatables
{
    /// <summary>
    /// Base class for wrapped IntPtr reference types.
    /// </summary>
    public abstract class HandleBase : ClassBase, IPropertyAccessor, IOwnable
    {
        protected HandleBase(XmlElement namespaceElement, XmlElement element) : base(namespaceElement, element) { }

        public override string AssignToName => "Raw";

        public override string GenerateGetSizeOf()
        {
            return $"{Namespace}.{Name}.abi_info.Size";
        }

        public override string GenerateAlign()
        {
            return $"{Namespace}.{Name}.abi_info.Align";
        }

        public override string MarshalType => "IntPtr";

        public override string CallByName(string name)
        {
            return $"{name} == null ? IntPtr.Zero : {name}.Handle";
        }

        public override string CallByName()
        {
            return "Handle";
        }

        public abstract string FromNative(string varName, bool owned);

        public override string FromNative(string varName)
        {
            return FromNative(varName, false);
        }

        public void WriteAccessors(TextWriter textWriter, string indent, string fieldName)
        {
            textWriter.WriteLine($"{indent}get {{");
            textWriter.WriteLine($"{indent}\treturn {FromNative(fieldName, false)};");
            textWriter.WriteLine($"{indent}}}");
            textWriter.WriteLine($"{indent}set {{");
            textWriter.WriteLine($"{indent}\t{fieldName} = {CallByName("value")};");
            textWriter.WriteLine($"{indent}}}");
        }
    }
}
