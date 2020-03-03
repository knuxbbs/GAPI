// GtkSharp.Generation.SimpleBase.cs - base class for marshaling non-generated types.
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

using GapiCodegen.Interfaces;

namespace GapiCodegen.Generatables
{
    /// <summary>
    /// Abstract base class for types which aren’t generated from xml like simple types or manually wrapped/implemented types.
    /// </summary>
    public abstract class SimpleBase : IGeneratable
    {
        private readonly string _namespace = string.Empty;

        protected SimpleBase(string cName, string type, string defaultValue)
        {
            CName = cName;
            DefaultValue = defaultValue;

            var toks = type.Split('.');
            Name = toks[toks.Length - 1];

            if (toks.Length > 2)
                _namespace = string.Join(".", toks, 0, toks.Length - 1);
            else if (toks.Length == 2)
                _namespace = toks[0];
        }

        public string CName { get; }

        public virtual string DefaultValue { get; }

        public string Name { get; }

        public string QualifiedName => string.IsNullOrEmpty(_namespace) ? Name : $"{_namespace}.{Name}";

        public virtual string MarshalType => QualifiedName;

        public virtual string CallByName(string varName)
        {
            return varName;
        }

        public virtual string FromNative(string varName)
        {
            return varName;
        }

        public bool Validate()
        {
            return true;
        }

        public void Generate()
        {
            //TODO: Remove
        }

        public void Generate(GenerationInfo generationInfo)
        {
            //TODO: Remove
        }

        public virtual string GenerateGetSizeOf()
        {
            //TODO: Remove
            return null;
        }

        public virtual string GenerateAlign()
        {
            //TODO: Remove
            return null;
        }
    }
}
