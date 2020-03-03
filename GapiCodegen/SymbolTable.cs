// GtkSharp.Generation.SymbolTable.cs - The Symbol Table Class.
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

using System.Collections.Generic;
using GapiCodegen.Generatables;
using GapiCodegen.Interfaces;
using GapiCodegen.Utils;

namespace GapiCodegen
{
    /// <summary>
    /// Keeps track of the type hierarchy and the mappings between C types and IGeneratable classes.
    /// </summary>
    public class SymbolTable
    {
        private static readonly LogWriter Log = new LogWriter("SymbolTable");
        private readonly IDictionary<string, IGeneratable> _types = new Dictionary<string, IGeneratable>();

        private static SymbolTable _table;

        public static SymbolTable Table => _table ?? (_table = new SymbolTable());

        public SymbolTable()
        {
            // Simple easily mapped types
            AddType(new SimpleGen("void", "void", string.Empty));
            AddType(new SimpleGen("gpointer", "IntPtr", "IntPtr.Zero"));
            AddType(new SimpleGen("AtkFunction", "IntPtr", "IntPtr.Zero")); // function definition used for padding
            AddType(new SimpleGen("gboolean", "bool", "false"));
            AddType(new SimpleGen("gint", "int", "0"));
            AddType(new SimpleGen("guint", "uint", "0"));
            AddType(new SimpleGen("int", "int", "0"));
            AddType(new SimpleGen("unsigned", "uint", "0"));
            AddType(new SimpleGen("unsigned int", "uint", "0"));
            AddType(new SimpleGen("unsigned-int", "uint", "0"));
            AddType(new SimpleGen("gshort", "short", "0"));
            AddType(new SimpleGen("gushort", "ushort", "0"));
            AddType(new SimpleGen("short", "short", "0"));
            AddType(new SimpleGen("guchar", "byte", "0"));
            AddType(new SimpleGen("unsigned char", "byte", "0"));
            AddType(new SimpleGen("unsigned-char", "byte", "0"));
            AddType(new SimpleGen("guint1", "bool", "false"));
            AddType(new SimpleGen("uint1", "bool", "false"));
            AddType(new SimpleGen("gint8", "sbyte", "0"));
            AddType(new SimpleGen("guint8", "byte", "0"));
            AddType(new SimpleGen("gint16", "short", "0"));
            AddType(new SimpleGen("guint16", "ushort", "0"));
            AddType(new SimpleGen("gint32", "int", "0"));
            AddType(new SimpleGen("guint32", "uint", "0"));
            AddType(new SimpleGen("gint64", "long", "0"));
            AddType(new SimpleGen("guint64", "ulong", "0"));
            AddType(new SimpleGen("unsigned long long", "ulong", "0"));
            AddType(new SimpleGen("long long", "long", "0"));
            AddType(new SimpleGen("gfloat", "float", "0.0"));
            AddType(new SimpleGen("float", "float", "0.0"));
            AddType(new SimpleGen("gdouble", "double", "0.0"));
            AddType(new SimpleGen("double", "double", "0.0"));
            AddType(new SimpleGen("goffset", "long", "0"));
            AddType(new SimpleGen("GQuark", "int", "0"));

            // platform specific integer types.
#if WIN64LONGS
			AddType (new SimpleGen ("long", "int", "0"));
			AddType (new SimpleGen ("glong", "int", "0"));
			AddType (new SimpleGen ("ulong", "uint", "0"));
			AddType (new SimpleGen ("gulong", "uint", "0"));
			AddType (new SimpleGen ("unsigned long", "uint", "0"));
			AddType (new SimpleGen ("gintptr", "int", "0"));
			AddType (new SimpleGen ("guintptr", "uint", "0"));
#else
            AddType(new Int64Gen("long"));
            AddType(new Int64Gen("glong"));
            AddType(new Int64Gen("gintptr"));
            AddType(new UInt64Gen("ulong"));
            AddType(new UInt64Gen("gulong"));
            AddType(new UInt64Gen("unsigned long"));
            AddType(new UInt64Gen("guintptr"));
#endif

            AddType(new Int64Gen("ssize_t"));
            AddType(new Int64Gen("gssize"));
            AddType(new UInt64Gen("size_t"));
            AddType(new UInt64Gen("gsize"));

#if OFF_T_8
			AddType (new AliasGen ("off_t", "long"));
#else
            AddType(new Int64Gen("off_t"));
#endif

            // string types
            AddType(new ConstStringGen("const-gchar"));
            AddType(new ConstStringGen("const-xmlChar"));
            AddType(new ConstStringGen("const-char"));
            AddType(new ConstFilenameGen("const-gfilename"));
            AddType(new MarshalGen("gfilename", "string", "IntPtr", "GLib.Marshaller.StringToFilenamePtr({0})",
                "GLib.Marshaller.FilenamePtrToStringGFree({0})"));
            AddType(new MarshalGen("gchar", "string", "IntPtr", "GLib.Marshaller.StringToPtrGStrdup({0})",
                "GLib.Marshaller.PtrToStringGFree({0})"));
            AddType(new MarshalGen("char", "string", "IntPtr", "GLib.Marshaller.StringToPtrGStrdup({0})",
                "GLib.Marshaller.PtrToStringGFree({0})"));
            AddType(new SimpleGen("GStrv", "string[]", "null"));

            // manually wrapped types requiring more complex marshaling
            AddType(new ManualGen("GInitiallyUnowned", "GLib.InitiallyUnowned", "GLib.Object.GetObject ({0})"));
            AddType(new ManualGen("GObject", "GLib.Object", "GLib.Object.GetObject ({0})"));
            AddType(new ManualGen("GList", "GLib.List"));
            AddType(new ManualGen("GPtrArray", "GLib.PtrArray"));
            AddType(new ManualGen("GSList", "GLib.SList"));
            AddType(new ManualGen("GVariant", "GLib.Variant"));
            AddType(new ManualGen("GVariantType", "GLib.VariantType"));
            AddType(new ManualGen("GValueArray", "GLib.ValueArray"));

            AddType(new ManualGen("GMutex", "GLib.Mutex",
                "new GLib.Mutex({0})",
                "GLib.Mutex.ABI"));

            AddType(new ManualGen("GRecMutex",
                "GLib.RecMutex",
                "new GLib.RecMutex({0})",
                "GLib.RecMutex.ABI"));

            AddType(new ManualGen("GCond", "GLib.Cond",
                "new GLib.Cond({0})",
                "GLib.Cond.ABI"));

            AddType(new ManualGen("GDateTime", "GLib.DateTime"));
            AddType(new ManualGen("GDate", "GLib.Date"));
            AddType(new ManualGen("GSource", "GLib.Source"));
            AddType(new ManualGen("GMainContext", "GLib.MainContext"));
            AddType(new SimpleGen("GPollFD", "GLib.PollFD", "GLib.PollFD.Zero"));

            AddType(new MarshalGen("gunichar", "char", "uint", "GLib.Marshaller.CharToGUnichar ({0})",
                "GLib.Marshaller.GUnicharToChar ({0})"));

            AddType(new MarshalGen("time_t", "System.DateTime", "IntPtr", "GLib.Marshaller.DateTimeTotime_t ({0})",
                "GLib.Marshaller.time_tToDateTime ({0})"));

            AddType(new MarshalGen("GString", "string", "IntPtr", "new GLib.GString ({0}).Handle",
                "GLib.GString.PtrToString ({0})"));

            AddType(
                new MarshalGen("GType", "GLib.GType", "IntPtr", "{0}.Val", "new GLib.GType({0})", "GLib.GType.None"));

            AddType(new ByRefGen("GValue", "GLib.Value"));

            AddType(new SimpleGen("GDestroyNotify", "GLib.DestroyNotify", "null",
                "(uint) Marshal.SizeOf(typeof(IntPtr))"));

            AddType(new SimpleGen("GThread", "GLib.Thread", "null"));
            AddType(new ManualGen("GBytes", "GLib.Bytes"));
            AddType(new SimpleGen("GHookList", "GLib.HookList", "null",
                "GLib.HookList.abi_info.Size"));

            //TODO: FIXME: These ought to be handled properly.
            AddType(new SimpleGen("GC", "IntPtr", "IntPtr.Zero"));
            AddType(new SimpleGen("GError", "IntPtr", "IntPtr.Zero"));
            AddType(new SimpleGen("GMemChunk", "IntPtr", "IntPtr.Zero"));
            AddType(new SimpleGen("GTimeVal", "IntPtr", "IntPtr.Zero"));
            AddType(new SimpleGen("GClosure", "IntPtr", "IntPtr.Zero"));
            AddType(new SimpleGen("GArray", "IntPtr", "IntPtr.Zero"));
            AddType(new SimpleGen("GByteArray", "IntPtr", "IntPtr.Zero"));
            AddType(new SimpleGen("GData", "IntPtr", "IntPtr.Zero"));
            AddType(new SimpleGen("GIOChannel", "IntPtr", "IntPtr.Zero"));
            AddType(new SimpleGen("GTypeModule", "GLib.Object", "null"));
            AddType(new SimpleGen("GHashTable", "System.IntPtr", "IntPtr.Zero"));
            AddType(new SimpleGen("va_list", "IntPtr", "IntPtr.Zero"));
            AddType(new SimpleGen("GParamSpec", "IntPtr", "IntPtr.Zero"));
            AddType(new SimpleGen("gconstpointer", "IntPtr", "IntPtr.Zero"));
            AddType(new SimpleGen("GBoxedCopyFunc", "IntPtr", "IntPtr.Zero"));
            AddType(new SimpleGen("GBoxedFreeFunc", "IntPtr", "IntPtr.Zero"));
            AddType(new SimpleGen("GHookFinalizeFunc", "IntPtr", "IntPtr.Zero"));
        }

        public void AddType(IGeneratable generatable)
        {
            Log.Info($"Adding {generatable.CName} = {generatable}");

            _types[generatable.CName] = generatable;
        }

        public void AddTypes(IGeneratable[] generatables)
        {
            foreach (var generatable in generatables)
                AddType(generatable);
        }

        public IGeneratable this[string ctype] => DeAlias(ctype);

        public string FromNative(string cType, string val)
        {
            var generatable = this[cType];

            return generatable != null ? generatable.FromNative(val) : string.Empty;
        }

        public string GetCsType(string cType, bool isDefaultPointer)
        {
            var generatable = this[cType];

            if (generatable != null) return generatable.QualifiedName;

            if (cType.EndsWith("*") && isDefaultPointer)
                return "IntPtr";

            return string.Empty;
        }

        public string GetCsType(string cType)
        {
            return GetCsType(cType, false);
        }

        public string GetMarshalType(string cType)
        {
            var generatable = this[cType];

            return generatable != null ? generatable.MarshalType : string.Empty;
        }

        public ClassBase GetClassGen(string cType)
        {
            return this[cType] as ClassBase;
        }

        public InterfaceGen GetInterfaceGen(string cType)
        {
            return this[cType] as InterfaceGen;
        }

        public string CallByName(string cType, string varName)
        {
            var generatable = this[cType];

            return generatable != null ? generatable.CallByName(varName) : string.Empty;
        }

        public bool IsOpaque(string cType)
        {
            return this[cType] is OpaqueGen;
        }

        public bool IsBoxed(string cType)
        {
            return this[cType] is BoxedGen;
        }

        public bool IsEnum(string cType)
        {
            return this[cType] is EnumGen;
        }

        public bool IsInterface(string cType)
        {
            return this[cType] is InterfaceGen;
        }

        public bool IsObject(string cType)
        {
            return this[cType] is ObjectGen;
        }

        public string MangleName(string name)
        {
            switch (name)
            {
                case "string":
                    return "str1ng";
                case "event":
                    return "evnt";
                case "null":
                    return "is_null";
                case "object":
                    return "objekt";
                case "params":
                    return "parms";
                case "ref":
                    return "reference";
                case "in":
                    return "in_param";
                case "out":
                    return "out_param";
                case "fixed":
                    return "mfixed";
                case "byte":
                    return "_byte";
                case "new":
                    return "_new";
                case "base":
                    return "_base";
                case "lock":
                    return "_lock";
                case "callback":
                    return "cb";
                case "readonly":
                    return "read_only";
                case "interface":
                    return "iface";
                case "internal":
                    return "_internal";
                case "where":
                    return "wh3r3";
                case "foreach":
                    return "for_each";
                case "remove":
                    return "_remove";
            }

            return name;
        }

        private IGeneratable DeAlias(string type)
        {
            IGeneratable currentType;

            type = Trim(type);

            while (_types.TryGetValue(type, out currentType) && currentType is AliasGen aliasGen)
            {
                if (_types.TryGetValue(aliasGen.Name, out var newType))
                {
                    _types[type] = newType;
                }

                type = aliasGen.Name;
            }

            return currentType;
        }

        private static string Trim(string type)
        {
            // HACK: If we don't detect this here, there is no
            // way of indicating it in the symbol table
            if (type == "void*" || type == "const-void*") return "gpointer";

            var trimmedType = type.TrimEnd('*');

            if (IsStringConstant(trimmedType))
                return trimmedType;

            return trimmedType.StartsWith("const-")
                ? trimmedType.Substring(6)
                : trimmedType;
        }

        private static bool IsStringConstant(string type)
        {
            switch (type)
            {
                case "const-gchar":
                case "const-char":
                case "const-xmlChar":
                case "const-gfilename":
                    return true;
                default:
                    return false;
            }
        }
    }
}
