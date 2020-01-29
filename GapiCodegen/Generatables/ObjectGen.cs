// GtkSharp.Generation.ObjectGen.cs - The Object Generatable.
//
// Author: Mike Kestner <mkestner@ximian.com>
//
// Copyright (c) 2001-2003 Mike Kestner
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

using System;
using System.Collections.Generic;
using System.Linq;
using System.Xml;
using GapiCodegen.Utils;

namespace GapiCodegen.Generatables
{
    /// <summary>
    /// Handles 'boxed' and 'struct' elements with the "opaque" flag (by creating C# classes).
    /// </summary>
    public class ObjectGen : ObjectBase
    {
        private readonly IList<string> _customAttributes = new List<string>();
        private readonly IList<XmlElement> _staticStrings = new List<XmlElement>();
        private readonly IDictionary<string, ChildProperty> _childProperties = new Dictionary<string, ChildProperty>();

        public ObjectGen(XmlElement namespaceElement, XmlElement element) : base(namespaceElement, element, false)
        {
            foreach (XmlNode node in element.ChildNodes)
            {
                var member = node as XmlElement;

                if (member == null)
                {
                    continue;
                }

                if (member.GetAttributeAsBoolean(Constants.Hidden))
                    continue;

                switch (node.Name)
                {
                    case Constants.Callback:
                        Statistics.IgnoreCount++;
                        break;

                    case Constants.CustomAttribute:
                        _customAttributes.Add(member.InnerXml);
                        break;

                    case Constants.StaticString:
                        _staticStrings.Add(member);
                        break;

                    case Constants.ChildProp:
                        var name = member.GetAttribute(Constants.Name);

                        while (_childProperties.ContainsKey(name))
                            name += "mangled";

                        _childProperties.Add(name, new ChildProperty(member, this));
                        break;

                    default:
                        if (!IsNodeNameHandled(node.Name))
                            Console.WriteLine($"Unexpected node {node.Name} in {CName}.");

                        break;
                }
            }
        }

        public override string CallByName(string var, bool owned)
        {
            return $"{var} == null ? IntPtr.Zero : {var}.{(owned ? "OwnedHandle" : "Handle")}";
        }

        public override bool Validate()
        {
            var logWriter = new LogWriter(QualifiedName);

            var invalidProps = _childProperties.Keys
                .Where(propertyName => !_childProperties[propertyName].Validate(logWriter)).ToArray();

            foreach (var propertyName in invalidProps)
                _childProperties.Remove(propertyName);

            return base.Validate();
        }

        private bool DisableVoidCtor => Element.GetAttributeAsBoolean(Constants.DisableVoidCtor);

        private static readonly IDictionary<string, DirectoryInfo> DirectoriesInfo = new Dictionary<string, DirectoryInfo>();

        private class DirectoryInfo
        {
            public readonly string AssemblyName;
            public readonly IDictionary<string, string> Objects;

            public DirectoryInfo(string assemblyName)
            {
                AssemblyName = assemblyName;
                Objects = new Dictionary<string, string>();
            }
        }

        private static DirectoryInfo GetDirectoryInfo(string directory, string assemblyName)
        {
            DirectoryInfo result;

            if (DirectoriesInfo.ContainsKey(directory))
            {
                result = DirectoriesInfo[directory];

                if (result.AssemblyName == assemblyName) return result;

                Console.WriteLine("Can't put multiple assemblies in one directory.");
                return null;
            }

            result = new DirectoryInfo(assemblyName);
            DirectoriesInfo.Add(directory, result);

            return result;
        }

        public override void Generate(GenerationInfo generationInfo)
        {
            generationInfo.CurrentType = QualifiedName;

            var assemblyName = generationInfo.AssemblyName.Length == 0
                ? $"{Namespace.ToLower()}-sharp"
                : generationInfo.AssemblyName;

            var directoryInfo = GetDirectoryInfo(generationInfo.DirectoryPath, assemblyName);

            var streamWriter = generationInfo.Writer = generationInfo.OpenStream(Name, Namespace);

            streamWriter.WriteLine($"namespace {Namespace} {{");
            streamWriter.WriteLine();
            streamWriter.WriteLine("\tusing System;");
            streamWriter.WriteLine("\tusing System.Collections;");
            streamWriter.WriteLine("\tusing System.Collections.Generic;");
            streamWriter.WriteLine("\tusing System.Runtime.InteropServices;");
            streamWriter.WriteLine();

            var table = SymbolTable.Table;

            streamWriter.WriteLine("#region Autogenerated code");

            if (IsDeprecated)
                streamWriter.WriteLine("\t[Obsolete]");

            foreach (var attribute in _customAttributes)
                streamWriter.WriteLine($"\t{attribute}");

            streamWriter.Write("\t{0} {1}partial class {2}", IsInternal ? "internal" : "public",
                IsAbstract ? "abstract " : "", Name);

            var csParent = table.GetCsType(Element.GetAttribute(Constants.Parent));

            if (csParent != "")
            {
                directoryInfo.Objects.Add(CName, QualifiedName);
                streamWriter.Write($" : {csParent}");
            }

            foreach (var iface in Interfaces)
            {
                if (Parent != null && Parent.Implements(iface))
                    continue;

                streamWriter.Write($", {table.GetCsType(iface)}");
            }

            foreach (var iface in ManagedInterfaces)
            {
                if (Parent != null && Parent.Implements(iface))
                    continue;

                streamWriter.Write($", {iface}");
            }

            streamWriter.WriteLine(" {");
            streamWriter.WriteLine();

            GenerateConstructors(generationInfo);
            GenerateProperties(generationInfo, null);
            GenerateFields(generationInfo);
            GenerateChildProperties(generationInfo);

            var hasSignals = Signals != null && Signals.Count > 0;

            if (!hasSignals)
            {
                foreach (var iface in Interfaces)
                {
                    if (!(table.GetClassGen(iface) is InterfaceGen interfaceGen) ||
                        interfaceGen.Signals == null) continue;

                    hasSignals = true;
                    break;
                }
            }

            if (hasSignals && Element.HasAttribute(Constants.Parent))
            {
                GenerateSignals(generationInfo, null);
            }

            GenerateConstants(generationInfo);
            GenerateClassMembers(generationInfo);
            GenerateMethods(generationInfo, null, null);

            if (Interfaces.Count != 0)
            {
                var methods = new Dictionary<string, Method>();

                foreach (var method in Methods.Values)
                {
                    methods[method.Name] = method;
                }

                var collisions = new Dictionary<string, bool>();

                foreach (var iface in Interfaces)
                {
                    var classGen = table.GetClassGen(iface);

                    foreach (var method in classGen.Methods.Values)
                    {
                        if (method.Name.StartsWith("Get") || method.Name.StartsWith("Set"))
                        {
                            if (GetProperty(method.Name.Substring(3)) != null)
                            {
                                collisions[method.Name] = true;
                                continue;
                            }
                        }

                        methods.TryGetValue(method.Name, out var collision);

                        if (collision != null && collision.Signature.Types == method.Signature.Types)
                        {
                            collisions[method.Name] = true;
                        }
                        else
                            methods[method.Name] = method;
                    }
                }

                foreach (var iface in Interfaces)
                {
                    if (Parent != null && Parent.Implements(iface))
                        continue;

                    var interfaceGen = (InterfaceGen)table.GetClassGen(iface);

                    interfaceGen.GenerateMethods(generationInfo, collisions, this);
                    interfaceGen.GenerateProperties(generationInfo, this);
                    interfaceGen.GenerateSignals(generationInfo, this);
                    interfaceGen.GenerateVirtualMethods(generationInfo, this);
                }
            }

            foreach (var str in _staticStrings)
            {
                streamWriter.Write($"\t\tpublic static string {str.GetAttribute(Constants.Name)}");
                streamWriter.WriteLine($" {{\n\t\t\t get {{ return \"{str.GetAttribute(Constants.Value)}\"; }}\n\t\t}}");
            }

            if (csParent != string.Empty && GetExpected(CName) != QualifiedName)
            {
                streamWriter.WriteLine();
                streamWriter.WriteLine($"\t\tstatic {Name} ()");
                streamWriter.WriteLine("\t\t{");
                streamWriter.WriteLine($"\t\t\tGtkSharp.{Studlify(assemblyName)}.ObjectManager.Initialize ();");
                streamWriter.WriteLine("\t\t}");
            }

            GenerateStructAbi(generationInfo);

            streamWriter.WriteLine("#endregion");
            streamWriter.WriteLine("\t}");
            streamWriter.WriteLine("}");

            streamWriter.Close();
            generationInfo.Writer = null;

            Statistics.ObjectCount++;
        }

        protected override void GenerateConstructors(GenerationInfo generationInfo)
        {
            if (!Element.HasAttribute(Constants.Parent))
                return;

            var defaultconstructoraccess = Element.HasAttribute("defaultconstructoraccess")
                ? Element.GetAttribute("defaultconstructoraccess")
                : "public";

            generationInfo.Writer.WriteLine($"\t\t{defaultconstructoraccess} {Name} (IntPtr raw) : base(raw) {{}}");

            if (Constructors.Count == 0 && !DisableVoidCtor)
            {
                generationInfo.Writer.WriteLine();
                generationInfo.Writer.WriteLine($"\t\tprotected {Name}() : base(IntPtr.Zero)");
                generationInfo.Writer.WriteLine("\t\t{");
                generationInfo.Writer.WriteLine("\t\t\tCreateNativeObject (new string [0], new GLib.Value [0]);");
                generationInfo.Writer.WriteLine("\t\t}");
            }

            generationInfo.Writer.WriteLine();

            base.GenerateConstructors(generationInfo);
        }

        protected void GenerateChildProperties(GenerationInfo generationInfo)
        {
            if (_childProperties.Count == 0)
                return;

            var streamWriter = generationInfo.Writer;

            var childAncestor = Parent as ObjectGen;

            while (childAncestor.CName != "GtkContainer" &&
                   childAncestor._childProperties.Count == 0)
            {
                childAncestor = childAncestor.Parent as ObjectGen;
            }

            streamWriter.WriteLine(
                $"\t\tpublic class {Name}Child : {childAncestor.Namespace}.{childAncestor.Name}.{childAncestor.Name}Child {{");
            streamWriter.WriteLine(
                $"\t\t\tprotected internal {Name}Child (Gtk.Container parent, Gtk.Widget child) : base (parent, child) {{}}");
            streamWriter.WriteLine("");

            foreach (var childProperty in _childProperties.Values)
                childProperty.Generate(generationInfo, "\t\t\t", null);

            streamWriter.WriteLine("\t\t}");
            streamWriter.WriteLine("");

            streamWriter.WriteLine("\t\tpublic override Gtk.Container.ContainerChild this [Gtk.Widget child] {");
            streamWriter.WriteLine("\t\t\tget {");
            streamWriter.WriteLine($"\t\t\t\treturn new {Name}Child (this, child);");
            streamWriter.WriteLine("\t\t\t}");
            streamWriter.WriteLine("\t\t}");
            streamWriter.WriteLine("");
        }

        private void GenerateClassMembers(GenerationInfo generationInfo)
        {
            GenerateVirtualMethods(generationInfo, null);
            GenerateStructAbi(generationInfo, AbiClassMembers, "class_abi", ClassStructName);
        }

        /* Keep this in sync with the one in glib/GType.cs */
        private static string GetExpected(string cName)
        {
            for (var i = 1; i < cName.Length; i++)
            {
                if (!char.IsUpper(cName[i])) continue;

                if (i == 1 && cName[0] == 'G')
                    return $"GLib.{cName.Substring(1)}";
                
                return $"{cName.Substring(0, i)}.{cName.Substring(i)}";
            }

            throw new ArgumentException($"cname doesn't follow the NamespaceType capitalization style: {cName}.");
        }

        private static bool NeedsMap(IDictionary<string, string> objs)
        {
            return objs.Keys.Any(key => GetExpected(key) != objs[key]);
        }

        private static string Studlify(string name)
        {
            var subs = name.Split('-');

            return subs.Aggregate("", (current, sub) => $"{current}{char.ToUpper(sub[0]) + sub.Substring(1)}");
        }

        public static void GenerateMappers()
        {
            foreach (var directory in DirectoriesInfo.Keys)
            {
                var directoryInfo = DirectoriesInfo[directory];

                if (!NeedsMap(directoryInfo.Objects))
                    continue;

                var generationInfo = new GenerationInfo(directory, directoryInfo.AssemblyName);

                GenerateMapper(directoryInfo, generationInfo);
            }
        }

        private static void GenerateMapper(DirectoryInfo directoryInfo, GenerationInfo generationInfo)
        {
            var streamWriter = generationInfo.OpenStream("ObjectManager", "GtkSharp");

            streamWriter.WriteLine($"namespace GtkSharp.{Studlify(directoryInfo.AssemblyName)} {{");
            streamWriter.WriteLine();
            streamWriter.WriteLine("\tpublic class ObjectManager {");
            streamWriter.WriteLine();
            streamWriter.WriteLine("\t\tstatic bool initialized = false;");
            streamWriter.WriteLine("\t\t// Call this method from the appropriate module init function.");
            streamWriter.WriteLine("\t\tpublic static void Initialize ()");
            streamWriter.WriteLine("\t\t{");
            streamWriter.WriteLine("\t\t\tif (initialized)");
            streamWriter.WriteLine("\t\t\t\treturn;");
            streamWriter.WriteLine("");
            streamWriter.WriteLine("\t\t\tinitialized = true;");

            foreach (var key in directoryInfo.Objects.Keys)
            {
                if (GetExpected(key) != directoryInfo.Objects[key])
                {
                    streamWriter.WriteLine("\t\t\tGLib.GType.Register ({0}.GType, typeof ({0}));", directoryInfo.Objects[key]);
                }
            }

            streamWriter.WriteLine("\t\t}");
            streamWriter.WriteLine("\t}");
            streamWriter.WriteLine("}");
            streamWriter.Close();
        }
    }
}
