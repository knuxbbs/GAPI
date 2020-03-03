// GtkSharp.Generation.StructGen.cs - The Structure Generatable.
//
// Author: Mike Kestner <mkestner@speakeasy.net>
//
// Copyright (c) 2001 Mike Kestner
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
using GapiCodegen.Utils;

namespace GapiCodegen.Generatables
{
    /// <summary>
    /// Handles non-opaque 'struct' elements.
    /// </summary>
    public class StructGen : StructBase
    {
        public StructGen(XmlElement namespaceElement, XmlElement element) : base(namespaceElement, element) { }

        public override void Generate(GenerationInfo generationInfo)
        {
            generationInfo.CurrentType = QualifiedName;

            var sw = generationInfo.Writer = generationInfo.OpenStream(Name, Namespace);

            base.Generate(generationInfo);

            if (GetMethod("GetType") == null && GetMethod("GetGType") == null)
            {
                sw.WriteLine("\t\tprivate static GLib.GType GType {");
                sw.WriteLine("\t\t\tget { return GLib.GType.Pointer; }");
                sw.WriteLine("\t\t}");
            }

            sw.WriteLine("#endregion");
            sw.WriteLine("\t}");
            sw.WriteLine("}");
            sw.Close();

            generationInfo.Writer = null;
            Statistics.StructCount++;
        }
    }
}
