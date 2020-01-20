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

using System.Xml;
using GapiCodegen.Generatables;
using GapiCodegen.Interfaces;
using GapiCodegen.Utils;

namespace GapiCodegen
{
    public class Parameter
    {
        private readonly XmlElement _element;

        public Parameter(XmlElement element)
        {
            _element = element;
        }

        private string _callName;

        public string CallName
        {
            get => _callName ?? Name;
            set => _callName = value;
        }

        public string CType
        {
            get
            {
                var type = _element.GetAttribute("type");

                if (type == "void*")
                    type = "gpointer";

                return type;
            }
        }

        public string CsType
        {
            get
            {
                var csType = SymbolTable.Table.GetCsType(_element.GetAttribute("type"));

                if (csType == "void")
                    csType = "System.IntPtr";

                if (!IsArray) return csType;

                if (IsParams)
                    csType = $"params {csType}";

                csType += "[]";
                csType = csType.Replace("ref ", "");

                return csType;
            }
        }

        public IGeneratable Generatable => SymbolTable.Table[CType];

        public bool IsArray => _element.GetAttributeAsBoolean("array") || _element.GetAttributeAsBoolean("null_term_array");

        public bool IsEllipsis => _element.GetAttributeAsBoolean("ellipsis");

        internal bool IsOptional => _element.GetAttributeAsBoolean("allow-none");

        private bool _isCount;
        private bool _isCountSet;

        public bool IsCount
        {
            get
            {
                if (_isCountSet)
                    return _isCount;

                if (!Name.StartsWith("n_")) return false;

                switch (CsType)
                {
                    case "int":
                    case "uint":
                    case "long":
                    case "ulong":
                    case "short":
                    case "ushort":
                        return true;
                    default:
                        return false;
                }
            }
            set
            {
                _isCountSet = true;
                _isCount = value;
            }
        }

        public bool IsDestroyNotify => CType == "GDestroyNotify";

        public bool IsLength
        {
            get
            {
                if (!Name.EndsWith("len") && !Name.EndsWith("length")) return false;

                switch (CsType)
                {
                    case "int":
                    case "uint":
                    case "long":
                    case "ulong":
                    case "short":
                    case "ushort":
                        return true;
                    default:
                        return false;
                }
            }
        }

        public bool IsParams => _element.HasAttribute("params");

        public bool IsString => CsType == "string";

        public bool IsUserData => CsType == "IntPtr" && (Name.EndsWith("data") || Name.EndsWith("data_or_owner"));

        public virtual string MarshalType
        {
            get
            {
                var type = SymbolTable.Table.GetMarshalType(_element.GetAttribute("type"));

                if (type == "void" || Generatable is IManualMarshaler)
                    type = "IntPtr";

                if (!IsArray) return type;

                type += "[]";
                type = type.Replace("ref ", "");

                return type;
            }
        }

        public string Name => SymbolTable.Table.MangleName(_element.GetAttribute("name"));

        public bool IsOwnable => Generatable is OwnableGen;

        public bool Owned => _element.GetAttribute("owned") == "true";

        public virtual string NativeSignature
        {
            get
            {
                var signature = $"{MarshalType} {Name}";

                if (!string.IsNullOrEmpty(PassAs))
                    signature = $"{PassAs} {signature}";

                return signature;
            }
        }

        public string PropertyName => _element.GetAttribute("property_name");

        private string _passAs;

        public string PassAs
        {
            get
            {
                if (!string.IsNullOrEmpty(_passAs))
                    return _passAs;

                if (_element.HasAttribute("pass_as"))
                    return _element.GetAttribute("pass_as");

                if (IsArray || CsType.EndsWith("IntPtr"))
                    return "";

                if (CType.EndsWith("*") && (Generatable is SimpleGen || Generatable is EnumGen))
                    return "out";

                return "";
            }
            set => _passAs = value;
        }

        private string _scope;

        public string Scope
        {
            get => _scope ?? (_scope = _element.GetAttribute("scope"));
            set => _scope = value;
        }

        private int _closure = -1;

        public int Closure
        {
            get
            {
                if (_closure == -1 && _element.HasAttribute("closure"))
                {
                    _closure = int.Parse(_element.GetAttribute("closure"));
                }

                return _closure;
            }
            set => _closure = value;
        }

        private int _destroyNotify = -1;

        public int DestroyNotify
        {
            get
            {
                if (_destroyNotify == -1 && _element.HasAttribute("destroy"))
                {
                    _destroyNotify = int.Parse(_element.GetAttribute("destroy"));
                }

                return _destroyNotify;
            }
            set => _destroyNotify = value;
        }

        public bool IsHidden => _element.GetAttributeAsBoolean("hidden");

        public virtual string[] Prepare
        {
            get
            {
                if (Generatable is IManualMarshaler marshaler)
                {
                    var result = $"IntPtr native_{CallName}";

                    if (PassAs != "out")
                        result += $" = {marshaler.AllocNative(CallName)}";

                    return new[] { $"{result};" };
                }

                switch (PassAs)
                {
                    case "out" when CsType != MarshalType:
                        return new[] { $"{Generatable.MarshalType} native_{CallName};" };
                    case "ref" when CsType != MarshalType:
                        return new[] { $"{Generatable.MarshalType} native_{CallName} = ({Generatable.MarshalType}) {CallName};" };
                    default:
                        {
                            if (Generatable is OpaqueGen && Owned)
                            {
                                return new[] { $"{CallName}.Owned = false;" };
                            }

                            break;
                        }
                }

                return new string[0];
            }
        }

        public virtual string CallString
        {
            get
            {
                if (Generatable is CallbackGen)
                    return SymbolTable.Table.CallByName(CType, $"{CallName}_wrapper");

                string callParam;

                if (!string.IsNullOrEmpty(PassAs))
                {
                    callParam = $"{PassAs} ";

                    if (CsType != MarshalType)
                        callParam += "native_";

                    callParam += CallName;
                }
                else switch (Generatable)
                    {
                        case IManualMarshaler _:
                            callParam = $"native_{CallName}";
                            break;
                        case ObjectBase objectBase:
                            callParam = objectBase.CallByName(CallName, Owned);
                            break;
                        default:
                            callParam = Generatable.CallByName(CallName);
                            break;
                    }

                return callParam;
            }
        }

        public virtual string[] Finish
        {
            get
            {
                if (Generatable is IManualMarshaler marshaler)
                {
                    var result = new string[PassAs == "ref" ? 2 : 1];
                    var i = 0;

                    if (!string.IsNullOrEmpty(PassAs))
                    {
                        result[i++] = $"{CallName} = {Generatable.FromNative($"native_{CallName}")};";
                    }

                    if (PassAs != "out")
                        result[i] = $"{marshaler.ReleaseNative($"native_{CallName}")};";

                    return result;
                }

                if (string.IsNullOrEmpty(PassAs) || MarshalType == CsType) return new string[0];

                if (Generatable is IOwnable ownable)
                {
                    return new[] { $"{CallName} = {ownable.FromNative($"native_{CallName}", Owned)};" };
                }

                return new[] { $"{CallName} = {Generatable.FromNative($"native_{CallName}")};" };
            }
        }

        public string FromNative(string var)
        {
            switch (Generatable)
            {
                case null:
                    return string.Empty;
                case IOwnable ownable:
                    return ownable.FromNative(var, Owned);
                default:
                    return Generatable.FromNative(var);
            }
        }

        public string StudlyName
        {
            get
            {
                var name = _element.GetAttribute("name");
                var segments = name.Split('_');
                var studly = "";

                foreach (var segment in segments)
                {
                    if (segment.Trim() == "")
                        continue;

                    studly += segment.Substring(0, 1).ToUpper() + segment.Substring(1);
                }

                return studly;
            }
        }
    }

    public class ErrorParameter : Parameter
    {
        public ErrorParameter(XmlElement element) : base(element)
        {
            PassAs = "out";
        }

        public override string CallString => "out error";
    }

    public class StructParameter : Parameter
    {
        public StructParameter(XmlElement element) : base(element) { }

        public override string MarshalType => "IntPtr";

        public override string[] Prepare
        {
            get
            {
                return PassAs == "out"
                    ? new[]
                    {
                        $"IntPtr native_{CallName} = Marshal.AllocHGlobal (Marshal.SizeOf (typeof ({Generatable.QualifiedName})));"
                    }
                    : new[]
                    {
                        $"IntPtr native_{CallName} = {(Generatable as IManualMarshaler)?.AllocNative(CallName)};"
                    };
            }
        }

        public override string CallString => $"native_{CallName}";

        public override string[] Finish
        {
            get
            {
                var result = new string[PassAs == string.Empty ? 1 : 2];
                var i = 0;

                if (PassAs != string.Empty)
                {
                    result[i++] = $"{CallName} = {FromNative($"native_{CallName}")};";
                }

                result[i] = $"{(Generatable as IManualMarshaler)?.ReleaseNative($"native_{CallName}")};";
                return result;
            }
        }

        public override string NativeSignature => $"IntPtr {CallName}";
    }
}
