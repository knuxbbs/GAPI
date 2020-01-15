// GtkSharp.Generation.Parser.cs - The XML Parsing engine.
//
// Author: Mike Kestner <mkestner@speakeasy.net>
//
// Copyright (c) 2001-2003 Mike Kestner
// Copyright (c) 2003 Ximian Inc.
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
using System.Collections.Generic;
using System.IO;
using System.Xml;
using System.Xml.Schema;
using GapiCodegen.Generatables;

namespace GapiCodegen
{
    /// <summary>
    /// The XML Parsing engine.
    /// </summary>
    public class Parser
    {
        private const int CurrentParserVersion = 3;

        public IGeneratable[] Parse(string filename)
        {
            return Parse(filename, null);
        }

        public IGeneratable[] Parse(string filename, string schemaUri)
        {
            return Parse(filename, schemaUri, string.Empty);
        }

        public IGeneratable[] Parse(string filename, string schemaUri, string gapidir)
        {
            var doc = Load(filename, schemaUri);

            if (doc == null)
                return null;

            var root = doc.DocumentElement;

            if (root == null || !root.HasChildNodes)
            {
                Console.WriteLine("No Namespaces found.");
                return null;
            }

            int parserVersion;

            if (root.HasAttribute("parser_version"))
            {
                try
                {
                    parserVersion = int.Parse(root.GetAttribute("parser_version"));
                }
                catch
                {
                    Console.WriteLine(
                        "ERROR: Unable to parse parser_version attribute value \"{0}\" to a number. Input file {1} will be ignored",
                        root.GetAttribute("parser_version"), filename);

                    return null;
                }
            }
            else
                parserVersion = 1;

            if (parserVersion > CurrentParserVersion)
                Console.WriteLine(
                    "WARNING: The input file {0} was created by a parser that was released after this version of the generator. Consider updating the code generator if you experience problems.",
                    filename);

            var gens = new List<IGeneratable>();

            foreach (XmlElement elem in root.ChildNodes)
            {
                if (elem == null)
                    continue;

                switch (elem.Name)
                {
                    case "include":
                        string xmlpath;

                        if (File.Exists(Path.Combine(gapidir, elem.GetAttribute("xml"))))
                            xmlpath = Path.Combine(gapidir, elem.GetAttribute("xml"));
                        else if (File.Exists(elem.GetAttribute("xml")))
                            xmlpath = elem.GetAttribute("xml");
                        else
                        {
                            Console.WriteLine($"Parser: Could not find include {elem.GetAttribute("xml")}");
                            break;
                        }

                        IGeneratable[] curr_gens = Parse(xmlpath);
                        SymbolTable.Table.AddTypes(curr_gens);
                        break;

                    case "namespace":
                        gens.AddRange(ParseNamespace(elem));
                        break;

                    case "symbol":
                        gens.Add(ParseSymbol(elem));
                        break;

                    default:
                        Console.WriteLine($"Parser::Parse - Unexpected child node: {elem.Name}");
                        break;
                }
            }

            return gens.ToArray();
        }

        internal static int GetVersion(XmlElement xmlElement)
        {
            return xmlElement.HasAttribute("parser_version")
                ? int.Parse(xmlElement.GetAttribute("parser_version"))
                : 1;
        }

        private static XmlDocument Load(string filename, string schemaUri)
        {
            var doc = new XmlDocument();

            try
            {
                var settings = new XmlReaderSettings();

                if (!string.IsNullOrEmpty(schemaUri))
                {
                    settings.Schemas.Add(null, schemaUri);
                    settings.ValidationType = ValidationType.Schema;
                    settings.ValidationFlags |= XmlSchemaValidationFlags.ReportValidationWarnings;
                    settings.ValidationEventHandler += ValidationEventHandler;
                }

                Stream stream = File.OpenRead(filename);
                var reader = XmlReader.Create(stream, settings);
                doc.Load(reader);

                stream.Close();
            }
            catch (XmlException e)
            {
                Console.WriteLine("Invalid XML file.");
                Console.WriteLine(e);
                doc = null;
            }

            return doc;
        }

        private static void ValidationEventHandler(object sender, ValidationEventArgs e)
        {
            switch (e.Severity)
            {
                case XmlSeverityType.Error:
                    Console.WriteLine("Error: {0}", e.Message);
                    break;
                case XmlSeverityType.Warning:
                    Console.WriteLine("Warning: {0}", e.Message);
                    break;
                default:
                    throw new ArgumentOutOfRangeException();
            }
        }

        private static IEnumerable<IGeneratable> ParseNamespace(XmlElement ns)
        {
            var result = new List<IGeneratable>();

            foreach (XmlElement elem in ns.ChildNodes)
            {
                if (elem == null)
                    continue;

                if (elem.GetAttributeAsBoolean("hidden"))
                    continue;

                var isOpaque = elem.GetAttributeAsBoolean("opaque");

                switch (elem.Name)
                {
                    case "alias":
                        {
                            var aname = elem.GetAttribute("cname");
                            var atype = elem.GetAttribute("type");

                            if (aname == "" || atype == "") continue;

                            result.Add(new AliasGen(aname, atype));
                            break;
                        }

                    case "boxed":
                        {
                            if (isOpaque)
                            {
                                result.Add(new OpaqueGen(ns, elem));
                            }
                            else
                            {
                                result.Add(new BoxedGen(ns, elem));
                            }

                            break;
                        }

                    case "callback":
                        result.Add(new CallbackGen(ns, elem));
                        break;

                    case "enum":
                        result.Add(new EnumGen(ns, elem));
                        break;

                    case "interface":
                        result.Add(new InterfaceGen(ns, elem));
                        break;
                    case "object":
                        result.Add(new ObjectGen(ns, elem));
                        break;

                    case "class":
                        result.Add(new ClassGen(ns, elem));
                        break;

                    case "union":
                        result.Add(new UnionGen(ns, elem));
                        break;

                    case "struct":
                        {
                            var isNativeStruct = elem.GetAttributeAsBoolean("native");

                            if (isOpaque)
                            {
                                result.Add(new OpaqueGen(ns, elem));
                            }
                            else if (isNativeStruct)
                            {
                                result.Add(new NativeStructGen(ns, elem));
                            }
                            else
                            {
                                result.Add(new StructGen(ns, elem));
                            }

                            break;
                        }

                    default:
                        Console.WriteLine($"Parser::ParseNamespace - Unexpected node: {elem.Name}");
                        break;
                }
            }

            return result;
        }

        private static IGeneratable ParseSymbol(XmlElement symbol)
        {
            var type = symbol.GetAttribute("type");
            var cname = symbol.GetAttribute("cname");
            var name = symbol.GetAttribute("name");

            IGeneratable result = null;

            switch (type)
            {
                case "simple" when symbol.HasAttribute("default_value"):
                    result = new SimpleGen(cname, name, symbol.GetAttribute("default_value"));
                    break;

                case "simple":
                    Console.WriteLine($"Simple type element {cname} has no specified default value");
                    result = new SimpleGen(cname, name, string.Empty);
                    break;

                case "manual":
                    result = new ManualGen(cname, name);
                    break;

                case "ownable":
                    result = new OwnableGen(cname, name);
                    break;

                case "alias":
                    result = new AliasGen(cname, name);
                    break;

                case "marshal":
                    var mtype = symbol.GetAttribute("marshal_type");
                    var call = symbol.GetAttribute("call_fmt");
                    var from = symbol.GetAttribute("from_fmt");

                    result = new MarshalGen(cname, name, mtype, call, @from);
                    break;

                case "struct":
                    result = new ByRefGen(symbol.GetAttribute("cname"), symbol.GetAttribute("name"));
                    break;

                default:
                    Console.WriteLine($"Parser::ParseSymbol - Unexpected symbol type {type}");
                    break;
            }

            return result;
        }
    }
}