// ObjectBase.cs - Base class for Object types
//
// Authors:  Mike Kestner <mkestner@novell.com>
//
// Copyright (c) 2005 Novell, Inc.
// Copyright (c) 2009 Christian Hoff
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
using System.Collections;
using System.Collections.Generic;
using System.Linq;
using System.Xml;
using GapiCodegen.Utils;

namespace GapiCodegen.Generatables
{
    /// <summary>
    /// Base class for GObject/GInterface types.
    /// </summary>
    public abstract class ObjectBase : HandleBase
    {
        private readonly ArrayList _classMembers = new ArrayList();
        private readonly bool _isInterface;

        private bool _classAbiValid = true;

        protected IList<StructAbiField> AbiClassMembers = new List<StructAbiField>();
        protected IList<ClassField> ClassFields = new List<ClassField>();

        // The default handlers of these signals need to be overridden with g_signal_override_class_closure
        protected IList<GObjectVirtualMethod> VirtualMethods = new List<GObjectVirtualMethod>();

        // virtual methods that are generated as an IntPtr in the class struct
        protected IList<VirtualMethod> HiddenVirtualMethods = new List<VirtualMethod>();
        protected IList<InterfaceVirtualMethod> InterfaceVirtualMethods = new List<InterfaceVirtualMethod>();

        protected ObjectBase(XmlElement namespaceElement, XmlElement element, bool isInterface) : base(namespaceElement, element)
        {
            _isInterface = isInterface;
            XmlElement classElement = null;

            var virtualMethods = new Dictionary<string, XmlElement>();
            var signalVirtualMethods = new Dictionary<string, XmlElement>();

            if (ParserVersion == 1)
                ClassStructName = $"{CName}{(isInterface ? "Iface" : "Class")}";

            foreach (XmlNode node in element.ChildNodes)
            {
                if (!(node is XmlElement)) continue;
                var member = node as XmlElement;

                switch (node.Name)
                {
                    case Constants.VirtualMethod:
                        if (ParserVersion == 1 && isInterface)
                        {
                            // Generating non-signal GObject virtual methods is not supported in compatibility mode
                            AddVirtualMethod(member, false, true);
                        }
                        else
                            virtualMethods.Add(member.GetAttribute(Constants.CName), member);
                        break;

                    case Constants.Signal:
                        if (ParserVersion == 1 || member.GetAttribute(Constants.FieldName) == "")
                        {
                            AddVirtualMethod(member, true, isInterface);
                        }
                        else
                        {
                            signalVirtualMethods.Add(member.GetAttribute(Constants.FieldName), member);
                        }

                        if (!member.GetAttributeAsBoolean(Constants.Hidden))
                        {
                            var cName = member.GetAttribute(Constants.CName);

                            while (Signals.ContainsKey(cName))
                                cName += "mangled";

                            Signals.Add(cName, new Signal(member, this));
                        }
                        break;

                    case "class_struct":
                        classElement = member;
                        break;
                }
            }

            if (classElement == null) return;

            if (classElement.GetAttributeAsBoolean("private"))
            {
                _classAbiValid = false;
                return;
            }

            ClassStructName = classElement.GetAttribute(Constants.CName);

            var numAbiFields = 0;

            for (var index = 0; index < classElement.ChildNodes.Count; index++)
            {
                var node = classElement.ChildNodes[index];
                if (!(node is XmlElement)) continue;

                var member = (XmlElement)node;

                switch (node.Name)
                {
                    // Make sure ABI fields are taken into account, even when hidden.
                    case Constants.Field:
                        {
                            numAbiFields += 1;

                            // Skip instance parent struct
                            if (numAbiFields != 1)
                            {
                                AbiClassMembers.Add(new StructAbiField(member, this, "class_abi"));
                            }

                            break;
                        }
                    case Constants.Method:
                        AbiClassMembers.Add(new MethodAbiField(member, this, "class_abi"));
                        break;
                    case "union":
                        AbiClassMembers.Add(new UnionAbiField(member, this, "class_abi"));
                        break;
                }

                switch (member.Name)
                {
                    case Constants.Method:
                        var isSignalVirtualMethod = member.HasAttribute("signal_vm");

                        string virtualMethodName;
                        XmlElement virtualMethodElement;

                        if (isSignalVirtualMethod)
                        {
                            virtualMethodName = member.GetAttribute("signal_vm");
                            virtualMethodElement = signalVirtualMethods[virtualMethodName];
                        }
                        else
                        {
                            virtualMethodName = member.GetAttribute("vm");
                            virtualMethodElement = virtualMethods[virtualMethodName];
                        }

                        AddVirtualMethod(virtualMethodElement, isSignalVirtualMethod, isInterface);
                        break;

                    case Constants.Field:
                        if (index == 0) continue; // Parent class

                        var field = new ClassField(member, this);

                        ClassFields.Add(field);
                        _classMembers.Add(field);
                        break;

                    default:
                        Console.WriteLine($"Unexpected node {member.Name} in {classElement.GetAttribute(Constants.CName)}.");
                        break;
                }
            }
        }

        protected sealed override bool IsNodeNameHandled(string name)
        {
            switch (name)
            {
                case Constants.VirtualMethod:
                case Constants.Signal:
                case "class_struct":
                    return true;
                default:
                    return base.IsNodeNameHandled(name);
            }
        }

        public override string CallByName(string var)
        {
            return CallByName(var, false);
        }

        public abstract string CallByName(string var, bool owned);

        public override string FromNative(string varName, bool owned)
        {
            return $"GLib.Object.GetObject({varName}{(owned ? ", true" : "")}) as {QualifiedName}";
        }

        public string ClassStructName { get; }

        /// <summary>
        /// Generation of interface class structs was already supported by version 2.12 of the GAPI parser.Their layout was determined by the order
        /// in which the signal and virtual_method elements appeared in the XML.However, we cannot use that approach for old GObject class structs
        /// as they may contain class fields which don't appear in the old (version 1) API files. There are also cases in which the order of the
        /// 'signal' and 'virtual_method' elements do not match the struct layout.
        /// </summary>
        public bool CanGenerateClassStruct => (_isInterface || ParserVersion >= 2) &&
                                              (_classAbiValid || ClassStructName == "GtkWidgetClass");

        public override bool CanGenerateAbiStruct(LogWriter logWriter)
        {
            if (!abi_fields_valid)
            {
                logWriter.Info($"{CName} has invalid fields.");

                return false;
            }

            // No instance structure for interfaces
            if (!_isInterface) return ClassStructName != null;

            logWriter.Info($"{CName} is interface.");

            return false;
        }

        protected void GenerateClassStruct(GenerationInfo generationInfo)
        {
            if (ClassStructName == null || !CanGenerateClassStruct) return;

            var streamWriter = generationInfo.Writer;

            streamWriter.WriteLine("\t\t[StructLayout (LayoutKind.Sequential)]");
            streamWriter.WriteLine($"\t\tstruct {ClassStructName} {{");

            foreach (var member in _classMembers)
            {
                switch (member)
                {
                    case VirtualMethod virtualMethod:
                        {
                            if (HiddenVirtualMethods.Contains(virtualMethod) || _isInterface && virtualMethod is DefaultSignalHandler)
                            {
                                streamWriter.WriteLine("\t\t\tIntPtr {0};", virtualMethod.Name);
                            }
                            else
                            {
                                streamWriter.WriteLine("\t\t\tpublic {0}NativeDelegate {0};", virtualMethod.Name);
                            }

                            break;
                        }
                    case ClassField classField:
                        {
                            classField.Generate(generationInfo, "\t\t\t");

                            break;
                        }
                }
            }

            streamWriter.WriteLine("\t\t}");
            streamWriter.WriteLine();
        }

        public Dictionary<string, Signal> Signals { get; } = new Dictionary<string, Signal>();

        public Signal GetSignal(string name)
        {
            return Signals[name];
        }

        public Signal GetSignalRecursively(string name)
        {
            return GetSignalRecursively(name, false);
        }

        public virtual Signal GetSignalRecursively(string name, bool checkSelf)
        {
            Signal signal = null;

            if (checkSelf)
                signal = GetSignal(name);

            if (signal == null && Parent != null)
                signal = (Parent as ObjectBase)?.GetSignalRecursively(name, true);

            if (!checkSelf || signal != null) return signal;

            foreach (var iface in Interfaces)
            {
                var interfaceGen = SymbolTable.Table.GetClassGen(iface) as InterfaceGen;

                if (interfaceGen == null)
                    continue;

                signal = interfaceGen.GetSignalRecursively(name, true);

                if (signal != null)
                    break;
            }

            return signal;
        }

        public void GenerateSignals(GenerationInfo generationInfo, ObjectBase implementor)
        {
            foreach (var signal in Signals.Values)
                signal.Generate(generationInfo, implementor);
        }

        public void GenerateVirtualMethods(GenerationInfo generationInfo, ObjectBase implementor)
        {
            foreach (var virtualMethod in VirtualMethods)
                virtualMethod.Generate(generationInfo, implementor);
        }

        public override bool Validate()
        {
            if (Parent != null && !((ObjectBase)Parent).ValidateForSubclass())
                return false;

            var logWriter = new LogWriter(QualifiedName);

            var invalidVirtualMethods =
                VirtualMethods.Where(virtualMethod => !virtualMethod.Validate(logWriter)).ToArray();

            foreach (var virtualMethod in invalidVirtualMethods)
            {
                VirtualMethods.Remove(virtualMethod);
                HiddenVirtualMethods.Add(virtualMethod);
            }

            foreach (var field in ClassFields)
                field.Validate(logWriter);

            foreach (var abiField in AbiClassMembers)
                if (!abiField.Validate(logWriter))
                    _classAbiValid = false;

            var invalidInterfaceVirtualMethods = InterfaceVirtualMethods
                .Where(interfaceVirtualMethod => !interfaceVirtualMethod.Validate(logWriter)).ToArray();

            foreach (var interfaceVirtualMethod in invalidInterfaceVirtualMethods)
            {
                InterfaceVirtualMethods.Remove(interfaceVirtualMethod);
                HiddenVirtualMethods.Add(interfaceVirtualMethod);
            }

            var invalidSignals = Signals.Values.Where(signal => !signal.Validate(logWriter)).ToArray();

            foreach (var signal in invalidSignals)
                Signals.Remove(signal.Name);

            return base.Validate();
        }

        public virtual bool ValidateForSubclass()
        {
            var logWriter = new LogWriter(QualifiedName);
            var invalidSignals =
                Signals.Values.Where(signal => !signal.Validate(logWriter)).ToList();

            foreach (var signal in invalidSignals)
                Signals.Remove(signal.Name);

            invalidSignals.Clear();

            return true;
        }

        public override string GenerateGetSizeOf()
        {
            return $"{Namespace}.{Name}.abi_info.Size";
        }

        private void AddVirtualMethod(XmlElement element, bool isSignalVirtualMethod, bool isInterface)
        {
            VirtualMethod virtualMethod;

            if (isSignalVirtualMethod)
            {
                virtualMethod = new DefaultSignalHandler(element, this);
            }
            else if (isInterface)
            {
                var targetName = element.HasAttribute("target_method")
                    ? element.GetAttribute("target_method")
                    : element.GetAttribute(Constants.Name);

                virtualMethod = new InterfaceVirtualMethod(element, GetMethod(targetName), this);
            }
            else
                virtualMethod = new GObjectVirtualMethod(element, this);

            if (element.GetAttributeAsBoolean("padding") || element.GetAttributeAsBoolean(Constants.Hidden))
            {
                HiddenVirtualMethods.Add(virtualMethod);
            }
            else
            {
                if (virtualMethod is GObjectVirtualMethod gObjectVirtualMethod)
                {
                    VirtualMethods.Add(gObjectVirtualMethod);
                }
                else
                {
                    InterfaceVirtualMethods.Add((InterfaceVirtualMethod)virtualMethod);
                }
            }

            if (virtualMethod.CName != "")
                _classMembers.Add(virtualMethod);
        }
    }
}
