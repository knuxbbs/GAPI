// Statistics.cs : Generation statistics class implementation
//
// Author: Mike Kestner  <mkestner@ximian.com>
//
// Copyright (c) 2002 Mike Kestner
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

using System;

namespace GapiCodegen
{
    /// <summary>
    /// Used to keep statistics on generated classes.
    /// </summary>
    public static class Statistics
    {
        public static int CallbackCount { get; set; } = 0;

        public static int EnumCount { get; set; } = 0;

        public static int ObjectCount { get; set; } = 0;

        public static int StructCount { get; set; } = 0;

        public static int BoxedCount { get; set; } = 0;

        public static int OpaqueCount { get; set; } = 0;

        public static int CtorCount { get; set; } = 0;

        public static int MethodCount { get; set; } = 0;

        public static int PropCount { get; set; } = 0;

        public static int SignalCount { get; set; } = 0;

        public static int InterfaceCount { get; set; } = 0;

        public static int ThrottledCount { get; set; } = 0;

        public static int IgnoreCount { get; set; } = 0;

        public static bool VirtualMethodsIgnored { get; set; }

        public static void Report()
        {
            if (VirtualMethodsIgnored)
            {
                Console.WriteLine();
                Console.WriteLine("Warning: Generation throttled for Virtual Methods.");
                Console.WriteLine("  Consider regenerating with --gluelib-name and --glue-filename.");
            }

            Console.WriteLine();
            Console.WriteLine("Generation Summary:");
            Console.Write($"  Enums: {EnumCount}");
            Console.Write($"  Structs: {StructCount}");
            Console.Write($"  Boxed: {BoxedCount}");
            Console.Write($"  Opaques: {OpaqueCount}");
            Console.Write($"  Interfaces: {InterfaceCount}");
            Console.Write($"  Objects: {ObjectCount}");
            Console.WriteLine($"  Callbacks: {CallbackCount}");
            Console.Write($"  Properties: {PropCount}");
            Console.Write($"  Signals: {SignalCount}");
            Console.Write($"  Methods: {MethodCount}");
            Console.Write($"  Constructors: {CtorCount}");
            Console.WriteLine($"  Throttled: {ThrottledCount}");
            Console.WriteLine(
                $"Total Nodes: {EnumCount + StructCount + BoxedCount + OpaqueCount + InterfaceCount + CallbackCount + ObjectCount + PropCount + SignalCount + MethodCount + CtorCount + ThrottledCount}");
            Console.WriteLine();
        }
    }
}