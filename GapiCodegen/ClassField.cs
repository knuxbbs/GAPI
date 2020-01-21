// GtkSharp.Generation.ClassField.cs - used in class structures
//
// Copyright (c) 2009 Christian Hoff
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
using GapiCodegen.Utils;

namespace GapiCodegen
{
    /// <summary>
    /// Handles 'field' elements in classes.
    /// </summary>
    public class ClassField : StructField
    {
        protected ObjectBase ObjectBase;

        public ClassField(XmlElement element, ObjectBase containerType) : base(element, containerType)
        {
            ObjectBase = containerType;
        }

        public override bool Validate(LogWriter logWriter)
        {
            if (!base.Validate(logWriter))
                return false;

            if (!IsBitfield) return true;

            logWriter.Warn("Bitfields are not supported.");
            return false;
        }
    }
}
