// GtkSharp.Generation.Method.cs - The Method Generatable.
//
// Author: Mike Kestner <mkestner@speakeasy.net>
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

using System.IO;
using System.Xml;
using GapiCodegen.Generatables;
using GapiCodegen.Utils;

namespace GapiCodegen
{
    /// <summary>
    /// Handles 'method' elements.
    /// </summary>
    public class Method : MethodBase
    {
        private readonly ReturnValue _returnValue;
        private string _call;

        public Method(XmlElement element, ClassBase containerType) : base(element, containerType)
        {
            _returnValue = new ReturnValue(element[Constants.ReturnType]);

            if (!containerType.IsDeprecated)
            {
                IsDeprecated = element.GetAttributeAsBoolean(Constants.Deprecated);
            }

            if (Name == "GetType")
                Name = "GetGType";
        }

        public bool IsDeprecated { get; }

        public bool IsGetter { get; private set; }

        public bool IsSetter { get; private set; }

        public string ReturnType => _returnValue.CsType;

        public override bool Validate(LogWriter logWriter)
        {
            logWriter.Member = Name;

            if (!_returnValue.Validate(logWriter) || !base.Validate(logWriter))
                return false;

            if (Name == string.Empty || CName == string.Empty)
            {
                logWriter.Warn("Method has no name or cname.");
                return false;
            }

            var parameters = Parameters;
            IsGetter = (parameters.IsAccessor && _returnValue.IsVoid || parameters.Count == 0 && !_returnValue.IsVoid) && HasGetterName;
            IsSetter = (parameters.IsAccessor || parameters.VisibleCount == 1 && _returnValue.IsVoid) && HasSetterName;

            _call =
                $"({(IsStatic ? "" : $"{ContainerType.CallByName()}{(parameters.Count > 0 ? ", " : "")}")}{Body.GetCallString(IsSetter)})";

            return true;
        }

        private Method GetComplement()
        {
            var complement = IsGetter ? 'S' : 'G';

            return ContainerType.GetMethod($"{complement}{BaseName.Substring(1)}");
        }

        private void GenerateDeclCommon(TextWriter textWriter, ClassBase implementor)
        {
            if (IsStatic)
            {
                textWriter.Write("static ");
            }

            textWriter.Write(Safety);

            Method dup = null;

            if (ContainerType != null)
                dup = ContainerType.GetMethodRecursively(Name);

            if (implementor != null)
                dup = implementor.GetMethodRecursively(Name);

            switch (Name)
            {
                case "ToString" when Parameters.Count == 0 && (!(ContainerType is InterfaceGen) || implementor != null):
                    textWriter.Write("override ");
                    break;
                case "GetGType" when ContainerType is ObjectGen || ContainerType?.Parent != null &&
                                     ContainerType.Parent.Methods.ContainsKey("GetType"):
                    textWriter.Write("new ");
                    break;
                default:
                    {
                        if (Modifiers == "new " || dup != null &&
                            (dup.Signature != null && Signature != null &&
                             dup.Signature.ToString() == Signature.ToString() || 
                             dup.Signature == null && Signature == null))
                            textWriter.Write("new ");

                        break;
                    }
            }

            if (Name.StartsWith(ContainerType.Name))
                Name = Name.Substring(ContainerType.Name.Length);

            if (IsGetter || IsSetter)
            {
                if (_returnValue.IsVoid)
                    textWriter.Write(Parameters.AccessorReturnType);
                else
                    textWriter.Write(_returnValue.CsType);
                textWriter.Write(" ");
                if (Name.StartsWith("Get") || Name.StartsWith("Set"))
                    textWriter.Write(Name.Substring(3));
                else
                {
                    int dot = Name.LastIndexOf('.');
                    if (dot != -1 && (Name.Substring(dot + 1, 3) == "Get" || Name.Substring(dot + 1, 3) == "Set"))
                        textWriter.Write(Name.Substring(0, dot + 1) + Name.Substring(dot + 4));
                    else
                        textWriter.Write(Name);
                }
                textWriter.WriteLine(" { ");
            }
            else if (IsAccessor)
            {
                textWriter.Write(Signature.AccessorType + " " + Name + "(" + Signature.AsAccessor + ")");
            }
            else
            {
                textWriter.Write(_returnValue.CsType + " " + Name + "(" + (Signature != null ? Signature.ToString() : "") + ")");
            }
        }

        public void GenerateDecl(StreamWriter sw)
        {
            if (IsStatic)
                return;

            if (IsGetter || IsSetter)
            {
                Method comp = GetComplement();
                if (comp != null && IsSetter)
                    return;

                sw.Write("\t\t");
                GenerateDeclCommon(sw, null);

                sw.Write("\t\t\t");
                sw.Write(IsGetter ? "get;" : "set;");

                if (comp != null && comp.IsSetter)
                    sw.WriteLine(" set;");
                else
                    sw.WriteLine();

                sw.WriteLine("\t\t}");
            }
            else
            {
                sw.Write("\t\t");
                GenerateDeclCommon(sw, null);
                sw.WriteLine(";");
            }

            Statistics.MethodCount++;
        }

        public void GenerateImport(StreamWriter sw)
        {
            string import_sig = IsStatic ? "" : ContainerType.MarshalType + " raw";
            import_sig += !IsStatic && Parameters.Count > 0 ? ", " : "";
            import_sig += Parameters.ImportSignature.ToString();

            sw.WriteLine("\t\t[UnmanagedFunctionPointer (CallingConvention.Cdecl)]");

            if (_returnValue.MarshalType.StartsWith("[return:"))
                sw.WriteLine("\t\tdelegate " + _returnValue.CsType + " d_" + CName + "(" + import_sig + ");");
            else
                sw.WriteLine("\t\tdelegate " + _returnValue.MarshalType + " d_" + CName + "(" + import_sig + ");");
            sw.WriteLine("\t\tstatic d_" + CName + " " + CName + " = FuncLoader.LoadFunction<d_" + CName + ">(FuncLoader.GetProcAddress(GLibrary.Load(" + LibraryName + "), \"" + CName + "\"));");
            sw.WriteLine();
        }

        public void GenerateOverloads(StreamWriter sw)
        {
            sw.WriteLine();
            sw.Write("\t\tpublic ");
            if (IsStatic)
                sw.Write("static ");
            sw.WriteLine(_returnValue.CsType + " " + Name + "(" + (Signature != null ? Signature.WithoutOptional() : "") + ") {");
            sw.WriteLine("\t\t\t{0}{1} ({2});", !_returnValue.IsVoid ? "return " : string.Empty, Name, Signature.CallWithoutOptionals());
            sw.WriteLine("\t\t}");
        }

        public void Generate(GenerationInfo gen_info, ClassBase implementor)
        {
            Method comp = null;

            gen_info.CurrentMember = Name;

            /* we are generated by the get Method, if there is one */
            if (IsSetter || IsGetter)
            {
                if (Modifiers != "new " && ContainerType.GetPropertyRecursively(Name.Substring(3)) != null)
                    return;
                comp = GetComplement();
                if (comp != null && IsSetter)
                {
                    if (Parameters.AccessorReturnType == comp.ReturnType)
                        return;
                    else
                    {
                        IsSetter = false;
                        _call = "(" + (IsStatic ? "" : ContainerType.CallByName() + (Parameters.Count > 0 ? ", " : "")) + Body.GetCallString(false) + ")";
                        comp = null;
                    }
                }
                /* some setters take more than one arg */
                if (comp != null && !comp.IsSetter)
                    comp = null;
            }

            GenerateImport(gen_info.Writer);
            if (comp != null && _returnValue.CsType == ((MethodBase)comp).Parameters.AccessorReturnType)
                comp.GenerateImport(gen_info.Writer);

            if (IsDeprecated)
                gen_info.Writer.WriteLine("\t\t[Obsolete]");
            gen_info.Writer.Write("\t\t");
            if (Protection != "")
                gen_info.Writer.Write("{0} ", Protection);
            GenerateDeclCommon(gen_info.Writer, implementor);

            if (IsGetter || IsSetter)
            {
                gen_info.Writer.Write("\t\t\t");
                gen_info.Writer.Write(IsGetter ? "get" : "set");
                GenerateBody(gen_info, implementor, "\t");
            }
            else
                GenerateBody(gen_info, implementor, "");

            if (IsGetter || IsSetter)
            {
                if (comp != null && _returnValue.CsType == ((MethodBase)comp).Parameters.AccessorReturnType)
                {
                    gen_info.Writer.WriteLine();
                    gen_info.Writer.Write("\t\t\tset");
                    comp.GenerateBody(gen_info, implementor, "\t");
                }
                gen_info.Writer.WriteLine();
                gen_info.Writer.WriteLine("\t\t}");
            }
            else
                gen_info.Writer.WriteLine();

            if (Parameters.HasOptional && !(IsGetter || IsSetter))
                GenerateOverloads(gen_info.Writer);

            gen_info.Writer.WriteLine();

            Statistics.MethodCount++;
        }

        public void GenerateBody(GenerationInfo gen_info, ClassBase implementor, string indent)
        {
            StreamWriter sw = gen_info.Writer;
            sw.WriteLine(" {");
            if (!IsStatic && implementor != null)
                implementor.Prepare(sw, indent + "\t\t\t");
            if (IsAccessor)
                Body.InitAccessor(sw, Signature, indent);
            Body.Initialize(gen_info, IsGetter, IsSetter, indent);

            sw.Write(indent + "\t\t\t");
            if (_returnValue.IsVoid)
                sw.WriteLine(CName + _call + ";");
            else
            {
                sw.WriteLine(_returnValue.MarshalType + " raw_ret = " + CName + _call + ";");
                sw.WriteLine(indent + "\t\t\t" + _returnValue.CsType + " ret = " + _returnValue.FromNative("raw_ret") + ";");
            }

            if (!IsStatic && implementor != null)
                implementor.Finish(sw, indent + "\t\t\t");
            Body.Finish(sw, indent);
            Body.HandleException(sw, indent);

            if (IsGetter && Parameters.Count > 0)
                sw.WriteLine(indent + "\t\t\treturn " + Parameters.AccessorName + ";");
            else if (!_returnValue.IsVoid)
                sw.WriteLine(indent + "\t\t\treturn ret;");
            else if (IsAccessor)
                Body.FinishAccessor(sw, Signature, indent);

            sw.Write(indent + "\t\t}");
        }

        private bool IsAccessor => _returnValue.IsVoid && Signature.IsAccessor;
    }
}
