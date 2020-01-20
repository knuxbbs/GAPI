// gapi-fixup.cs - xml alteration engine.
//
// Authors:
//   Mike Kestner <mkestner@speakeasy.net>
//   Stephan Sundermann <stephansundermann@gmail.com>
//
// Copyright (c) 2003 Mike Kestner
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

using System;
using System.IO;
using System.Xml;
using System.Xml.XPath;

namespace GapiFixup
{
    public class Program
    {
        public static int Main(string[] args)
        {
            if (args.Length < 2)
            {
                Console.WriteLine("Usage: gapi-fixup --metadata=<filename> --api=<filename> --symbols=<filename>");
                return 0;
            }

            var apiFilename = string.Empty;
            var apiDoc = new XmlDocument();
            var metaDoc = new XmlDocument();
            var symbolDoc = new XmlDocument();

            foreach (var arg in args)
            {
                if (arg.StartsWith("--metadata="))
                {
                    var metaFilename = arg.Substring("--metadata=".Length);

                    try
                    {
                        Stream stream = File.OpenRead(metaFilename);
                        metaDoc.Load(stream);
                        stream.Close();
                    }
                    catch (XmlException e)
                    {
                        Console.WriteLine("Invalid meta file.");
                        Console.WriteLine(e);
                        return 1;
                    }
                }
                else if (arg.StartsWith("--api="))
                {
                    apiFilename = arg.Substring("--api=".Length);

                    try
                    {
                        Stream stream = File.OpenRead(apiFilename);
                        apiDoc.Load(stream);
                        stream.Close();
                    }
                    catch (XmlException e)
                    {
                        Console.WriteLine("Invalid api file.");
                        Console.WriteLine(e);
                        return 1;
                    }
                }
                else if (arg.StartsWith("--symbols="))
                {
                    var symbolFilename = arg.Substring("--symbols=".Length);

                    try
                    {
                        Stream stream = File.OpenRead(symbolFilename);
                        symbolDoc.Load(stream);
                        stream.Close();
                    }
                    catch (XmlException e)
                    {
                        Console.WriteLine("Invalid api file.");
                        Console.WriteLine(e);
                        return 1;
                    }
                }
                else
                {
                    Console.WriteLine("Usage: gapi-fixup --metadata=<filename> --api=<filename>");
                    return 1;
                }
            }

            XPathNavigator metaNavigator = metaDoc.CreateNavigator();
            XPathNavigator apiNavigator = apiDoc.CreateNavigator();

            XPathNodeIterator copyNodeIterator = metaNavigator.Select("/metadata/copy-node");

            while (copyNodeIterator.MoveNext())
            {
                var path = copyNodeIterator.Current.GetAttribute("path", string.Empty);
                XPathExpression expr = apiNavigator.Compile(path);
                var parent = copyNodeIterator.Current.Value;
                XPathNodeIterator parent_iter = apiNavigator.Select(parent);
                var matched = false;

                while (parent_iter.MoveNext())
                {
                    XmlNode parent_node = ((IHasXmlNode)parent_iter.Current).GetNode();
                    XPathNodeIterator path_iter = parent_iter.Current.Clone().Select(expr);

                    while (path_iter.MoveNext())
                    {
                        XmlNode node = ((IHasXmlNode)path_iter.Current).GetNode();
                        parent_node.AppendChild(node.Clone());
                    }

                    matched = true;
                }

                if (!matched)
                    Console.WriteLine($"Warning: <copy-node path=\"{path}\"/> matched no nodes");
            }

            XPathNodeIterator removeNodeIterator = metaNavigator.Select("/metadata/remove-node");

            while (removeNodeIterator.MoveNext())
            {
                var path = removeNodeIterator.Current.GetAttribute("path", "");
                XPathNodeIterator api_iter = apiNavigator.Select(path);
                var matched = false;

                while (api_iter.MoveNext())
                {
                    var api_node = ((IHasXmlNode)api_iter.Current).GetNode() as XmlElement;
                    api_node.ParentNode.RemoveChild(api_node);
                    matched = true;
                }

                if (!matched)
                    Console.WriteLine($"Warning: <remove-node path=\"{path}\"/> matched no nodes");
            }

            XPathNodeIterator addNodeIterator = metaNavigator.Select("/metadata/add-node");

            while (addNodeIterator.MoveNext())
            {
                var path = addNodeIterator.Current.GetAttribute("path", "");
                XPathNodeIterator api_iter = apiNavigator.Select(path);
                var matched = false;

                while (api_iter.MoveNext())
                {
                    var api_node = ((IHasXmlNode)api_iter.Current).GetNode() as XmlElement;

                    foreach (XmlNode child in ((IHasXmlNode)addNodeIterator.Current).GetNode().ChildNodes)
                        api_node.AppendChild(apiDoc.ImportNode(child, true));

                    matched = true;
                }

                if (!matched)
                    Console.WriteLine($"Warning: <add-node path=\"{path}\"/> matched no nodes");
            }

            XPathNodeIterator changeNodeTypeIterator = metaNavigator.Select("/metadata/change-node-type");

            while (changeNodeTypeIterator.MoveNext())
            {
                var path = changeNodeTypeIterator.Current.GetAttribute("path", "");
                XPathNodeIterator api_iter = apiNavigator.Select(path);
                var matched = false;

                while (api_iter.MoveNext())
                {
                    var node = ((IHasXmlNode)api_iter.Current).GetNode() as XmlElement;
                    var parent = node.ParentNode as XmlElement;
                    XmlElement new_node = apiDoc.CreateElement(changeNodeTypeIterator.Current.Value);

                    foreach (XmlNode child in node.ChildNodes)
                        new_node.AppendChild(child.Clone());

                    foreach (XmlAttribute attribute in node.Attributes)
                        new_node.Attributes.Append((XmlAttribute)attribute.Clone());

                    parent.ReplaceChild(new_node, node);
                    matched = true;
                }

                if (!matched)
                    Console.WriteLine($"Warning: <change-node-type path=\"{path}\"/> matched no nodes");
            }

            XPathNodeIterator attrIterator = metaNavigator.Select("/metadata/attr");

            while (attrIterator.MoveNext())
            {
                var path = attrIterator.Current.GetAttribute("path", "");
                var attr_name = attrIterator.Current.GetAttribute("name", "");
                XPathNodeIterator api_iter = apiNavigator.Select(path);
                var matched = false;

                while (api_iter.MoveNext())
                {
                    var node = ((IHasXmlNode)api_iter.Current).GetNode() as XmlElement;
                    node.SetAttribute(attr_name, attrIterator.Current.Value);
                    matched = true;
                }

                if (!matched)
                    Console.WriteLine($"Warning: <attr path=\"{path}\"/> matched no nodes");
            }

            XPathNodeIterator moveNodeIterator = metaNavigator.Select("/metadata/move-node");

            while (moveNodeIterator.MoveNext())
            {
                var path = moveNodeIterator.Current.GetAttribute("path", "");
                XPathExpression expr = apiNavigator.Compile(path);
                var parent = moveNodeIterator.Current.Value;
                XPathNodeIterator parent_iter = apiNavigator.Select(parent);
                var matched = false;

                while (parent_iter.MoveNext())
                {
                    XmlNode parent_node = ((IHasXmlNode)parent_iter.Current).GetNode();
                    XPathNodeIterator path_iter = parent_iter.Current.Clone().Select(expr);

                    while (path_iter.MoveNext())
                    {
                        XmlNode node = ((IHasXmlNode)path_iter.Current).GetNode();
                        parent_node.AppendChild(node.Clone());
                        node.ParentNode.RemoveChild(node);
                    }

                    matched = true;
                }

                if (!matched)
                    Console.WriteLine($"Warning: <move-node path=\"{path}\"/> matched no nodes");
            }

            XPathNodeIterator removeAttrIterator = metaNavigator.Select("/metadata/remove-attr");

            while (removeAttrIterator.MoveNext())
            {
                var path = removeAttrIterator.Current.GetAttribute("path", "");
                var name = removeAttrIterator.Current.GetAttribute("name", "");
                XPathNodeIterator api_iter = apiNavigator.Select(path);
                var matched = false;

                while (api_iter.MoveNext())
                {
                    var node = ((IHasXmlNode)api_iter.Current).GetNode() as XmlElement;

                    node.RemoveAttribute(name);
                    matched = true;
                }

                if (!matched)
                    Console.WriteLine($"Warning: <remove-attr path=\"{path}\"/> matched no nodes");
            }

            XPathNavigator symbolNavigator = symbolDoc.CreateNavigator();
            XPathNodeIterator iterator = symbolNavigator.Select("/api/*");

            while (iterator.MoveNext())
            {
                XmlNode sym_node = ((IHasXmlNode)iterator.Current).GetNode();
                XPathNodeIterator parent_iter = apiNavigator.Select("/api");

                if (!parent_iter.MoveNext()) continue;

                XmlNode parent_node = ((IHasXmlNode)parent_iter.Current).GetNode();
                parent_node.AppendChild(apiDoc.ImportNode(sym_node, true));
            }

            apiDoc.Save(apiFilename);
            return 0;
        }
    }
}