// GtkSharp.Generation.VirtualMethod.cs - The VirtualMethod Generatable.
//
// Author: Mike Kestner <mkestner@novell.com>
//
// Copyright (c) 2003-2004 Novell, Inc.
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

namespace GapiCodegen {
	public abstract class VirtualMethod : MethodBase  {
		protected ReturnValue retval;
		protected ManagedCallString call;
		
		protected string modifiers = "";

		public VirtualMethod (XmlElement element, ObjectBase container_type) : base (element, container_type)
		{
			if (container_type.ParserVersion == 1) {
				// The old pre 2.14 parser didn't drop the 1st parameter in all <signal> and <virtual_method> elements
				Params = new Parameters (element ["parameters"], true);
			}
			retval = new ReturnValue (element ["return-type"]);
		}
	
		protected abstract string CallString {
			get;
		}

		VirtualMethodSignature signature;
		protected new VirtualMethodSignature Signature {
			get {
				if (signature == null)
					signature = new VirtualMethodSignature (Params);

				return signature;
			}
		}

		/* Creates a callback method which invokes the corresponding virtual method
		* @implementor is the class that implements the virtual method(e.g. the class that derives from an interface) or NULL if containing and declaring type are equal
		*/
		public void GenerateCallback (StreamWriter sw, ClassBase implementor)
		{
			LogWriter log = new LogWriter ();
			log.Type = ContainerType.QualifiedName;
			if (!Validate (log))
				return;

			string native_signature = "";
			if (!IsStatic) {
				native_signature += "IntPtr inst";
				if (Params.Count > 0)
					native_signature += ", ";
			}
			if (Params.Count > 0)
				native_signature += Params.ImportSignature;

			sw.WriteLine ("\t\t[UnmanagedFunctionPointer (CallingConvention.Cdecl)]");
			sw.WriteLine ("\t\tdelegate {0} {1}NativeDelegate ({2});", retval.ToNativeType, Name, native_signature);
			sw.WriteLine ();
			sw.WriteLine ("\t\tstatic {0} {1}_cb ({2})", retval.ToNativeType, Name, native_signature);
			sw.WriteLine ("\t\t{");
			string unconditional = call.Unconditional ("\t\t\t");
			if (unconditional.Length > 0)
				sw.WriteLine (unconditional);
			sw.WriteLine ("\t\t\ttry {");

			if (!IsStatic) {
				string type;
				if (implementor != null)
					type = implementor.QualifiedName;
				else if (ContainerType is InterfaceGen)
					// We are in an interface/adaptor, invoke the method in the implementor class
					type = (ContainerType as InterfaceGen).ImplementorName;
				else
					type = ContainerType.Name;

				sw.WriteLine ("\t\t\t\t{0} __obj = GLib.Object.GetObject (inst, false) as {0};", type);
			}

			string indent = "\t\t\t\t";
			if (!retval.IsVoid)
				sw.WriteLine (indent + retval.CSType + " __result;");
			sw.Write (call.Setup (indent));
			sw.Write (indent);
			if (!retval.IsVoid)
				sw.Write ("__result = ");
			if (!IsStatic)
				sw.Write ("__obj.");
			sw.WriteLine (CallString + ";");
			sw.Write (call.Finish (indent));
			if (!retval.IsVoid)
				sw.WriteLine ("\t\t\t\treturn " + retval.ToNative ("__result") + ";");

			bool fatal = Params.HasOutParam || !retval.IsVoid;
			sw.WriteLine ("\t\t\t} catch (Exception e) {");
			sw.WriteLine ("\t\t\t\tGLib.ExceptionManager.RaiseUnhandledException (e, " + (fatal ? "true" : "false") + ");");
			if (fatal) {
				sw.WriteLine ("\t\t\t\t// NOTREACHED: above call does not return.");
				sw.WriteLine ("\t\t\t\tthrow e;");
			}

			if (call.HasDisposeParam) {
				sw.WriteLine ("\t\t\t} finally {");
				sw.Write (call.DisposeParams (indent));
			}
			sw.WriteLine ("\t\t\t}");
			sw.WriteLine ("\t\t}");
			sw.WriteLine ();
		}

		enum ValidState {
			Unvalidated,
			Invalid,
			Valid
		}

		ValidState vstate = ValidState.Unvalidated;

		public override bool Validate (LogWriter logWriter)
		{
			if (vstate != ValidState.Unvalidated)
				return vstate == ValidState.Valid;

			vstate = ValidState.Valid;
			logWriter.Member = Name;
			if (!Params.Validate (logWriter) || !retval.Validate (logWriter)) {
				vstate = ValidState.Invalid;
				return false;
			}

			call = new ManagedCallString (Params);
			return true;
		}
	}
}

