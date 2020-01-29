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
            IsGetter =
                (parameters.IsAccessor && _returnValue.IsVoid || parameters.Count == 0 && !_returnValue.IsVoid) &&
                HasGetterName;
            IsSetter = (parameters.IsAccessor || parameters.VisibleCount == 1 && _returnValue.IsVoid) && HasSetterName;

            _call =
                $"({(IsStatic ? "" : $"{ContainerType.CallByName()}{(parameters.Count > 0 ? ", " : "")}")}{Body.GetCallString(IsSetter)})";

            return true;
        }

        public void GenerateDeclaration(StreamWriter streamWriter)
        {
            if (IsStatic) return;

            if (IsGetter || IsSetter)
            {
                var complement = GetComplement();

                if (complement != null && IsSetter)
                    return;

                streamWriter.Write("\t\t");
                GenerateDeclCommon(streamWriter, null);

                streamWriter.Write("\t\t\t");
                streamWriter.Write(IsGetter ? "get;" : "set;");

                if (complement != null && complement.IsSetter)
                    streamWriter.WriteLine(" set;");
                else
                    streamWriter.WriteLine();

                streamWriter.WriteLine("\t\t}");
            }
            else
            {
                streamWriter.Write("\t\t");
                GenerateDeclCommon(streamWriter, null);
                streamWriter.WriteLine(";");
            }

            Statistics.MethodCount++;
        }

        public void GenerateImport(StreamWriter streamWriter)
        {
            var importSignature = IsStatic ? "" : $"{ContainerType.MarshalType} raw";
            importSignature += !IsStatic && Parameters.Count > 0 ? ", " : "";
            importSignature += Parameters.ImportSignature;

            streamWriter.WriteLine("\t\t[UnmanagedFunctionPointer(CallingConvention.Cdecl)]");

            streamWriter.WriteLine(_returnValue.MarshalType.StartsWith("[return:")
                ? $"\t\tdelegate {_returnValue.CsType} d_{CName}({importSignature});"
                : $"\t\tdelegate {_returnValue.MarshalType} d_{CName}({importSignature});");

            streamWriter.WriteLine(
                "\t\tstatic d_{0} {0} = FuncLoader.LoadFunction<d_{0}>(FuncLoader.GetProcAddress(GLibrary.Load({1}), \"{0}\"));",
                CName, LibraryName);

            streamWriter.WriteLine();
        }

        public void GenerateOverloads(StreamWriter streamWriter)
        {
            streamWriter.WriteLine();
            streamWriter.Write("\t\tpublic ");

            if (IsStatic)
                streamWriter.Write("static ");

            streamWriter.WriteLine(
                $"{_returnValue.CsType} {Name}({(Signature != null ? Signature.WithoutOptional() : "")}) {{");

            streamWriter.WriteLine("\t\t\t{0}{1} ({2});", !_returnValue.IsVoid ? "return " : string.Empty, Name,
                Signature?.CallWithoutOptionals());

            streamWriter.WriteLine("\t\t}");
        }

        public void Generate(GenerationInfo generationInfo, ClassBase implementor)
        {
            Method complement = null;

            /* we are generated by the get Method, if there is one */
            if (IsSetter || IsGetter)
            {
                if (Modifiers != "new " && ContainerType.GetPropertyRecursively(Name.Substring(3)) != null)
                    return;

                complement = GetComplement();

                if (complement != null && IsSetter)
                {
                    if (Parameters.AccessorReturnType == complement.ReturnType)
                        return;

                    _call = string.Format("({0}{1})",
                        IsStatic ? "" : ContainerType.CallByName() + (Parameters.Count > 0 ? ", " : ""),
                        Body.GetCallString(false));

                    complement = null;
                    IsSetter = false;
                }

                /* some setters take more than one arg */
                if (complement != null && !complement.IsSetter)
                    complement = null;
            }

            generationInfo.CurrentMember = Name;

            GenerateImport(generationInfo.Writer);
            if (complement != null && _returnValue.CsType == complement.Parameters.AccessorReturnType)
                complement.GenerateImport(generationInfo.Writer);

            if (IsDeprecated)
                generationInfo.Writer.WriteLine("\t\t[Obsolete]");
            generationInfo.Writer.Write("\t\t");
            if (Protection != "")
                generationInfo.Writer.Write("{0} ", Protection);
            GenerateDeclCommon(generationInfo.Writer, implementor);

            if (IsGetter || IsSetter)
            {
                generationInfo.Writer.Write("\t\t\t");
                generationInfo.Writer.Write(IsGetter ? "get" : "set");
                GenerateBody(generationInfo, implementor, "\t");
            }
            else
                GenerateBody(generationInfo, implementor, "");

            if (IsGetter || IsSetter)
            {
                if (complement != null && _returnValue.CsType == complement.Parameters.AccessorReturnType)
                {
                    generationInfo.Writer.WriteLine();
                    generationInfo.Writer.Write("\t\t\tset");
                    complement.GenerateBody(generationInfo, implementor, "\t");
                }

                generationInfo.Writer.WriteLine();
                generationInfo.Writer.WriteLine("\t\t}");
            }
            else
                generationInfo.Writer.WriteLine();

            if (Parameters.HasOptional && !(IsGetter || IsSetter))
                GenerateOverloads(generationInfo.Writer);

            generationInfo.Writer.WriteLine();

            Statistics.MethodCount++;
        }

        public void GenerateBody(GenerationInfo generationInfo, ClassBase implementor, string indent)
        {
            var streamWriter = generationInfo.Writer;

            streamWriter.WriteLine(" {");

            if (!IsStatic)
                implementor?.Prepare(streamWriter, $"{indent}\t\t\t");

            if (IsAccessor)
                Body.InitAccessor(streamWriter, Signature, indent);

            Body.Initialize(generationInfo, IsGetter, IsSetter, indent);

            streamWriter.Write($"{indent}\t\t\t");

            if (_returnValue.IsVoid)
            {
                streamWriter.WriteLine($"{CName}{_call};");
            }
            else
            {
                streamWriter.WriteLine($"{_returnValue.MarshalType} raw_ret = {CName}{_call};");
                streamWriter.WriteLine(
                    $"{indent}\t\t\t{_returnValue.CsType} ret = {_returnValue.FromNative("raw_ret")};");
            }

            if (!IsStatic)
                implementor?.Finish(streamWriter, $"{indent}\t\t\t");

            Body.Finish(streamWriter, indent);
            Body.HandleException(streamWriter, indent);

            if (IsGetter && Parameters.Count > 0)
            {
                streamWriter.WriteLine($"{indent}\t\t\treturn {Parameters.AccessorName};");
            }
            else if (!_returnValue.IsVoid)
            {
                streamWriter.WriteLine($"{indent}\t\t\treturn ret;");
            }
            else if (IsAccessor)
            {
                Body.FinishAccessor(streamWriter, Signature, indent);
            }

            streamWriter.Write($"{indent}\t\t}}");
        }

        private bool IsAccessor => _returnValue.IsVoid && Signature.IsAccessor;

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
                textWriter.Write(_returnValue.IsVoid ? Parameters.AccessorReturnType : _returnValue.CsType);
                textWriter.Write(" ");

                if (Name.StartsWith("Get") || Name.StartsWith("Set"))
                {
                    textWriter.Write(Name.Substring(3));
                }
                else
                {
                    var dotIndex = Name.LastIndexOf('.');

                    if (dotIndex != -1 && (Name.Substring(dotIndex + 1, 3) == "Get" ||
                                           Name.Substring(dotIndex + 1, 3) == "Set"))
                    {
                        textWriter.Write(Name.Substring(0, dotIndex + 1) + Name.Substring(dotIndex + 4));
                    }
                    else
                        textWriter.Write(Name);
                }

                textWriter.WriteLine(" { ");
            }
            else if (IsAccessor)
            {
                textWriter.Write($"{Signature.AccessorType} {Name}({Signature.AsAccessor})");
            }
            else
            {
                textWriter.Write($"{_returnValue.CsType} {Name}({(Signature != null ? Signature.ToString() : "")})");
            }
        }
    }
}
