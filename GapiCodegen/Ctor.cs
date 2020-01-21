// GtkSharp.Generation.Ctor.cs - The Constructor Generation Class.
//
// Author: Mike Kestner <mkestner@novell.com>
//
// Copyright (c) 2001-2003 Mike Kestner
// Copyright (c) 2004-2005 Novell, Inc.
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
using GapiCodegen.Generatables;
using GapiCodegen.Utils;

namespace GapiCodegen
{
    /// <summary>
    /// Handles 'constructor' elements.
    /// </summary>
    public class Ctor : MethodBase
    {
        private readonly string _name;
        private readonly bool _needsChaining;

        public Ctor(XmlElement element, ClassBase containerType) : base(element, containerType)
        {
            Preferred = element.GetAttributeAsBoolean(Constants.Preferred);

            _needsChaining = containerType is ObjectGen;

            _name = containerType.Name;
        }

        public bool Preferred { get; set; }

        public string StaticName
        {
            get
            {
                if (!IsStatic)
                    return string.Empty;

                if (!string.IsNullOrEmpty(Name))
                    return Name;

                var toks = CName.Substring(CName.IndexOf("new", StringComparison.Ordinal)).Split('_');
                var result = string.Empty;

                foreach (var tok in toks)
                    result += $"{tok.Substring(0, 1).ToUpper()}{tok.Substring(1)}";

                return result;
            }
        }

        public void Generate(GenerationInfo generationInfo)
        {
            var streamWriter = generationInfo.Writer;
            generationInfo.CurrentMember = CName;

            GenerateImport(streamWriter);

            if (IsStatic)
            {
                GenerateStatic(generationInfo);
            }
            else
            {
                streamWriter.WriteLine("\t\t{0} {1}{2} ({3}) {4}", Protection, Safety, _name, Signature,
                    _needsChaining ? ": base (IntPtr.Zero)" : "");
                streamWriter.WriteLine("\t\t{");

                if (_needsChaining)
                {
                    streamWriter.WriteLine($"\t\t\tif (GetType () != typeof ({_name})) {{");

                    if (Parameters.Count == 0)
                    {
                        streamWriter.WriteLine("\t\t\t\tCreateNativeObject (new string [0], new GLib.Value[0]);");
                        streamWriter.WriteLine("\t\t\t\treturn;");
                    }
                    else
                    {
                        var names = new List<string>();
                        var values = new List<string>();

                        foreach (var parameter in Parameters)
                        {
                            if (ContainerType.GetPropertyRecursively(parameter.StudlyName) != null)
                            {
                                names.Add(parameter.Name);
                                values.Add(parameter.Name);
                            }
                            else if (parameter.PropertyName != string.Empty)
                            {
                                names.Add(parameter.PropertyName);
                                values.Add(parameter.Name);
                            }
                        }

                        streamWriter.WriteLine("\t\t\t\tvar vals = new List<GLib.Value> ();");
                        streamWriter.WriteLine("\t\t\t\tvar names = new List<string> ();");

                        for (var i = 0; i < names.Count; i++)
                        {
                            var parameter = Parameters[i];
                            var indent = "\t\t\t\t";

                            if (parameter.Generatable is ClassBase && !(parameter.Generatable is StructBase))
                            {
                                streamWriter.WriteLine($"{indent}if ({parameter.Name} != null) {{");
                                indent += "\t";
                            }

                            streamWriter.WriteLine($"{indent}names.Add (\"{names[i]}\");");
                            streamWriter.WriteLine($"{indent}vals.Add (new GLib.Value ({values[i]}));");

                            if (parameter.Generatable is ClassBase && !(parameter.Generatable is StructBase))
                                streamWriter.WriteLine("\t\t\t\t}");
                        }

                        streamWriter.WriteLine("\t\t\t\tCreateNativeObject (names.ToArray (), vals.ToArray ());");
                        streamWriter.WriteLine("\t\t\t\treturn;");
                    }

                    streamWriter.WriteLine("\t\t\t}");
                }

                Body.Initialize(generationInfo, false, false, "");

                if (ContainerType is ObjectGen)
                {
                    streamWriter.WriteLine("\t\t\towned = true;");
                }

                streamWriter.WriteLine("\t\t\t{0} = {1}({2});", ContainerType.AssignToName, CName, Body.GetCallString(false));
                Body.Finish(streamWriter, "");
                Body.HandleException(streamWriter, "");
            }

            streamWriter.WriteLine("\t\t}");
            streamWriter.WriteLine();

            Statistics.CtorCount++;
        }

        private void GenerateImport(TextWriter textWriter)
        {
            textWriter.WriteLine("\t\t[UnmanagedFunctionPointer (CallingConvention.Cdecl)]");
            textWriter.WriteLine("\t\tdelegate IntPtr d_{0}({1});", CName, Parameters.ImportSignature);
            textWriter.WriteLine(
                "\t\tstatic d_{0} {0} = FuncLoader.LoadFunction<d_{0}>(FuncLoader.GetProcAddress(GLibrary.Load({1}), \"{0}\"));",
                CName, LibraryName);
            textWriter.WriteLine();
        }

        private void GenerateStatic(GenerationInfo generationInfo)
        {
            var streamWriter = generationInfo.Writer;

            streamWriter.WriteLine($"\t\t{Protection} static {Safety}{Modifiers}{_name} {StaticName}({Signature})");
            streamWriter.WriteLine("\t\t{");

            Body.Initialize(generationInfo, false, false, "");

            streamWriter.Write($"\t\t\t{_name} result = ");

            streamWriter.Write(ContainerType is StructBase ? "{0}.New (" : "new {0} (", _name);
            streamWriter.WriteLine($"{CName}({Body.GetCallString(false)}));");

            Body.Finish(streamWriter, "");
            Body.HandleException(streamWriter, "");

            streamWriter.WriteLine("\t\t\treturn result;");
        }
    }
}
