// GtkSharp.Generation.Parameters.cs - The Parameters Generation Class.
//
// Author: Mike Kestner <mkestner@speakeasy.net>
//
// Copyright (c) 2001-2003 Mike Kestner
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

using System.Collections;
using System.Collections.Generic;
using System.Linq;
using System.Xml;
using GapiCodegen.Generatables;
using GapiCodegen.Utils;

namespace GapiCodegen
{
    /// <summary>
    /// Represents the list of parameters to a method.
    /// </summary>
    public class Parameters : IEnumerable<Parameter>
    {
        private IList<Parameter> _paramList = new List<Parameter>();
        private XmlElement _element;
        private readonly bool _firstIsInstance;

        public Parameters(XmlElement element) : this(element, false) { }

        public Parameters(XmlElement element, bool firstIsInstance)
        {
            if (element == null)
                _valid = true;

            _element = element;
            _firstIsInstance = firstIsInstance;

            if (firstIsInstance)
                Static = false;
        }

        public int Count => _paramList.Count;

        // gapi assumes GError** parameters to be error parameters in version 1 and 2
        private bool _throws;

        public bool Throws
        {
            get
            {
                if (Parser.GetVersion(_element.OwnerDocument.DocumentElement) <= 2)
                    return true;

                if (!_throws && _element.HasAttribute("throws"))
                    _throws = _element.GetAttributeAsBoolean("throws");

                return _throws;
            }
        }

        public int VisibleCount => this.Count(p => !IsHidden(p));

        public Parameter this[int index] => _paramList[index];

        public bool IsHidden(Parameter parameter)
        {
            var index = _paramList.IndexOf(parameter);

            if (index > 0 && parameter.IsLength && parameter.PassAs == string.Empty && this[index - 1].IsString)
                return true;

            if (parameter.IsCount)
                return true;

            if (parameter.IsHidden)
                return true;

            if (parameter.CType == "GError**" && Throws)
                return true;

            if (!HasCallback && !HideData) return false;

            if (Parser.GetVersion(_element.OwnerDocument.DocumentElement) >= 3)
            {
                foreach (var param in _paramList)
                {
                    if (param.Closure == index)
                        return true;
                    if (param.DestroyNotify == index)
                        return true;
                }
            }
            else
            {
                if (parameter.IsUserData && index == Count - 1)
                    return true;
                if (parameter.IsUserData && index == Count - 2 && this[Count - 1] is ErrorParameter)
                    return true;
                if (parameter.IsUserData && index > 0 && this[index - 1].Generatable is CallbackGen)
                    return true;
                if (parameter.IsDestroyNotify && index == Count - 1 && this[index - 1].IsUserData)
                    return true;
            }

            return false;
        }

        public bool HasCallback { get; set; }

        public bool HasOutParam => this.Any(p => p.PassAs == "out");

        public bool HideData { get; set; }

        public bool Static { get; set; }

        internal bool HasOptional { get; private set; }

        public Parameter GetCountParameter(string paramName)
        {
            foreach (var parameter in this)
                if (parameter.Name == paramName)
                {
                    parameter.IsCount = true;
                    return parameter;
                }

            return null;
        }

        private void Clear()
        {
            _element = null;
            _paramList.Clear();
            _paramList = null;
        }

        public IEnumerator<Parameter> GetEnumerator()
        {
            return _paramList.GetEnumerator();
        }

        IEnumerator IEnumerable.GetEnumerator()
        {
            return GetEnumerator();
        }

        private bool _valid;

        public bool Validate(LogWriter log)
        {
            if (_valid)
                return true;

            if (_element == null)
                return false;

            for (var i = _firstIsInstance ? 1 : 0; i < _element.ChildNodes.Count; i++)
            {
                var elementChildNode = _element.ChildNodes[i] as XmlElement;

                if (elementChildNode == null || elementChildNode.Name != "parameter")
                    continue;

                var parameter = new Parameter(elementChildNode);

                if (parameter.IsEllipsis)
                {
                    log.Warn("Ellipsis parameter: hide and bind manually if no alternative exists.");
                    Clear();

                    return false;
                }

                if (parameter.CsType == "" || parameter.Name == "" ||
                    parameter.MarshalType == "" || SymbolTable.Table.CallByName(parameter.CType, parameter.Name) == "")
                {
                    log.Warn("Unknown type {1} on parameter {0}", parameter.Name, parameter.CType);
                    Clear();

                    return false;
                }

                if (parameter.IsOptional && parameter.PassAs == string.Empty && parameter.IsUserData == false)
                    HasOptional = true;

                var generatable = parameter.Generatable;

                if (parameter.IsArray)
                {
                    parameter = new ArrayParameter(elementChildNode);

                    if (i < _element.ChildNodes.Count - 1)
                    {
                        if (_element.ChildNodes[i + 1] is XmlElement nextElement && nextElement.Name == "parameter")
                        {
                            var nextElementParam = new Parameter(nextElement);

                            if (nextElementParam.IsCount)
                            {
                                parameter = new ArrayCountPair(elementChildNode, nextElement, false);
                                i++;
                            }
                        }
                    }
                }
                else if (parameter.IsCount)
                {
                    parameter.IsCount = false;

                    if (i < _element.ChildNodes.Count - 1)
                    {
                        if (_element.ChildNodes[i + 1] is XmlElement nextElement && nextElement.Name == "parameter")
                        {
                            var nextElementParam = new Parameter(nextElement);

                            if (nextElementParam.IsArray)
                            {
                                parameter = new ArrayCountPair(nextElement, elementChildNode, true);
                                i++;
                            }
                        }
                    }
                }
                else if (parameter.CType == "GError**" && Throws)
                {
                    parameter = new ErrorParameter(elementChildNode);
                }
                else switch (generatable)
                {
                    case StructBase _:
                    case ByRefGen _:
                        parameter = new StructParameter(elementChildNode);
                        break;
                    case CallbackGen _:
                        HasCallback = true;
                        break;
                }

                _paramList.Add(parameter);
            }

            if (Parser.GetVersion(_element.OwnerDocument.DocumentElement) < 3 &&
                HasCallback && Count > 2 && this[Count - 3].Generatable is CallbackGen &&
                this[Count - 2].IsUserData && this[Count - 1].IsDestroyNotify)
            {
                this[Count - 3].Scope = "notified";
            }

            _valid = true;
            return _valid;
        }

        public bool IsAccessor => VisibleCount == 1 && AccessorParam.PassAs == "out";

        public Parameter AccessorParam => this.FirstOrDefault(parameter => !IsHidden(parameter));

        public string AccessorReturnType => AccessorParam?.CsType;

        public string AccessorName => AccessorParam?.Name;

        /// <summary>
        /// Represents a signature for an unmanaged method.
        /// </summary>
        public string ImportSignature
        {
            get
            {
                if (Count == 0)
                    return string.Empty;

                var result = new string[Count];

                for (var i = 0; i < Count; i++)
                    result[i] = this[i].NativeSignature;

                return string.Join(", ", result);
            }
        }
    }
}
