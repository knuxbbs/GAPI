// GtkSharp.Generation.InterfaceGen.cs - The Interface Generatable.
//
// Authors:
//   Mike Kestner <mkestner@speakeasy.net>
//   Andres G. Aragoneses <knocte@gmail.com>
//
// Copyright (c) 2001-2003 Mike Kestner
// Copyright (c) 2004, 2007 Novell, Inc.
// Copyright (c) 2013 Andres G. Aragoneses
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
using System.Linq;
using System.Xml;
using GapiCodegen.Utils;

namespace GapiCodegen.Generatables
{
    /// <summary>
    /// Handles 'interface' elements.
    /// </summary>
    public class InterfaceGen : ObjectBase
    {
        public InterfaceGen(XmlElement namespaceElement, XmlElement element) : base(namespaceElement, element, true)
        {
            IsConsumeOnly = element.GetAttributeAsBoolean(Constants.ConsumeOnly);

            foreach (XmlNode node in element.ChildNodes)
            {
                if (!(node is XmlElement)) continue;

                if (!IsNodeNameHandled(node.Name))
                    new LogWriter(QualifiedName).Warn($"Unexpected node {node.Name}");
            }
        }

        public bool IsConsumeOnly { get; }

        public string AdapterName => $"{base.Name}Adapter";

        public string QualifiedAdapterName => $"{Namespace}.{AdapterName}";

        public string ImplementorName => $"{Name}Implementor";

        public override string Name => $"I{base.Name}";

        public override string CallByName(string var, bool owned)
        {
            return string.Format(
                "{0} == null ? IntPtr.Zero : (({0} is GLib.Object) ? ({0} as GLib.Object).{1} : ({0} as {2}).{1})",
                var, owned ? "OwnedHandle" : "Handle", QualifiedAdapterName);
        }

        public override string FromNative(string varName, bool owned)
        {
            return $"{QualifiedAdapterName}.GetObject({varName}, {(owned ? "true" : "false")})";
        }

        public override bool ValidateForSubclass()
        {
            if (!base.ValidateForSubclass())
                return false;

            var logWriter = new LogWriter(QualifiedName);
            var invalidMethods = Methods.Values.Where(method => !method.Validate(logWriter)).ToArray();

            foreach (var method in invalidMethods)
                Methods.Remove(method.Name);

            return true;
        }

        private void GenerateStaticConstructor(TextWriter textWriter)
        {
            textWriter.WriteLine("\t\tstatic {0} iface;", ClassStructName);
            textWriter.WriteLine();
            textWriter.WriteLine($"\t\tstatic {AdapterName}()");
            textWriter.WriteLine("\t\t{");
            textWriter.WriteLine("\t\t\tGLib.GType.Register(_gtype, typeof({0}));", AdapterName);

            foreach (var virtualMethod in InterfaceVirtualMethods)
            {
                if (virtualMethod.Validate(new LogWriter(QualifiedName)))
                {
                    textWriter.WriteLine("\t\t\tiface.{0} = new {0}NativeDelegate({0}_cb);", virtualMethod.Name);
                }
            }

            textWriter.WriteLine("\t\t}");
            textWriter.WriteLine();
        }

        private void GenerateInitializer(TextWriter textWriter)
        {
            if (InterfaceVirtualMethods.Count > 0)
            {
                textWriter.WriteLine("\t\tstatic int class_offset = 2 * IntPtr.Size;"); // Class size of GTypeInterface struct
                textWriter.WriteLine();
            }

            textWriter.WriteLine("\t\tstatic void Initialize(IntPtr ptr, IntPtr data)");
            textWriter.WriteLine("\t\t{");

            if (InterfaceVirtualMethods.Count > 0)
            {
                textWriter.WriteLine("\t\t\tIntPtr ifaceptr = new IntPtr(ptr.ToInt64() + class_offset);");
                textWriter.WriteLine("\t\t\t{0} native_iface = ({0})Marshal.PtrToStructure(ifaceptr, typeof({0}));", ClassStructName);

                foreach (var virtualMethod in InterfaceVirtualMethods)
                {
                    textWriter.WriteLine($"\t\t\tnative_iface.{virtualMethod.Name} = iface.{virtualMethod.Name};");
                }

                textWriter.WriteLine("\t\t\tMarshal.StructureToPtr(native_iface, ifaceptr, false);");
            }

            textWriter.WriteLine("\t\t}");
            textWriter.WriteLine();
        }

        private void GenerateCallbacks(StreamWriter streamWriter)
        {
            foreach (var virtualMethod in InterfaceVirtualMethods)
            {
                virtualMethod.GenerateCallback(streamWriter, null);
            }
        }

        private void GenerateConstructors(TextWriter textWriter)
        {
            // Native GObjects do not implement the *Implementor interfaces
            textWriter.WriteLine("\t\tGLib.Object implementor;");
            textWriter.WriteLine();

            if (!IsConsumeOnly)
            {
                textWriter.WriteLine($"\t\tpublic {AdapterName}()");
                textWriter.WriteLine("\t\t{");
                textWriter.WriteLine("\t\t\tInitHandler = new GLib.GInterfaceInitHandler(Initialize);");
                textWriter.WriteLine("\t\t}");
                textWriter.WriteLine();
                textWriter.WriteLine("\t\tpublic {0}({1} implementor)", AdapterName, ImplementorName);
                textWriter.WriteLine("\t\t{");
                textWriter.WriteLine("\t\t\tif (implementor == null)");
                textWriter.WriteLine("\t\t\t\tthrow new ArgumentNullException(\"implementor\");");
                textWriter.WriteLine("\t\t\telse if(!(implementor is GLib.Object))");
                textWriter.WriteLine("\t\t\t\tthrow new ArgumentException(\"implementor must be a subclass of GLib.Object\");");
                textWriter.WriteLine("\t\t\tthis.implementor = implementor as GLib.Object;");
                textWriter.WriteLine("\t\t}");
                textWriter.WriteLine();
            }

            textWriter.WriteLine($"\t\tpublic {AdapterName}(IntPtr handle)");
            textWriter.WriteLine("\t\t{");
            textWriter.WriteLine("\t\t\tif (!_gtype.IsInstance(handle))");
            textWriter.WriteLine("\t\t\t\tthrow new ArgumentException(\"The gobject doesn't implement the GInterface of this adapter\", \"handle\");");
            textWriter.WriteLine("\t\t\timplementor = GLib.Object.GetObject(handle);");
            textWriter.WriteLine("\t\t}");
            textWriter.WriteLine();
        }

        private void GenerateGType(StreamWriter streamWriter)
        {
            var method = GetMethod("GetType");

            if (method == null)
                throw new Exception($"Interface {QualifiedName} missing GetType method.");

            method.GenerateImport(streamWriter);
            streamWriter.WriteLine("\t\tprivate static GLib.GType _gtype = new GLib.GType({0}());", method.CName);
            streamWriter.WriteLine();

            // by convention, all GTypes generated have a static GType property
            streamWriter.WriteLine("\t\tpublic static GLib.GType GType {");
            streamWriter.WriteLine("\t\t\tget {");
            streamWriter.WriteLine("\t\t\t\treturn _gtype;");
            streamWriter.WriteLine("\t\t\t}");
            streamWriter.WriteLine("\t\t}");
            streamWriter.WriteLine();

            // we need same property but non-static, because it is being accessed via a GInterfaceAdapter instance
            streamWriter.WriteLine("\t\tpublic override GLib.GType GInterfaceGType {");
            streamWriter.WriteLine("\t\t\tget {");
            streamWriter.WriteLine("\t\t\t\treturn _gtype;");
            streamWriter.WriteLine("\t\t\t}");
            streamWriter.WriteLine("\t\t}");

            streamWriter.WriteLine();
        }

        private static void GenerateHandleProperty(TextWriter textWriter)
        {
            textWriter.WriteLine("\t\tpublic override IntPtr Handle {");
            textWriter.WriteLine("\t\t\tget {");
            textWriter.WriteLine("\t\t\t\treturn implementor.Handle;");
            textWriter.WriteLine("\t\t\t}");
            textWriter.WriteLine("\t\t}");
            textWriter.WriteLine();
            textWriter.WriteLine("\t\tpublic IntPtr OwnedHandle {");
            textWriter.WriteLine("\t\t\tget {");
            textWriter.WriteLine("\t\t\t\treturn implementor.OwnedHandle;");
            textWriter.WriteLine("\t\t\t}");
            textWriter.WriteLine("\t\t}");
            textWriter.WriteLine();
        }

        private void GenerateGetObject(TextWriter textWriter)
        {
            textWriter.WriteLine($"\t\tpublic static {Name} GetObject(IntPtr handle, bool owned)");
            textWriter.WriteLine("\t\t{");
            textWriter.WriteLine("\t\t\tGLib.Object obj = GLib.Object.GetObject(handle, owned);");
            textWriter.WriteLine("\t\t\treturn GetObject(obj);");
            textWriter.WriteLine("\t\t}");
            textWriter.WriteLine();
            textWriter.WriteLine($"\t\tpublic static {Name} GetObject(GLib.Object obj)");
            textWriter.WriteLine("\t\t{");
            textWriter.WriteLine("\t\t\tif (obj == null)");
            textWriter.WriteLine("\t\t\t\treturn null;");

            if (!IsConsumeOnly)
            {
                textWriter.WriteLine($"\t\t\telse if (obj is {ImplementorName})");
                textWriter.WriteLine("\t\t\t\treturn new {0}(obj as {1});", AdapterName, ImplementorName);
            }

            textWriter.WriteLine($"\t\t\telse if (obj as {Name} == null)");
            textWriter.WriteLine("\t\t\t\treturn new {0}(obj.Handle);", AdapterName);
            textWriter.WriteLine("\t\t\telse");
            textWriter.WriteLine("\t\t\t\treturn obj as {0};", Name);
            textWriter.WriteLine("\t\t}");
            textWriter.WriteLine();
        }

        private void GenerateImplementorProperty(TextWriter textWriter)
        {
            textWriter.WriteLine($"\t\tpublic {ImplementorName} Implementor {{");
            textWriter.WriteLine("\t\t\tget {");
            textWriter.WriteLine("\t\t\t\treturn implementor as {0};", ImplementorName);
            textWriter.WriteLine("\t\t\t}");
            textWriter.WriteLine("\t\t}");
            textWriter.WriteLine();
        }

        private void GenerateAdapter(GenerationInfo generationInfo)
        {
            var streamWriter = generationInfo.Writer = generationInfo.OpenStream(AdapterName, Namespace);

            streamWriter.WriteLine($"namespace {Namespace} {{");
            streamWriter.WriteLine();
            streamWriter.WriteLine("\tusing System;");
            streamWriter.WriteLine("\tusing System.Runtime.InteropServices;");
            streamWriter.WriteLine();
            streamWriter.WriteLine("#region Autogenerated code");
            streamWriter.WriteLine($"\tpublic partial class {AdapterName} : GLib.GInterfaceAdapter, {QualifiedName} {{");
            streamWriter.WriteLine();

            if (!IsConsumeOnly)
            {
                GenerateClassStruct(generationInfo);
                GenerateStaticConstructor(streamWriter);
                GenerateCallbacks(streamWriter);
                GenerateInitializer(streamWriter);
            }

            GenerateConstructors(streamWriter);
            GenerateGType(streamWriter);
            GenerateHandleProperty(streamWriter);
            GenerateGetObject(streamWriter);

            if (!IsConsumeOnly)
                GenerateImplementorProperty(streamWriter);

            GenerateProperties(generationInfo, null);

            foreach (var signal in Signals.Values)
                signal.GenEvent(streamWriter, null, "GLib.Object.GetObject(Handle)");

            var method = GetMethod("GetType");

            if (method != null)
                Methods.Remove("GetType");

            GenerateMethods(generationInfo, null, this);

            if (method != null)
                Methods["GetType"] = method;

            streamWriter.WriteLine("#endregion");

            streamWriter.WriteLine("\t}");
            streamWriter.WriteLine("}");
            streamWriter.Close();

            generationInfo.Writer = null;
        }

        private void GenerateImplementorInterface(GenerationInfo generationInfo)
        {
            if (IsConsumeOnly)
                return;

            var streamWriter = generationInfo.Writer;
            streamWriter.WriteLine();
            streamWriter.WriteLine($"\t[GLib.GInterface(typeof({AdapterName}))]");
            streamWriter.WriteLine(
                $"\t{(IsInternal ? "internal" : "public")} partial interface {ImplementorName} : GLib.IWrapper {{");
            streamWriter.WriteLine();

            var virtualMethods = new Dictionary<string, InterfaceVirtualMethod>();

            foreach (var virtualMethod in InterfaceVirtualMethods)
            {
                virtualMethods[virtualMethod.Name] = virtualMethod;
            }

            foreach (var virtualMethod in InterfaceVirtualMethods)
            {
                if (!virtualMethod.Validate(new LogWriter(QualifiedName)))
                {
                    virtualMethods.Remove(virtualMethod.Name);
                }
                else if (virtualMethod.IsGetter || virtualMethod.IsSetter)
                {
                    var cmp_name = $"{(virtualMethod.IsGetter ? "Set" : "Get")}{virtualMethod.Name.Substring(3)}";

                    if (virtualMethods.TryGetValue(cmp_name, out var cmp) && (cmp.IsGetter || cmp.IsSetter))
                    {
                        if (virtualMethod.IsSetter)
                        {
                            cmp.GenerateDeclaration(streamWriter, virtualMethod);
                        }
                        else
                        {
                            virtualMethod.GenerateDeclaration(streamWriter, cmp);
                        }

                        virtualMethods.Remove(cmp.Name);
                    }
                    else
                    {
                        virtualMethod.GenerateDeclaration(streamWriter, null);
                    }

                    virtualMethods.Remove(virtualMethod.Name);
                }
                else
                {
                    virtualMethod.GenerateDeclaration(streamWriter, null);
                    virtualMethods.Remove(virtualMethod.Name);
                }
            }

            foreach (var property in Properties.Values)
            {
                streamWriter.WriteLine($"\t\t[GLib.Property(\"{property.CName}\")]");
                property.GenerateDeclaration(streamWriter, "\t\t");
            }

            streamWriter.WriteLine("\t}");
        }

        public override void Generate(GenerationInfo generationInfo)
        {
            GenerateAdapter(generationInfo);

            var streamWriter = generationInfo.Writer = generationInfo.OpenStream(Name, Namespace);

            streamWriter.WriteLine($"namespace {Namespace} {{");
            streamWriter.WriteLine();
            streamWriter.WriteLine("\tusing System;");
            streamWriter.WriteLine();
            streamWriter.WriteLine("#region Autogenerated code");
            streamWriter.WriteLine(
                $"\t{(IsInternal ? "internal" : "public")} partial interface {Name} : GLib.IWrapper {{");
            streamWriter.WriteLine();

            foreach (var signal in Signals.Values)
            {
                signal.GenerateDecl(streamWriter);
                signal.GenEventHandler(generationInfo);
            }

            foreach (var method in Methods.Values)
            {
                if (IgnoreMethod(method, this))
                    continue;

                method.GenerateDeclaration(streamWriter);
            }

            foreach (var property in Properties.Values)
                property.GenerateDeclaration(streamWriter, "\t\t");

            streamWriter.WriteLine("\t}");
            GenerateImplementorInterface(generationInfo);
            streamWriter.WriteLine("#endregion");
            streamWriter.WriteLine("}");
            streamWriter.Close();

            generationInfo.Writer = null;

            Statistics.InterfaceCount++;
        }
    }
}
