// GtkSharp.Generation.ReturnValue.cs - The ReturnValue Generatable.
//
// Author: Mike Kestner <mkestner@novell.com>
//
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

using System.Xml;
using GapiCodegen.Generatables;
using GapiCodegen.Interfaces;
using GapiCodegen.Utils;

namespace GapiCodegen
{
    /// <summary>
    /// Represents the return value of a method, virtual method or signal.
    /// </summary>
    public class ReturnValue
    {
        private readonly bool _isNullTermArray;
        private readonly bool _isArray;
        private readonly bool _elementsOwned;
        private readonly bool _owned;
        private readonly string _defaultValue = string.Empty;
        private readonly string _elementType = string.Empty;

        public ReturnValue(XmlElement element)
        {
            if (element == null) return;

            _isNullTermArray = element.GetAttributeAsBoolean(Constants.NullTermArray);
            _isArray = element.GetAttributeAsBoolean(Constants.Array) || element.HasAttribute("array_length_param");
            CountParameterName = element.GetAttribute("array_length_param");

            if (element.HasAttribute("array_length_param_length"))
                CountParameterIndex = int.Parse(element.GetAttribute("array_length_param_index"));

            _elementsOwned = element.GetAttributeAsBoolean(Constants.ElementsOwned);
            _owned = element.GetAttributeAsBoolean(Constants.Owned);

            CType = element.GetAttribute(Constants.Type);
            _defaultValue = element.GetAttribute(Constants.DefaultValue);
            _elementType = element.GetAttribute(Constants.ElementType);
        }

        public Parameter CountParameter { get; set; }

        public string CountParameterName { get; } = string.Empty;

        public int CountParameterIndex { get; } = -1;

        public string CType { get; } = string.Empty;

        public string CsType
        {
            get
            {
                if (Generatable == null)
                    return string.Empty;

                return ElementType != string.Empty
                    ? $"{ElementType}[]"
                    : $"{Generatable.QualifiedName}{(_isArray || _isNullTermArray ? "[]" : string.Empty)}";
            }
        }

        public string DefaultValue
        {
            get
            {
                if (!string.IsNullOrEmpty(_defaultValue))
                    return _defaultValue;

                return Generatable != null ? Generatable.DefaultValue : string.Empty;
            }
        }

        private string ElementType =>
            _elementType.Length > 0 ? SymbolTable.Table.GetCsType(_elementType) : string.Empty;

        private IGeneratable _generatable;

        public IGeneratable Generatable => _generatable ?? (_generatable = SymbolTable.Table[CType]);

        public bool IsVoid => CsType == "void";

        public string MarshalType
        {
            get
            {
                if (Generatable == null)
                    return string.Empty;

                if (_isArray || _isNullTermArray)
                    return "IntPtr";

                return Generatable.MarshalType;
            }
        }

        public string ToNativeType
        {
            get
            {
                if (Generatable == null)
                    return string.Empty;

                if (_isArray || _isNullTermArray)
                    return "IntPtr";

                return Generatable.MarshalType;
            }
        }

        public string FromNative(string var)
        {
            if (Generatable == null)
                return string.Empty;

            if (ElementType != string.Empty)
            {
                var args = $"{(_owned ? "true" : "false")}, {(_elementsOwned ? "true" : "false")}";

                return Generatable.QualifiedName == "GLib.PtrArray"
                    ? string.Format("({0}[]) GLib.Marshaller.PtrArrayToArray ({1}, {2}, typeof({0}))", ElementType, var,
                        args)
                    : string.Format("({0}[]) GLib.Marshaller.ListPtrToArray ({1}, typeof({2}), {3}, typeof({4}))",
                        ElementType, var, Generatable.QualifiedName, args,
                        _elementType == "gfilename*" ? "GLib.ListBase.FilenameString" : ElementType);
            }

            if (Generatable is IOwnable ownable)
                return ownable.FromNative(var, _owned);

            if (_isNullTermArray)
                return $"GLib.Marshaller.NullTermPtrToStringArray ({var}, {(_owned ? "true" : "false")})";

            return _isArray
                ? string.Format("({0}) GLib.Marshaller.ArrayPtrToArray ({1}, typeof ({2}), (int){3}native_{4}, true)",
                    CsType, var, Generatable.QualifiedName,
                    CountParameter.CsType == "int" ? string.Empty : $"({CountParameter.CsType})",
                    CountParameter.Name)
                : Generatable.FromNative(var);
        }

        public string ToNative(string var)
        {
            if (Generatable == null)
                return string.Empty;

            if (ElementType.Length > 0)
            {
                var args =
                    $", typeof ({ElementType}), {(_owned ? "true" : "false")}, {(_elementsOwned ? "true" : "false")}";

                var = $"new {Generatable.QualifiedName}({var}{args})";
            }
            else if (_isNullTermArray)
                return $"GLib.Marshaller.StringArrayToNullTermStrvPointer ({var})";
            else if (_isArray)
                return $"GLib.Marshaller.ArrayToArrayPtr ({var})";

            switch (Generatable)
            {
                case IManualMarshaler _:
                    return (Generatable as IManualMarshaler)?.AllocNative(var);
                case ObjectGen _ when _owned:
                    return $"{var} == null ? IntPtr.Zero : {var}.OwnedHandle";
                case OpaqueGen _ when _owned:
                    return $"{var} == null ? IntPtr.Zero : {var}.OwnedCopy";
                default:
                    return Generatable.CallByName(var);
            }
        }

        public bool Validate(LogWriter logWriter)
        {
            if (MarshalType == "" || CsType == "")
            {
                logWriter.Warn($"Unknown return type: {CType}.");
                return false;
            }

            if ((CsType == "GLib.List" || CsType == "GLib.SList") && string.IsNullOrEmpty(ElementType))
                logWriter.Warn($"Returns {CType} with unknown element type. Add element_type attribute with gapi-fixup.");

            if (!_isArray || _isNullTermArray || !string.IsNullOrEmpty(CountParameterName)) return true;
            logWriter.Warn("Returns an array with undeterminable length. Add null_term_array or array_length_param attribute with gapi-fixup.");
            
            return false;
        }
    }
}
