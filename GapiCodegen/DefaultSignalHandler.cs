// GtkSharp.Generation.DefaultSignalHandler.cs - The default signal handler generatable
//
// Author: Christian Hoff <christian_hoff@gmx.net>
//
// Copyright (c) 2008 Novell Inc.
// Copyright (c) 2008-2009 Christian Hoff
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
using GapiCodegen.Generatables;
using GapiCodegen.Utils;

namespace GapiCodegen
{
    public class DefaultSignalHandler : GObjectVirtualMethod
    {
        private readonly string _signalName;

        public DefaultSignalHandler(XmlElement element, ObjectBase containerType) : base(element, containerType)
        {
            _signalName = element.GetAttribute(Constants.CName);
        }

        public override string CName => Element.GetAttribute(Constants.FieldName);

        protected override bool CanGenerate(GenerationInfo generationInfo, ObjectBase implementor)
        {
            return true;
        }

        protected override void GenerateOverride(GenerationInfo generationInfo, ObjectBase implementor)
        {
            var streamWriter = generationInfo.Writer;

            if (!base.CanGenerate(generationInfo, implementor))
            {
                GenerateOverrideBody(streamWriter);
                streamWriter.WriteLine("\t\t\tOverrideVirtualMethod (gtype, \"{0}\", callback);", _signalName);
                streamWriter.WriteLine("\t\t}");
            }
            else
                base.GenerateOverride(generationInfo, implementor);
        }

        protected override void GenerateUnmanagedInvocation(GenerationInfo generationInfo, ObjectBase implementor)
        {
            if (!base.CanGenerate(generationInfo, implementor))
            {
                GenerateChainVirtualMethod(generationInfo.Writer, implementor);
            }
            else
                base.GenerateUnmanagedInvocation(generationInfo, implementor);
        }

        private void GenerateChainVirtualMethod(StreamWriter streamWriter, ClassBase implementor)
        {
            GenerateMethodBody(streamWriter, implementor);

            streamWriter.WriteLine(ReturnValue.IsVoid
                ? "\t\t\tGLib.Value ret = GLib.Value.Empty;"
                : $"\t\t\tGLib.Value ret = new GLib.Value ({ReturnGType});");

            streamWriter.WriteLine(
                $"\t\t\tGLib.ValueArray inst_and_params = new GLib.ValueArray ({Parameters.Count + 1});");

            streamWriter.WriteLine($"\t\t\tGLib.Value[] vals = new GLib.Value [{Parameters.Count + 1}];");
            streamWriter.WriteLine("\t\t\tvals [0] = new GLib.Value (this);");
            streamWriter.WriteLine("\t\t\tinst_and_params.Append (vals [0]);");

            var cleanup = string.Empty;

            for (var i = 0; i < Parameters.Count; i++)
            {
                var parameter = Parameters[i];

                if (parameter.PassAs != "")
                {
                    if (SymbolTable.Table.IsBoxed(parameter.CType))
                    {
                        streamWriter.WriteLine(parameter.PassAs == "ref"
                            ? $"\t\t\tvals [{i + 1}] = new GLib.Value ({parameter.Name});"
                            : $"\t\t\tvals [{i + 1}] = new GLib.Value ((GLib.GType)typeof ({parameter.CsType}));");

                        cleanup += $"\t\t\t{parameter.Name} = ({parameter.CsType}) vals [{i}];\n";
                    }
                    else
                    {
                        streamWriter.WriteLine(parameter.PassAs == "ref"
                            ? $"\t\t\tIntPtr {parameter.Name}_ptr = GLib.Marshaller.StructureToPtrAlloc ({parameter.Generatable.CallByName(parameter.Name)});"
                            : $"\t\t\tIntPtr {parameter.Name}_ptr = Marshal.AllocHGlobal (Marshal.SizeOf (typeof ({parameter.MarshalType})));");

                        streamWriter.WriteLine($"\t\t\tvals [{i + 1}] = new GLib.Value ({parameter.Name}_ptr);");

                        cleanup +=
                            $"\t\t\t{parameter.Name} = {parameter.FromNative($"({parameter.MarshalType}) Marshal.PtrToStructure ({parameter.Name}_ptr, typeof ({parameter.MarshalType}))")};\n";
                        cleanup += $"\t\t\tMarshal.FreeHGlobal ({parameter.Name}_ptr);\n";
                    }
                }
                else if (parameter.IsLength && i > 0 && Parameters[i - 1].IsString)
                {
                    streamWriter.WriteLine(
                        $"\t\t\tvals [{i + 1}] = new GLib.Value (System.Text.Encoding.UTF8.GetByteCount ({Parameters[i - 1].Name}));");
                }
                else
                {
                    streamWriter.WriteLine($"\t\t\tvals [{i + 1}] = new GLib.Value ({parameter.Name});");
                }

                streamWriter.WriteLine("\t\t\tinst_and_params.Append (vals [" + (i + 1) + "]);");
            }

            streamWriter.WriteLine("\t\t\tg_signal_chain_from_overridden (inst_and_params.ArrayPtr, ref ret);");

            if (!string.IsNullOrEmpty(cleanup))
            {
                streamWriter.WriteLine(cleanup);
            }

            streamWriter.WriteLine("\t\t\tforeach (GLib.Value v in vals)");
            streamWriter.WriteLine("\t\t\t\tv.Dispose ();");

            if (!ReturnValue.IsVoid)
            {
                var generatable = SymbolTable.Table[ReturnValue.CType];

                streamWriter.WriteLine(
                    $"\t\t\t{ReturnValue.CsType} result = ({(generatable is EnumGen ? $"{ReturnValue.CsType}) (Enum" : ReturnValue.CsType)}) ret;");
                streamWriter.WriteLine("\t\t\tret.Dispose ();");
                streamWriter.WriteLine("\t\t\treturn result;");
            }

            streamWriter.WriteLine("\t\t}\n");
        }

        private string ReturnGType
        {
            get
            {
                switch (SymbolTable.Table[ReturnValue.CType])
                {
                    case ObjectGen _:
                        return "GLib.GType.Object";
                    case BoxedGen _:
                        return $"{ReturnValue.CsType}.GType";
                    case EnumGen _:
                        return $"{ReturnValue.CsType}GType.GType";
                    default:
                        switch (ReturnValue.CsType)
                        {
                            case "bool":
                                return "GLib.GType.Boolean";
                            case "string":
                                return "GLib.GType.String";
                            case "int":
                                return "GLib.GType.Int";
                            default:
                                throw new Exception(ReturnValue.CsType);
                        }
                }
            }
        }
    }
}
