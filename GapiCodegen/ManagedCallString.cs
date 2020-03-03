// GtkSharp.Generation.ManagedCallString.cs - The ManagedCallString Class.
//
// Author: Mike Kestner <mkestner@speakeasy.net>
//
// Copyright (c) 2003 Mike Kestner
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
using System.Linq;
using System.Text;
using GapiCodegen.Generatables;
using GapiCodegen.Interfaces;

namespace GapiCodegen
{
    /// <summary>
    /// Represents a call to a managed method from a method that has unmanaged data.
    /// </summary>
    public class ManagedCallString
    {
        private readonly IDictionary<Parameter, bool> _paramDictionary = new Dictionary<Parameter, bool>();
        private readonly IList<Parameter> _disposeParams = new List<Parameter>();
        private readonly string _errorParam;
        private readonly string _userDataParam;
        private readonly string _destroyParam;

        public ManagedCallString(Parameters parameters)
        {
            for (var i = 0; i < parameters.Count; i++)
            {
                var parameter = parameters[i];

                if (parameter.IsLength && i > 0 && parameters[i - 1].IsString)
                    continue;

                if (parameter.Scope == "notified")
                {
                    _userDataParam = parameters[i + 1].Name;
                    _destroyParam = parameters[i + 2].Name;
                    i += 2;
                }
                else if ((parameter.IsCount || parameter.IsUserData) && parameters.IsHidden(parameter))
                {
                    _userDataParam = parameter.Name;
                    continue;
                }
                else if (parameter is ErrorParameter)
                {
                    _errorParam = parameter.Name;
                    continue;
                }

                var isSpecial =
                    parameter.PassAs != string.Empty && parameter.Name != parameter.FromNative(parameter.Name) ||
                    parameter.Generatable is CallbackGen;

                _paramDictionary.Add(parameter, isSpecial);

                if (parameter.IsOwnable)
                {
                    _disposeParams.Add(parameter);
                }
            }
        }

        public bool HasOutParam
        {
            get
            {
                return _paramDictionary.Keys.Any(parameter => parameter.PassAs == "out");
            }
        }

        public bool HasDisposeParam => _disposeParams.Count > 0;

        public string Unconditional(string indent)
        {
            var ret = new StringBuilder();

            if (_errorParam != null)
                ret.Append($"{indent}{_errorParam} = IntPtr.Zero;\n");

            foreach (var parameter in _disposeParams)
            {
                ret.Append($"{indent}{parameter.CsType} my{parameter.Name} = null;\n");
            }

            return ret.ToString();
        }

        public string Setup(string indent)
        {
            var ret = new StringBuilder();

            foreach (var parameter in _paramDictionary.Keys)
            {
                //Verify if parameter is special
                if (_paramDictionary[parameter] == false)
                {
                    continue;
                }

                var generatable = parameter.Generatable;

                if (generatable is CallbackGen callbackGen)
                {
                    string format;

                    if (_userDataParam == null)
                        format = string.Format("{0} {1}_invoker = new {0} ({1});\n", callbackGen.InvokerName,
                            parameter.Name);
                    else if (_destroyParam == null)
                        format = string.Format("{0} {1}_invoker = new {0} ({1}, {2});\n",
                            callbackGen.InvokerName, parameter.Name, _userDataParam);
                    else
                        format = string.Format("{0} {1}_invoker = new {0} ({1}, {2}, {3});\n",
                            callbackGen.InvokerName, parameter.Name, _userDataParam, _destroyParam);

                    ret.Append($"{indent}{format}");
                }
                else
                {
                    ret.Append($"{indent}{generatable.QualifiedName} my{parameter.Name}");

                    if (parameter.PassAs == "ref")
                        ret.Append($" = {parameter.FromNative(parameter.Name)}");

                    ret.Append(";\n");
                }
            }

            foreach (var p in _disposeParams)
            {
                ret.Append($"{indent}my{p.Name} = {p.FromNative(p.Name)};\n");
            }

            return ret.ToString();
        }

        public string Finish(string indent)
        {
            var ret = new StringBuilder();

            foreach (var parameter in _paramDictionary.Keys)
            {
                //Verify if parameter is special
                if (_paramDictionary[parameter] == false)
                {
                    continue;
                }

                var generatable = parameter.Generatable;
                string format;

                switch (generatable)
                {
                    case CallbackGen _:
                        continue;
                    case StructBase _:
                    case ByRefGen _:
                        format = string.Format(
                            "if ({0} != IntPtr.Zero) System.Runtime.InteropServices.Marshal.StructureToPtr (my{0}, {0}, false);\n",
                            parameter.Name);
                        break;

                    case IManualMarshaler marshaler:
                        format = $"{parameter.Name} = {marshaler.AllocNative($"my{parameter.Name}")};";
                        break;

                    default:
                        format = $"{parameter.Name} = {generatable.CallByName($"my{parameter.Name}")};\n";
                        break;
                }

                ret.Append($"{indent}{format}");
            }

            return ret.ToString();
        }

        public string DisposeParams(string indent)
        {
            var ret = new StringBuilder();

            foreach (var p in _disposeParams)
            {
                var name = $"my{p.Name}";
                var disposableName = $"disposable_{p.Name}";

                ret.Append($"{indent}var {disposableName} = {name} as IDisposable;\n");
                ret.Append($"{indent}if ({disposableName} != null)\n");
                ret.Append($"{indent}\t{disposableName}.Dispose ();\n");
            }

            return ret.ToString();
        }

        public override string ToString()
        {
            if (_paramDictionary.Count < 1)
                return string.Empty;

            var result = new string[_paramDictionary.Count];

            var i = 0;

            foreach (var parameter in _paramDictionary.Keys)
            {
                result[i] = parameter.PassAs == string.Empty
                    ? string.Empty
                    : $"{parameter.PassAs} ";

                if (parameter.Generatable is CallbackGen)
                {
                    result[i] += $"{parameter.Name}_invoker.Handler";
                }
                else
                {
                    if (_paramDictionary[parameter] || _disposeParams.Contains(parameter))
                    {
                        // Parameter was declared and marshalled earlier
                        result[i] += $"my{parameter.Name}";
                    }
                    else
                    {
                        result[i] += parameter.FromNative(parameter.Name);
                    }
                }

                i++;
            }

            return string.Join(", ", result);
        }
    }
}
