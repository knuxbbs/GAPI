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

using System.Collections.Generic;
using System.Xml;
using GapiCodegen.Generatables;
using GapiCodegen.Interfaces;
using GapiCodegen.Util;

namespace GapiCodegen
{
    public class ArrayParameter : Parameter
    {
        public ArrayParameter(XmlElement element) : base(element)
        {
            NullTerminated = element.GetAttributeAsBoolean("null_term_array");

            if (element.HasAttribute("array_len"))
                FixedArrayLength = int.Parse(element.GetAttribute("array_len"));
        }

        public int? FixedArrayLength { get; }

        public override string MarshalType => Generatable is StructBase ? CsType : base.MarshalType;

        public override string[] Prepare
        {
            get
            {
                if (CsType == MarshalType && !FixedArrayLength.HasValue)
                    return new string[0];

                var result = new List<string>();

                if (FixedArrayLength.HasValue)
                {
                    result.Add($"{Name} = new {MarshalType.TrimEnd('[', ']')}[{FixedArrayLength}];");
                    return result.ToArray();
                }

                result.Add($"int cnt_{CallName} = {CallName} == null ? 0 : {CallName}.Length;");
                result.Add(string.Format("{0}[] native_{1} = new {0} [cnt_{1}{2}];",
                    MarshalType.TrimEnd('[', ']'), CallName, NullTerminated ? " + 1" : ""));
                result.Add($"for (int i = 0; i < cnt_{CallName}; i++)");

                if (Generatable is IManualMarshaler marshaler)
                    result.Add($"\tnative_{CallName} [i] = {marshaler.AllocNative(CallName + "[i]")};");
                else
                    result.Add($"\tnative_{CallName} [i] = {Generatable.CallByName(CallName + "[i]")};");

                if (NullTerminated)
                    result.Add($"native_{CallName} [cnt_{CallName}] = IntPtr.Zero;");

                return result.ToArray();
            }
        }

        public override string CallString
        {
            get
            {
                if (CsType != MarshalType)
                    return $"native_{CallName}";

                return FixedArrayLength.HasValue ? base.CallString : CallName;
            }
        }

        public override string[] Finish
        {
            get
            {
                if (CsType == MarshalType)
                    return new string[0];

                if (!(Generatable is IManualMarshaler marshaler)) return new string[0];

                var result = new string[4];

                result[0] = $"for (int i = 0; i < native_{CallName}.Length{(NullTerminated ? " - 1" : "")}; i++) {{";
                result[1] = $"\t{CallName} [i] = {Generatable.FromNative("native_" + CallName + "[i]")};";
                result[2] = $"\t{marshaler.ReleaseNative("native_" + CallName + "[i]")};";
                result[3] = "}";

                return result;
            }
        }

        private bool NullTerminated { get; }
    }

    public class ArrayCountPair : ArrayParameter
    {
        private readonly XmlElement _countElement;
        private readonly bool _invert;

        public ArrayCountPair(XmlElement arrayElement, XmlElement countElement, bool invert) : base(arrayElement)
        {
            _countElement = countElement;
            _invert = invert;
        }

        public override string CallString => _invert
            ? $"{CallCount(CallName)}, {base.CallString}"
            : $"{base.CallString}, {CallCount(CallName)}";

        public override string NativeSignature => _invert
            ? $"{CountNativeType} {CountName}, {MarshalType} {Name}"
            : $"{MarshalType} {Name}, {CountNativeType} {CountName}";

        private string CountNativeType => SymbolTable.Table.GetMarshalType(_countElement.GetAttribute("type"));

        private string CountType => SymbolTable.Table.GetCsType(_countElement.GetAttribute("type"));

        private string CountCast => CountType == "int" ? string.Empty : $"({CountType}) ";

        private string CountName => SymbolTable.Table.MangleName(_countElement.GetAttribute("name"));

        private string CallCount(string name)
        {
            var result = $"{CountCast}({name} == null ? 0 : {name}.Length)";
            var generatable = SymbolTable.Table[_countElement.GetAttribute("type")];
            return generatable.CallByName(result);
        }
    }
}
