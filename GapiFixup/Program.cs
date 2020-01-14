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

            var api_filename = string.Empty;
            XmlDocument api_doc = new XmlDocument();
            XmlDocument meta_doc = new XmlDocument();
            XmlDocument symbol_doc = new XmlDocument();

            foreach (var arg in args)
            {
                if (arg.StartsWith("--metadata="))
                {
                    var meta_filename = arg.Substring("--metadata=".Length);

                    try
                    {
                        Stream stream = File.OpenRead(meta_filename);
                        meta_doc.Load(stream);
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
                    api_filename = arg.Substring("--api=".Length);

                    try
                    {
                        Stream stream = File.OpenRead(api_filename);
                        api_doc.Load(stream);
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
                    var symbol_filename = arg.Substring("--symbols=".Length);

                    try
                    {
                        Stream stream = File.OpenRead(symbol_filename);
                        symbol_doc.Load(stream);
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

            XPathNavigator meta_nav = meta_doc.CreateNavigator();
            XPathNavigator api_nav = api_doc.CreateNavigator();

            XPathNodeIterator copy_iter = meta_nav.Select("/metadata/copy-node");

            while (copy_iter.MoveNext())
            {
                var path = copy_iter.Current.GetAttribute("path", string.Empty);
                XPathExpression expr = api_nav.Compile(path);
                var parent = copy_iter.Current.Value;
                XPathNodeIterator parent_iter = api_nav.Select(parent);
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

            XPathNodeIterator rmv_iter = meta_nav.Select("/metadata/remove-node");

            while (rmv_iter.MoveNext())
            {
                var path = rmv_iter.Current.GetAttribute("path", "");
                XPathNodeIterator api_iter = api_nav.Select(path);
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

            XPathNodeIterator add_iter = meta_nav.Select("/metadata/add-node");

            while (add_iter.MoveNext())
            {
                var path = add_iter.Current.GetAttribute("path", "");
                XPathNodeIterator api_iter = api_nav.Select(path);
                var matched = false;

                while (api_iter.MoveNext())
                {
                    var api_node = ((IHasXmlNode)api_iter.Current).GetNode() as XmlElement;

                    foreach (XmlNode child in ((IHasXmlNode)add_iter.Current).GetNode().ChildNodes)
                        api_node.AppendChild(api_doc.ImportNode(child, true));

                    matched = true;
                }

                if (!matched)
                    Console.WriteLine($"Warning: <add-node path=\"{path}\"/> matched no nodes");
            }

            XPathNodeIterator change_node_type_iter = meta_nav.Select("/metadata/change-node-type");

            while (change_node_type_iter.MoveNext())
            {
                var path = change_node_type_iter.Current.GetAttribute("path", "");
                XPathNodeIterator api_iter = api_nav.Select(path);
                var matched = false;

                while (api_iter.MoveNext())
                {
                    var node = ((IHasXmlNode)api_iter.Current).GetNode() as XmlElement;
                    var parent = node.ParentNode as XmlElement;
                    XmlElement new_node = api_doc.CreateElement(change_node_type_iter.Current.Value);

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

            XPathNodeIterator attr_iter = meta_nav.Select("/metadata/attr");

            while (attr_iter.MoveNext())
            {
                var path = attr_iter.Current.GetAttribute("path", "");
                var attr_name = attr_iter.Current.GetAttribute("name", "");
                XPathNodeIterator api_iter = api_nav.Select(path);
                var matched = false;

                while (api_iter.MoveNext())
                {
                    var node = ((IHasXmlNode)api_iter.Current).GetNode() as XmlElement;
                    node.SetAttribute(attr_name, attr_iter.Current.Value);
                    matched = true;
                }

                if (!matched)
                    Console.WriteLine($"Warning: <attr path=\"{path}\"/> matched no nodes");
            }

            XPathNodeIterator move_iter = meta_nav.Select("/metadata/move-node");

            while (move_iter.MoveNext())
            {
                var path = move_iter.Current.GetAttribute("path", "");
                XPathExpression expr = api_nav.Compile(path);
                var parent = move_iter.Current.Value;
                XPathNodeIterator parent_iter = api_nav.Select(parent);
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

            XPathNodeIterator remove_attr_iter = meta_nav.Select("/metadata/remove-attr");

            while (remove_attr_iter.MoveNext())
            {
                var path = remove_attr_iter.Current.GetAttribute("path", "");
                var name = remove_attr_iter.Current.GetAttribute("name", "");
                XPathNodeIterator api_iter = api_nav.Select(path);
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

            XPathNavigator symbol_nav = symbol_doc.CreateNavigator();
            XPathNodeIterator iter = symbol_nav.Select("/api/*");

            while (iter.MoveNext())
            {
                XmlNode sym_node = ((IHasXmlNode)iter.Current).GetNode();
                XPathNodeIterator parent_iter = api_nav.Select("/api");

                if (!parent_iter.MoveNext()) continue;

                XmlNode parent_node = ((IHasXmlNode)parent_iter.Current).GetNode();
                parent_node.AppendChild(api_doc.ImportNode(sym_node, true));
            }

            api_doc.Save(api_filename);
            return 0;
        }
    }
}