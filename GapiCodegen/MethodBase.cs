// GtkSharp.Generation.MethodBase.cs - function element base class.
//
// Author: Mike Kestner <mkestner@novell.com>
//
// Copyright (c) 2001-2003 Mike Kestner
// Copyright (c) 2004-2005 Novell, Inc.
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

using System;
using System.Xml;
using GapiCodegen.Generatables;
using GapiCodegen.Utils;

namespace GapiCodegen
{
    /// <summary>
    /// Abstract base class for constructors, methods and virtual methods.
    /// </summary>
    public abstract class MethodBase
    {
        protected readonly XmlElement Element;
        protected readonly ClassBase ContainerType;

        protected MethodBase(XmlElement element, ClassBase containerType)
        {
            Element = element;
            ContainerType = containerType;

            Name = element.GetAttribute(Constants.Name);
            Parameters = new Parameters(element[Constants.Parameters]);
            IsStatic = element.GetAttribute(Constants.Shared) == "true";

            Protection = "public";
            Modifiers = string.Empty;

            if (element.GetAttributeAsBoolean(Constants.NewFlag))
                Modifiers = "new ";

            if (!element.HasAttribute(Constants.Accessibility)) return;

            var protection = element.GetAttribute(Constants.Accessibility);

            switch (protection)
            {
                case "public":
                case "protected":
                case "internal":
                case "private":
                case "protected internal":
                    Protection = protection;
                    break;
            }
        }

        protected string BaseName
        {
            get
            {
                var name = Name;
                var index = Name.LastIndexOf(".", StringComparison.Ordinal);

                if (index > 0)
                    name = Name.Substring(index + 1);

                return name;
            }
        }

        private MethodBody _body;

        protected MethodBody Body
        {
            get
            {
                if (_body != null) return _body;

                var logWriter = new LogWriter(Name);
                _body = new MethodBody(Parameters, logWriter);

                return _body;
            }
        }

        public virtual string CName => SymbolTable.Table.MangleName(Element.GetAttribute(Constants.CName));

        protected bool HasGetterName
        {
            get
            {
                if (BaseName.Length <= 3)
                    return false;

                if (BaseName.StartsWith("Get") || BaseName.StartsWith("Has"))
                    return char.IsUpper(BaseName[3]);

                return BaseName.StartsWith("Is") && char.IsUpper(BaseName[2]);
            }
        }

        protected bool HasSetterName
        {
            get
            {
                if (BaseName.Length <= 3)
                    return false;

                return BaseName.StartsWith("Set") && char.IsUpper(BaseName[3]);
            }
        }

        public bool IsStatic
        {
            get => Parameters.Static;
            set => Parameters.Static = value;
        }

        protected string LibraryName => Element.HasAttribute(Constants.Library)
            ? Element.GetAttribute(Constants.Library)
            : ContainerType.LibraryName;

        public string Modifiers { get; set; }

        public string Name { get; set; }

        public Parameters Parameters { get; protected set; }

        public string Protection { get; set; }

        protected string Safety => Body.ThrowsException && !(ContainerType is InterfaceGen) ? "unsafe " : "";

        private Signature _signature;

        public Signature Signature => _signature ?? (_signature = new Signature(Parameters));

        public virtual bool Validate(LogWriter logWriter)
        {
            logWriter.Member = Name;

            if (Parameters.Validate(logWriter)) return true;

            Statistics.ThrottledCount++;
            return false;
        }
    }
}
