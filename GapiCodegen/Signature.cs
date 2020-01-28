// GtkSharp.Generation.Signature.cs - The Signature Generation Class.
//
// Author: Mike Kestner <mkestner@ximian.com>
//
// Copyright (c) 2003-2004 Novell, Inc.
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

namespace GapiCodegen
{
    /// <summary>
    /// Represents the signature of a method.
    /// </summary>
    public class Signature
    {
        private readonly IList<Parameter> _parameters = new List<Parameter>();

        public Signature(Parameters parameters)
        {
            foreach (var parameter in parameters)
            {
                if (!parameters.IsHidden(parameter))
                    _parameters.Add(parameter);
            }
        }

        public string Types
        {
            get
            {
                if (_parameters.Count == 0)
                    return string.Empty;

                var results = new string[_parameters.Count];
                var i = 0;

                foreach (var parameter in _parameters)
                    results[i++] = parameter.CsType;

                return string.Join(":", results);
            }
        }

        public bool IsAccessor
        {
            get
            {
                var count = 0;

                foreach (var parameter in _parameters)
                {
                    if (parameter.PassAs == "out")
                        count++;

                    if (count > 1)
                        return false;
                }

                return count == 1;
            }
        }

        public string AccessorType =>
            (from parameter in _parameters where parameter.PassAs == "out" select parameter.CsType).FirstOrDefault();

        public string AccessorName =>
            (from parameter in _parameters where parameter.PassAs == "out" select parameter.Name).FirstOrDefault();

        public string AsAccessor
        {
            get
            {
                var results = new string[_parameters.Count - 1];
                var i = 0;

                foreach (var parameter in _parameters)
                {
                    if (parameter.PassAs == "out")
                        continue;

                    results[i] = parameter.PassAs != string.Empty ? $"{parameter.PassAs} " : string.Empty;
                    results[i++] += $"{parameter.CsType} {parameter.Name}";
                }

                return string.Join(", ", results);
            }
        }

        public string WithoutOptional()
        {
            if (_parameters.Count == 0)
                return string.Empty;

            var results = new string[_parameters.Count];
            var i = 0;

            foreach (var parameter in _parameters)
            {
                if (parameter.IsOptional && parameter.PassAs == string.Empty)
                    continue;

                results[i] = parameter.PassAs != string.Empty ? $"{parameter.PassAs} " : string.Empty;
                results[i++] += $"{parameter.CsType} {parameter.Name}";
            }

            return string.Join(", ", results, 0, i);
        }

        public string CallWithoutOptionals()
        {
            if (_parameters.Count == 0)
                return string.Empty;

            var results = new string[_parameters.Count];
            var i = 0;

            foreach (var parameter in _parameters)
            {
                results[i] = parameter.PassAs != string.Empty ? $"{parameter.PassAs} " : string.Empty;

                if (parameter.IsOptional && parameter.PassAs == string.Empty)
                {
                    if (parameter.IsArray)
                        results[i++] += "null";
                    else
                        results[i++] += parameter.Generatable.DefaultValue;
                }
                else
                    results[i++] += parameter.Name;
            }

            return string.Join(", ", results);
        }

        public override string ToString()
        {
            if (_parameters.Count == 0)
                return string.Empty;

            var results = new string[_parameters.Count];
            var i = 0;

            foreach (var parameter in _parameters)
            {
                results[i] = parameter.PassAs != string.Empty ? $"{parameter.PassAs} " : string.Empty;
                results[i++] += $"{parameter.CsType} {parameter.Name}";
            }

            return string.Join(", ", results);
        }
    }
}
