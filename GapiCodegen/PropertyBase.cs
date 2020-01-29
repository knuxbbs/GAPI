// GtkSharp.Generation.PropertyBase.cs - base class for properties and
// fields
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

using System.Xml;
using GapiCodegen.Generatables;
using GapiCodegen.Utils;

namespace GapiCodegen
{
    /// <summary>
    /// Abstract base class for property-like elements.
    /// </summary>
    public abstract class PropertyBase
    {
        protected readonly XmlElement Element;
        protected readonly ClassBase ContainerType;

        protected PropertyBase(XmlElement element, ClassBase containerType)
        {
            Element = element;
            ContainerType = containerType;
        }

        public string Name => Element.GetAttribute(Constants.Name);

        public virtual string CName => Element.GetAttribute(Constants.CName);

        private string _cType;

        public virtual string CType
        {
            get
            {
                if (_cType != null) return _cType;

                _cType = Element.GetAttribute(Constants.Bits) == "1"
                    ? "gboolean"
                    : Element.GetAttribute(Constants.Type);

                return _cType;
            }
            protected set => _cType = value;
        }

        protected string CsType
        {
            get
            {
                if (Getter != null)
                    return Getter.Signature.IsAccessor ? Getter.Signature.AccessorType : Getter.ReturnType;

                return Setter != null ? Setter.Signature.Types : SymbolTable.Table.GetCsType(CType);
            }
        }

        public virtual bool Hidden => Element.GetAttributeAsBoolean(Constants.Hidden);

        protected bool IsNew => Element.GetAttributeAsBoolean(Constants.NewFlag);

        protected Method Getter
        {
            get
            {
                var getter = ContainerType.GetMethod($"Get{Name}");

                if (getter != null && getter.Name == $"Get{Name}" && getter.IsGetter)
                    return getter;

                return null;
            }
        }

        protected Method Setter
        {
            get
            {
                var setter = ContainerType.GetMethod($"Set{Name}");

                if (setter != null && setter.Name == $"Set{Name}" && setter.IsSetter &&
                    (Getter == null || setter.Signature.Types == CsType))
                    return setter;

                return null;
            }
        }

        protected virtual void GenerateImports(GenerationInfo generationInfo, string indent)
        {
            Getter?.GenerateImport(generationInfo.Writer);
            Setter?.GenerateImport(generationInfo.Writer);
        }
    }
}
