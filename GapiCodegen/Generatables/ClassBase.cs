// GtkSharp.Generation.ClassBase.cs - Common code between object
// and interface wrappers
//
// Authors: Rachel Hestilow <hestilow@ximian.com>
//          Mike Kestner <mkestner@speakeasy.net>
//
// Copyright (c) 2002 Rachel Hestilow
// Copyright (c) 2001-2003 Mike Kestner 
// Copyright (c) 2004 Novell, Inc.
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
using System.IO;
using System.Linq;
using System.Xml;
using GapiCodegen.Utils;

namespace GapiCodegen.Generatables
{
    /// <summary>
    /// Abstract base class for types that will be converted to C# classes, structs or interfaces.
    /// </summary>
    public abstract class ClassBase : GenBase
    {
        private readonly IDictionary<string, ObjectField> _fields = new Dictionary<string, ObjectField>();
        private readonly IDictionary<string, Constant> _constants = new Dictionary<string, Constant>();

        protected ClassBase(XmlElement namespaceElement, XmlElement element) : base(namespaceElement, element)
        {
            IsDeprecated = element.GetAttributeAsBoolean(Constants.Deprecated);
            IsAbstract = element.GetAttributeAsBoolean(Constants.Abstract);
            IsAbiFieldsValid = true;

            var parentType = Element.GetAttribute(Constants.Parent);

            var abiFieldsCount = 0;

            foreach (XmlNode node in element.ChildNodes)
            {
                if (!(node is XmlElement)) continue;

                var member = (XmlElement)node;
                StructAbiField abiField = null;

                switch (member.Name)
                {
                    // Make sure ABI fields are taken into account, even when hidden.
                    case Constants.Field:
                        {
                            abiFieldsCount += 1;

                            // Skip instance parent struct if present, taking into account
                            // bindinator broken behaviour concerning parent field (ie.
                            // marking it as pointer, somehow risky but good enough for now.)
                            if (abiFieldsCount != 1 || parentType == "" ||
                                member.GetAttribute(Constants.Type).Replace("*", "") != parentType)
                            {
                                abiField = new StructAbiField(member, this, "abi_info");
                                AbiFields.Add(abiField);
                            }

                            break;
                        }
                    case "union":
                        abiField = new UnionAbiField(member, this, "abi_info");
                        AbiFields.Add(abiField);
                        break;
                }

                if (member.GetAttributeAsBoolean(Constants.Hidden))
                    continue;

                string name;

                switch (member.Name)
                {
                    case Constants.Method:
                        name = member.GetAttribute(Constants.Name);

                        while (Methods.ContainsKey(name))
                            name += "mangled";

                        Methods.Add(name, new Method(member, this));
                        break;

                    case Constants.Property:
                        name = member.GetAttribute(Constants.Name);

                        while (Properties.ContainsKey(name))
                            name += "mangled";

                        Properties.Add(name, new Property(member, this));
                        break;

                    case Constants.Field:
                        //TODO: Generate callbacks.
                        if (member.GetAttributeAsBoolean("is_callback"))
                            continue;

                        name = member.GetAttribute(Constants.Name);

                        while (_fields.ContainsKey(name))
                            name += "mangled";

                        var field = new ObjectField(member, this);
                        field.AbiField = abiField;
                        _fields.Add(name, field);
                        break;

                    case Constants.Implements:
                        ParseImplements(member);
                        break;

                    case Constants.Constructor:
                        Constructors.Add(new Ctor(member, this));
                        break;

                    case "constant":
                        name = member.GetAttribute(Constants.Name);
                        _constants.Add(name, new Constant(member));
                        break;
                }
            }
        }

        public bool IsDeprecated { get; }

        public bool IsAbstract { get; }

        public abstract string AssignToName { get; }

        public override string DefaultValue => "null";

        public IDictionary<string, Method> Methods { get; } = new Dictionary<string, Method>();

        public IDictionary<string, Property> Properties { get; } = new Dictionary<string, Property>();

        public ClassBase Parent
        {
            get
            {
                var parent = Element.GetAttribute(Constants.Parent);

                return parent != "" ? SymbolTable.Table.GetClassGen(parent) : null;
            }
        }

        public IList<Ctor> Constructors = new List<Ctor>();
        protected IList<string> Interfaces = new List<string>();
        protected IList<string> ManagedInterfaces = new List<string>();
        protected IList<StructAbiField> AbiFields = new List<StructAbiField>();

        // false if the instance structure contains a bitfield or fields of unknown types
        protected bool IsAbiFieldsValid;

        public abstract string CallByName();

        public virtual bool CanGenerateAbiStruct(LogWriter logWriter)
        {
            return IsAbiFieldsValid;
        }

        protected void GenerateStructAbi(GenerationInfo generationInfo)
        {
            GenerateStructAbi(generationInfo, null, "abi_info", CName);
        }

        protected void GenerateStructAbi(GenerationInfo generationInfo, IList<StructAbiField> fields,
                string infoName, string structName)
        {
            if (fields == null)
                fields = AbiFields;

            var logWriter = new LogWriter(QualifiedName);

            if (!CheckStructAbiParent(logWriter, out var csParentStruct))
                return;

            var streamWriter = generationInfo.Writer;

            streamWriter.WriteLine();
            streamWriter.WriteLine("\t\t// Internal representation of the wrapped structure ABI.");
            streamWriter.WriteLine($"\t\tstatic GLib.AbiStruct _{infoName} = null;");
            streamWriter.WriteLine(
                $"\t\tstatic public {(!string.IsNullOrEmpty(csParentStruct) ? "new " : "")}GLib.AbiStruct {infoName} {{");
            streamWriter.WriteLine("\t\t\tget {");
            streamWriter.WriteLine($"\t\t\t\tif (_{infoName} == null)");

            // Generate Tests
            var usingParentFields = false;

            if (fields.Count > 0)
            {
                streamWriter.WriteLine($"\t\t\t\t\t_{infoName} = new GLib.AbiStruct (new List<GLib.AbiField>{{ ");

                if (generationInfo.CAbiWriter != null)
                {
                    generationInfo.CAbiWriter.WriteLine(
                        "\tg_print(\"\\\"sizeof({0})\\\": \\\"%\" G_GUINT64_FORMAT \"\\\"\\n\", (guint64) sizeof({0}));",
                        structName);
                    generationInfo.AbiWriter.WriteLine(
                        "\t\t\tConsole.WriteLine(\"\\\"sizeof({0})\\\": \\\"\" + {1}.{2}.{3}.Size + \"\\\"\");",
                        structName, Namespace, Name, infoName);
                }
            }
            else
            {
                if (!string.IsNullOrEmpty(csParentStruct))
                {
                    streamWriter.WriteLine("\t\t\t\t\t_{1} = new GLib.AbiStruct ({0}.{1}.Fields);", csParentStruct, infoName);
                    usingParentFields = true;
                }
                else
                {
                    streamWriter.WriteLine("\t\t\t\t\t_{0} = new GLib.AbiStruct (new List<GLib.AbiField>{{ ", infoName);
                }
            }

            StructAbiField previous = null;
            var fieldAlignmentStructuresWriter = new StringWriter();

            for (var i = 0; i < fields.Count; i++)
            {
                var field = fields[i];
                var next = fields.Count > i + 1 ? fields[i + 1] : null;

                previous = field.Generate(generationInfo, "\t\t\t\t\t", previous, next, csParentStruct,
                    fieldAlignmentStructuresWriter);

                if (field is UnionAbiField || generationInfo.CAbiWriter == null || field.IsBitfield) continue;

                generationInfo.CAbiWriter.WriteLine(
                    "\tg_print(\"\\\"{0}.{1}\\\": \\\"%\" G_GUINT64_FORMAT \"\\\"\\n\", (guint64) G_STRUCT_OFFSET({0}, {1}));",
                    structName, field.CName);
                generationInfo.AbiWriter.WriteLine(
                    "\t\t\tConsole.WriteLine(\"\\\"{0}.{3}\\\": \\\"\" + {1}.{2}.{4}.GetFieldOffset(\"{3}\") + \"\\\"\");",
                    structName, Namespace, Name, field.CName, infoName);
            }

            if (fields.Count > 0 && generationInfo.CAbiWriter != null)
            {
                generationInfo.AbiWriter.Flush();
                generationInfo.CAbiWriter.Flush();
            }

            if (!usingParentFields)
                streamWriter.WriteLine("\t\t\t\t\t});");

            streamWriter.WriteLine();
            streamWriter.WriteLine($"\t\t\t\treturn _{infoName};");
            streamWriter.WriteLine("\t\t\t}");
            streamWriter.WriteLine("\t\t}");
            streamWriter.WriteLine();

            streamWriter.WriteLine(fieldAlignmentStructuresWriter.ToString());
            streamWriter.WriteLine("\t\t// End of the ABI representation.");
            streamWriter.WriteLine();
        }

        public override bool Validate()
        {
            var logWriter = new LogWriter(QualifiedName);

            foreach (var @interface in Interfaces)
            {
                if (SymbolTable.Table[@interface] is InterfaceGen interfaceGen)
                {
                    if (interfaceGen.ValidateForSubclass()) continue;

                    logWriter.Warn($"implements invalid GInterface {@interface}");
                    return false;
                }

                logWriter.Warn($"implements unknown GInterface {@interface}");
                return false;
            }

            foreach (var field in AbiFields)
            {
                if (field.Validate(logWriter))
                {
                    field.SetGetOffsetName();
                }
                else
                {
                    IsAbiFieldsValid = false;
                }
            }

            var invalidProperties =
                Properties.Values.Where(property => !property.Validate(logWriter)).ToArray();

            foreach (var property in invalidProperties)
                Properties.Remove(property.Name);

            var invalidFields =
                _fields.Values.Where(field => !field.Validate(logWriter)).ToArray();

            foreach (var field in invalidFields)
                _fields.Remove(field.Name);

            var invalidMethods =
                Methods.Values.Where(method => !method.Validate(logWriter)).ToArray();

            foreach (var method in invalidMethods)
                Methods.Remove(method.Name);

            var invalidConstants =
                _constants.Values.Where(con => !con.Validate(logWriter)).ToArray();

            foreach (var con in invalidConstants)
                _constants.Remove(con.Name);

            var invalidConstructors =
                Constructors.Where(ctor => !ctor.Validate(logWriter)).ToArray();

            foreach (var ctor in invalidConstructors)
                Constructors.Remove(ctor);

            return true;
        }

        protected virtual bool IsNodeNameHandled(string name)
        {
            switch (name)
            {
                case Constants.Method:
                case Constants.Property:
                case Constants.Field:
                case Constants.Signal:
                case Constants.Implements:
                case Constants.Constructor:
                case Constants.DisableDefaultConstructor:
                case "constant":
                    return true;
                default:
                    return false;
            }
        }

        public void GenerateProperties(GenerationInfo generationInfo, ClassBase implementor)
        {
            if (Properties.Count == 0)
                return;

            foreach (var property in Properties.Values)
                property.Generate(generationInfo, "\t\t", implementor);
        }

        protected void GenerateFields(GenerationInfo generationInfo)
        {
            foreach (var field in _fields.Values)
                field.Generate(generationInfo, "\t\t");
        }

        protected void GenerateConstants(GenerationInfo generationInfo)
        {
            foreach (var con in _constants.Values)
                con.Generate(generationInfo, "\t\t");
        }

        protected bool IgnoreMethod(Method method, ClassBase implementor)
        {
            if (implementor != null && implementor.QualifiedName != QualifiedName && method.IsStatic)
                return true;

            var methodName = method.Name;

            return (method.IsSetter || method.IsGetter && methodName.StartsWith("Get")) &&
                   (Properties != null && Properties.ContainsKey(methodName.Substring(3)) ||
                    _fields != null && _fields.ContainsKey(methodName.Substring(3)));
        }

        public void GenerateMethods(GenerationInfo generationInfo, IDictionary<string, bool> collisions, ClassBase implementor)
        {
            if (Methods == null)
                return;

            foreach (var method in Methods.Values)
            {
                if (IgnoreMethod(method, implementor))
                    continue;

                string methodName = null, methodProtection = null;

                if (collisions != null && collisions.ContainsKey(method.Name))
                {
                    methodName = method.Name;
                    methodProtection = method.Protection;
                    method.Name = $"{QualifiedName}.{method.Name}";
                    method.Protection = string.Empty;
                }

                method.Generate(generationInfo, implementor);

                if (methodName == null) continue;

                method.Name = methodName;
                method.Protection = methodProtection;
            }
        }

        public Method GetMethod(string name)
        {
            Methods.TryGetValue(name, out var method);

            return method;
        }

        public Method GetMethodRecursively(string name)
        {
            return GetMethodRecursively(name, false);
        }

        public virtual Method GetMethodRecursively(string name, bool selfCheck)
        {
            Method method = null;

            if (selfCheck)
                method = GetMethod(name);

            if (method == null && Parent != null)
                method = Parent.GetMethodRecursively(name, true);

            if (!selfCheck || method != null) return method;

            foreach (var @interface in Interfaces)
            {
                var classGen = SymbolTable.Table.GetClassGen(@interface);

                if (classGen == null)
                    continue;

                method = classGen.GetMethodRecursively(name, true);

                if (method != null)
                    break;
            }

            return method;
        }

        public Property GetProperty(string name)
        {
            Properties.TryGetValue(name, out var property);

            return property;
        }

        public virtual Property GetPropertyRecursively(string name)
        {
            var classBase = this;
            Property property = null;

            while (classBase != null && property == null)
            {
                property = classBase.GetProperty(name);
                classBase = classBase.Parent;
            }

            if (property != null) return property;

            foreach (var @interface in Interfaces)
            {
                var classGen = SymbolTable.Table.GetClassGen(@interface);

                if (classGen == null)
                    continue;

                property = classGen.GetPropertyRecursively(name);

                if (property != null)
                    break;
            }

            return property;
        }

        public bool Implements(string @interface)
        {
            if (Interfaces.Contains(@interface))
                return true;

            return Parent != null && Parent.Implements(@interface);
        }

        protected virtual void GenerateConstructors(GenerationInfo generationInfo)
        {
            InitializeConstructors();

            foreach (var ctor in Constructors)
                ctor.Generate(generationInfo);
        }

        public virtual void Finish(StreamWriter sw, string indent)
        {
        }

        public virtual void Prepare(StreamWriter sw, string indent)
        {
        }

        private bool CheckStructAbiParent(LogWriter logWriter, out string csParentStruct)
        {
            csParentStruct = null;

            if (!CanGenerateAbiStruct(logWriter))
                return false;

            var parent = SymbolTable.Table[Element.GetAttribute(Constants.Parent)];
            var csParent = SymbolTable.Table.GetCsType(Element.GetAttribute(Constants.Parent));

            var parentCanBeGenerated = true;

            if (parent != null)
            {
                //TODO: Add that information to ManualGen and use it.
                if (parent.CName == "GInitiallyUnowned" || parent.CName == "GObject")
                {
                    csParentStruct = "GLib.Object";
                }
                else
                {
                    parentCanBeGenerated = false;

                    if (parent is ClassBase classBase)
                    {
                        parentCanBeGenerated = classBase.CheckStructAbiParent(logWriter, out _);
                    }

                    if (parentCanBeGenerated)
                        csParentStruct = csParent;
                }

                if (parentCanBeGenerated) return true;

                logWriter.Warn($"Can't generate ABI structrure as the parent structure '{parent.CName}' can't be generated.");

                return false;
            }

            csParentStruct = string.Empty;

            return true;
        }

        private void ParseImplements(XmlNode member)
        {
            foreach (XmlNode node in member.ChildNodes)
            {
                if (node.Name != Constants.Interface)
                    continue;

                var element = (XmlElement)node;

                if (element.GetAttributeAsBoolean(Constants.Hidden))
                    continue;

                if (element.HasAttribute(Constants.CName))
                {
                    Interfaces.Add(element.GetAttribute(Constants.CName));
                }
                else if (element.HasAttribute(Constants.Name))
                {
                    ManagedInterfaces.Add(element.GetAttribute(Constants.Name));
                }
            }
        }

        private bool HasStaticConstructor(string name)
        {
            if (Parent != null && Parent.HasStaticConstructor(name))
                return true;

            return Constructors.Any(ctor => ctor.StaticName == name);
        }

        private bool _constructorsInitialized;
        
        private void InitializeConstructors()
        {
            if (_constructorsInitialized)
                return;

            Parent?.InitializeConstructors();

            var validCtors = new List<Ctor>();
            var clashMap = new Dictionary<string, Ctor>();

            foreach (var ctor in Constructors)
            {
                if (clashMap.TryGetValue(ctor.Signature.Types, out var clash))
                {
                    var alter = ctor.Preferred ? clash : ctor;
                    alter.IsStatic = true;

                    if (Parent != null && Parent.HasStaticConstructor(alter.StaticName))
                        alter.Modifiers = "new ";
                }
                else
                    clashMap[ctor.Signature.Types] = ctor;

                validCtors.Add(ctor);
            }

            Constructors = validCtors;
            _constructorsInitialized = true;
        }
    }
}
