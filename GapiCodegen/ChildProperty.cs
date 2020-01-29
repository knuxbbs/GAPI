// GtkSharp.Generation.ChildProperty.cs - GtkContainer child properties
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

using System.Xml;
using GapiCodegen.Generatables;

namespace GapiCodegen
{
    /// <summary>
    /// Handles 'childprop' elements.
    /// </summary>
    public class ChildProperty : Property
    {
        public ChildProperty(XmlElement element, ClassBase containerType) : base(element, containerType) { }

        protected override string PropertyAttribute(string name)
        {
            return $"[Gtk.ChildProperty({name})]";
        }

        protected override string RawGetter(string name)
        {
            return $"parent.ChildGetProperty(child, {name})";
        }

        protected override string RawSetter(string name)
        {
            return $"parent.ChildSetProperty(child, {name}, val)";
        }
    }
}
