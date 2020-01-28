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
        private const int DefaultParserVersion = 3;

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
            var document = Load(filename, schemaUri);

            if (document == null)
                return null;

            var root = document.DocumentElement;

            if (root == null || !root.HasChildNodes)
            {
                Console.WriteLine("No namespaces found.");
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

            if (parserVersion > DefaultParserVersion)
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
                        {
                            xmlPath = Path.Combine(gapiDir, element.GetAttribute("xml"));
                        }
                        else if (File.Exists(element.GetAttribute("xml")))
                        {
                            xmlPath = element.GetAttribute("xml");
                        }
                        else
                        {
                            Console.WriteLine($"Parser: Could not find include {element.GetAttribute("xml")}");
                            break;
                        }

                        var includedGeneratables = Parse(xmlPath);
                        SymbolTable.Table.AddTypes(includedGeneratables);

                        break;

                    case Constants.Namespace:
                        generatables.AddRange(ParseNamespace(element));
                        break;

                    case Constants.Symbol:
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
            var document = new XmlDocument();

            try
            {
                var settings = new XmlReaderSettings();

                if (!string.IsNullOrEmpty(schemaUri))
                {
                    settings.Schemas.Add(null, schemaUri);
                    settings.ValidationType = ValidationType.Schema;
                    settings.ValidationFlags |= XmlSchemaValidationFlags.ReportValidationWarnings;
                    settings.ValidationEventHandler += (sender, args) =>
                    {
                        switch (args.Severity)
                        {
                            case XmlSeverityType.Error:
                                Console.WriteLine($"Error: {args.Message}");
                                break;
                            case XmlSeverityType.Warning:
                                Console.WriteLine($"Warning: {args.Message}");
                                break;
                            default:
                                throw new ArgumentOutOfRangeException();
                        }
                    };
                }

                Stream stream = File.OpenRead(filename);
                var reader = XmlReader.Create(stream, settings);
                document.Load(reader);

                stream.Close();
            }
            catch (XmlException e)
            {
                Console.WriteLine("Invalid XML file.");
                Console.WriteLine(e);
                document = null;
            }

            return document;
        }

        private static IEnumerable<IGeneratable> ParseNamespace(XmlElement namespaceElement)
        {
            var results = new List<IGeneratable>();

            foreach (XmlElement element in namespaceElement.ChildNodes)
            {
                if (element == null)
                    continue;

                if (element.GetAttributeAsBoolean(Constants.Hidden))
                    continue;

                var isOpaque = element.GetAttributeAsBoolean(Constants.Opaque);

                switch (element.Name)
                {
                    case Constants.Alias:
                        {
                            var cName = element.GetAttribute(Constants.CName);
                            var type = element.GetAttribute(Constants.Type);

                            if (cName == "" || type == "") continue;

                            results.Add(new AliasGen(cName, type));
                            break;
                        }

                    case Constants.Boxed:
                        {
                            if (isOpaque)
                            {
                                results.Add(new OpaqueGen(namespaceElement, element));
                            }
                            else
                            {
                                results.Add(new BoxedGen(namespaceElement, element));
                            }

                            break;
                        }

                    case Constants.Callback:
                        results.Add(new CallbackGen(namespaceElement, element));
                        break;

                    case Constants.Enumeration:
                        results.Add(new EnumGen(namespaceElement, element));
                        break;

                    case Constants.Interface:
                        results.Add(new InterfaceGen(namespaceElement, element));
                        break;
                    case Constants.Object:
                        results.Add(new ObjectGen(namespaceElement, element));
                        break;

                    case Constants.Class:
                        results.Add(new ClassGen(namespaceElement, element));
                        break;

                    case "union":
                        results.Add(new UnionGen(namespaceElement, element));
                        break;

                    case Constants.Struct:
                        {
                            var isNativeStruct = element.GetAttributeAsBoolean("native");

                            if (isOpaque)
                            {
                                results.Add(new OpaqueGen(namespaceElement, element));
                            }
                            else if (isNativeStruct)
                            {
                                results.Add(new NativeStructGen(namespaceElement, element));
                            }
                            else
                            {
                                results.Add(new StructGen(namespaceElement, element));
                            }

                            break;
                        }

                    default:
                        Console.WriteLine($"Parser::ParseNamespace - Unexpected node: {element.Name}");
                        break;
                }
            }

            return results;
        }

        private static IGeneratable ParseSymbol(XmlElement symbol)
        {
            var type = symbol.GetAttribute(Constants.Type);
            var cName = symbol.GetAttribute(Constants.CName);
            var name = symbol.GetAttribute(Constants.Name);

            IGeneratable result = null;

            switch (type)
            {
                case Constants.Simple when symbol.HasAttribute(Constants.DefaultValue):
                    result = new SimpleGen(cName, name, symbol.GetAttribute(Constants.DefaultValue));
                    break;

                case Constants.Simple:
                    Console.WriteLine($"Simple type element {cName} has no specified default value");
                    result = new SimpleGen(cName, name, string.Empty);
                    break;

                case Constants.Manual:
                    result = new ManualGen(cName, name);
                    break;

                case "ownable":
                    result = new OwnableGen(cName, name);
                    break;

                case Constants.Alias:
                    result = new AliasGen(cName, name);
                    break;

                case Constants.Marshal:
                    var marshalType = symbol.GetAttribute(Constants.MarshalType);
                    var call = symbol.GetAttribute(Constants.CallFmt);
                    var from = symbol.GetAttribute(Constants.FromFmt);

                    result = new MarshalGen(cName, name, marshalType, call, from);
                    break;

                case Constants.Struct:
                    result = new ByRefGen(symbol.GetAttribute(Constants.CName), symbol.GetAttribute(Constants.Name));
                    break;

                default:
                    Console.WriteLine($"Parser::ParseSymbol - Unexpected symbol type {type}");
                    break;
            }

            return result;
        }
    }
}
