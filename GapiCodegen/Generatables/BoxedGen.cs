// GtkSharp.Generation.BoxedGen.cs - The Boxed Generatable.
//
// Author: Mike Kestner <mkestner@speakeasy.net>
//
// Copyright (c) 2001-2003 Mike Kestner
// Copyright (c) 2003-2004 Novell, Inc.
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
    /// Handles non-opaque 'boxed' elements.
    /// </summary>
    public class BoxedGen : StructBase
    {
        public BoxedGen(XmlElement namespaceElement, XmlElement element) : base(namespaceElement, element) { }

        public override void Generate(GenerationInfo generationInfo)
        {
            generationInfo.CurrentType = QualifiedName;

            var sw = generationInfo.Writer = generationInfo.OpenStream(Name, Namespace);
            base.Generate(generationInfo);

            sw.WriteLine($"\t\tpublic static explicit operator GLib.Value ({QualifiedName} boxed)");
            sw.WriteLine("\t\t{");
            sw.WriteLine("\t\t\tGLib.Value val = GLib.Value.Empty;");
            sw.WriteLine($"\t\t\tval.Init ({QualifiedName}.GType);");
            sw.WriteLine("\t\t\tval.Val = boxed;");
            sw.WriteLine("\t\t\treturn val;");
            sw.WriteLine("\t\t}");
            sw.WriteLine();
            sw.WriteLine($"\t\tpublic static explicit operator {QualifiedName} (GLib.Value val)");
            sw.WriteLine("\t\t{");
            sw.WriteLine($"\t\t\treturn ({QualifiedName}) val.Val;");
            sw.WriteLine("\t\t}");

            var copy = GetMethod("Copy");
            var free = GetMethod("Free");
            Methods.Remove("Copy");
            Methods.Remove("Free");

            if (copy != null && copy.IsDeprecated)
            {
                sw.WriteLine();
                sw.WriteLine("\t\t[Obsolete(\"This is a no-op\")]");
                sw.WriteLine($"\t\tpublic {QualifiedName} Copy() {{");
                sw.WriteLine("\t\t\treturn this;");
                sw.WriteLine("\t\t}");
            }

            if (free != null && free.IsDeprecated)
            {
                sw.WriteLine();
                sw.WriteLine("\t\t[Obsolete(\"This is a no-op\")]");
                sw.WriteLine($"\t\tpublic {QualifiedName} Free () {{");
                sw.WriteLine("\t\t\treturn this;");
                sw.WriteLine("\t\t}");
            }

            sw.WriteLine("#endregion");
            sw.WriteLine("\t}");
            sw.WriteLine("}");
            sw.Close();
            
            generationInfo.Writer = null;
            Statistics.BoxedCount++;
        }
    }
}
