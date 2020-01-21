// Authors:
//   Stephan Sundermann <stephansundermann@gmail.com>
//
// Copyright (c) 2013 Stephan Sundermann
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
using GapiCodegen.Utils;

namespace GapiCodegen
{
    public class Constant
    {
        private readonly string _value;
        private readonly string _ctype;

        public Constant(XmlElement elem)
        {
            Name = elem.GetAttribute(Constants.Name);
            _value = elem.GetAttribute(Constants.Value);
            _ctype = elem.GetAttribute("ctype");
        }

        public string Name { get; }

        public string ConstType
        {
            get
            {
                if (IsString)
                    return "string";

                // gir registers all integer values as gint even for numbers which do not fit into a gint
                // if the number is too big for an int, try to fit it into a long
                if (SymbolTable.Table.GetMarshalType(_ctype) == "int" && _value.Length < 20 && long.Parse(_value) > int.MaxValue)
                    return "long";

                return SymbolTable.Table.GetMarshalType(_ctype);
            }
        }

        public bool IsString => SymbolTable.Table.GetCsType(_ctype) == "string";

        public virtual bool Validate(LogWriter log)
        {
            if (ConstType != string.Empty)
            {
                return SymbolTable.Table.GetMarshalType(_ctype) != "int" || _value.Length < 20;
            }

            log.Warn($"{Name} type is missing or wrong.");

            return false;
        }

        public virtual void Generate(GenerationInfo generationInfo, string indent)
        {
            var streamWriter = generationInfo.Writer;

            streamWriter.WriteLine("{0}public const {1} {2} = {3}{4}{5};",
                indent,
                ConstType,
                Name,
                IsString ? "@\"" : string.Empty,
                _value,
                IsString ? "\"" : string.Empty);
        }
    }
}
