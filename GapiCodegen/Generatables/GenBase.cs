// GtkSharp.Generation.GenBase.cs - The Generatable base class.
//
// Author: Mike Kestner <mkestner@novell.com>
//
// Copyright (c) 2001-2002 Mike Kestner
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
using GapiCodegen.Interfaces;
using GapiCodegen.Utils;

namespace GapiCodegen.Generatables
{
    /// <summary>
    /// Abstract base class for any api.xml element that will have its own generated .cs file.
    /// </summary>
    public abstract class GenBase : IGeneratable
    {
        private readonly XmlElement _namespaceElement;

        protected GenBase(XmlElement namespaceElement, XmlElement element)
        {
            _namespaceElement = namespaceElement;
            Element = element;
        }

        public string CName => Element.GetAttribute(Constants.CName);

        public XmlElement Element { get; }

        public int ParserVersion
        {
            get
            {
                var root = Element.OwnerDocument.DocumentElement;

                return root.HasAttribute("parser_version") 
                    ? int.Parse(root.GetAttribute("parser_version")) 
                    : 1;
            }
        }

        public bool IsInternal => Element.GetAttributeAsBoolean(Constants.Internal);

        public string LibraryName => _namespaceElement.GetAttribute(Constants.Library);

        public abstract string MarshalType { get; }

        public virtual string Name => Element.GetAttribute(Constants.Name);

        public string Namespace => _namespaceElement.GetAttribute(Constants.Name);

        public abstract string DefaultValue { get; }

        public string QualifiedName => $"{Namespace}.{Name}";

        public abstract string CallByName(string var);

        public abstract string FromNative(string varName);

        public abstract bool Validate();

        public virtual string GenerateGetSizeOf()
        {
            return null;
        }

        public virtual string GenerateAlign()
        {
            return null;
        }

        public void Generate()
        {
            var generationInfo = new GenerationInfo(_namespaceElement);

            Generate(generationInfo);
        }

        public abstract void Generate(GenerationInfo generationInfo);
    }
}
