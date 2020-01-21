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
using GapiCodegen.Interfaces;
using GapiCodegen.Utils;

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

        public IGeneratable[] Parse(string filename, string schemaUri, string gapiDir)
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

            var generatables = new List<IGeneratable>();

            foreach (XmlElement element in root.ChildNodes)
            {
                if (element == null)
                    continue;

                switch (element.Name)
                {
                    case "include":
                        string xmlPath;

                        if (File.Exists(Path.Combine(gapiDir, element.GetAttribute("xml"))))
                            xmlPath = Path.Combine(gapiDir, element.GetAttribute("xml"));
                        else if (File.Exists(element.GetAttribute("xml")))
                            xmlPath = element.GetAttribute("xml");
                        else
                        {
                            Console.WriteLine($"Parser: Could not find include {element.GetAttribute("xml")}");
                            break;
                        }

                        var includedGeneratables = Parse(xmlPath);
                        SymbolTable.Table.AddTypes(includedGeneratables);

                        break;

                    case "namespace":
                        generatables.AddRange(ParseNamespace(element));
                        break;

                    case "symbol":
                        generatables.Add(ParseSymbol(element));
                        break;

                    default:
                        Console.WriteLine($"Parser::Parse - Unexpected child node: {element.Name}");
                        break;
                }
            }

            return generatables.ToArray();
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
                    Console.WriteLine($"Error: {e.Message}");
                    break;
                case XmlSeverityType.Warning:
                    Console.WriteLine($"Warning: {e.Message}");
                    break;
                default:
                    throw new ArgumentOutOfRangeException();
            }
        }

        private static IEnumerable<IGeneratable> ParseNamespace(XmlElement namespaceElement)
        {
            var result = new List<IGeneratable>();

            foreach (XmlElement elem in namespaceElement.ChildNodes)
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
                                result.Add(new OpaqueGen(namespaceElement, elem));
                            }
                            else
                            {
                                result.Add(new BoxedGen(namespaceElement, elem));
                            }

                            break;
                        }

                    case "callback":
                        result.Add(new CallbackGen(namespaceElement, elem));
                        break;

                    case "enum":
                        result.Add(new EnumGen(namespaceElement, elem));
                        break;

                    case "interface":
                        result.Add(new InterfaceGen(namespaceElement, elem));
                        break;
                    case "object":
                        result.Add(new ObjectGen(namespaceElement, elem));
                        break;

                    case "class":
                        result.Add(new ClassGen(namespaceElement, elem));
                        break;

                    case "union":
                        result.Add(new UnionGen(namespaceElement, elem));
                        break;

                    case "struct":
                        {
                            var isNativeStruct = elem.GetAttributeAsBoolean("native");

                            if (isOpaque)
                            {
                                result.Add(new OpaqueGen(namespaceElement, elem));
                            }
                            else if (isNativeStruct)
                            {
                                result.Add(new NativeStructGen(namespaceElement, elem));
                            }
                            else
                            {
                                result.Add(new StructGen(namespaceElement, elem));
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
            var cName = symbol.GetAttribute("cname");
            var name = symbol.GetAttribute("name");

            IGeneratable result = null;

            switch (type)
            {
                case "simple" when symbol.HasAttribute("default_value"):
                    result = new SimpleGen(cName, name, symbol.GetAttribute("default_value"));
                    break;

                case "simple":
                    Console.WriteLine($"Simple type element {cName} has no specified default value");
                    result = new SimpleGen(cName, name, string.Empty);
                    break;

                case "manual":
                    result = new ManualGen(cName, name);
                    break;

                case "ownable":
                    result = new OwnableGen(cName, name);
                    break;

                case "alias":
                    result = new AliasGen(cName, name);
                    break;

                case "marshal":
                    var mtype = symbol.GetAttribute("marshal_type");
                    var call = symbol.GetAttribute("call_fmt");
                    var from = symbol.GetAttribute("from_fmt");

                    result = new MarshalGen(cName, name, mtype, call, @from);
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
