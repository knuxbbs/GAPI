// GtkSharp.Generation.InterfaceVM.cs - interface-specific part of VM creation
//
// Author: Christian Hoff <christian_hoff@gmx.net>
//
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

using System.IO;
using System.Xml;
using GapiCodegen.Generatables;
using GapiCodegen.Utils;

namespace GapiCodegen
{
    public class InterfaceVirtualMethod : VirtualMethod
    {
        private Method target;

        public InterfaceVirtualMethod(XmlElement element, Method target, ObjectBase container_type) : base(element, container_type)
        {
            this.target = target;
            Parameters.HideData = true;
            Protection = "public";
        }

        public bool IsGetter
        {
            get
            {
                return HasGetterName && (!ReturnValue.IsVoid && Parameters.Count == 0 || ReturnValue.IsVoid && Parameters.Count == 1 && Parameters[0].PassAs == "out");
            }
        }

        public bool IsSetter
        {
            get
            {
                if (!HasSetterName || !ReturnValue.IsVoid)
                    return false;

                if (Parameters.Count == 1 || Parameters.Count == 3 && Parameters[0].Scope == "notified")
                    return true;
                else
                    return false;
            }
        }

        protected override string CallString
        {
            get
            {
                if (IsGetter)
                    return target.Name.StartsWith("Get") ? target.Name.Substring(3) : target.Name;
                else if (IsSetter)
                    return target.Name.Substring(3) + " = " + call;
                else
                    return target.Name + " (" + call + ")";
            }
        }

        public void GenerateDeclaration(StreamWriter sw, InterfaceVirtualMethod complement)
        {
            if (IsGetter)
            {
                string name = Name.StartsWith("Get") ? Name.Substring(3) : Name;
                string type = ReturnValue.IsVoid ? Parameters[0].CsType : ReturnValue.CsType;
                if (complement != null && complement.Parameters[0].CsType == type)
                    sw.WriteLine("\t\t" + type + " " + name + " { get; set; }");
                else
                {
                    sw.WriteLine("\t\t" + type + " " + name + " { get; }");
                    if (complement != null)
                        sw.WriteLine("\t\t" + complement.ReturnValue.CsType + " " + complement.Name + " (" + complement.Signature + ");");
                }
            }
            else if (IsSetter)
                sw.WriteLine("\t\t" + Parameters[0].CsType + " " + Name.Substring(3) + " { set; }");
            else
                sw.WriteLine("\t\t" + ReturnValue.CsType + " " + Name + " (" + Signature + ");");
        }

        public override bool Validate(LogWriter logWriter)
        {
            if (!base.Validate(logWriter))
                return false;

            if (target == null && !(ContainerType as InterfaceGen).IsConsumeOnly)
            {
                logWriter.Warn("No matching target method to invoke. Add target_method attribute with fixup.");
                return false;
            }

            return true;
        }
    }
}
