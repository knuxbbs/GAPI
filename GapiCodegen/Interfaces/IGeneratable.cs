// GtkSharp.Generation.IGeneratable.cs - Interface to generate code for a type.
//
// Author: Mike Kestner <mkestner@novell.com>
//
// Copyright (c) 2001 Mike Kestner
// Copyright (c) 2007 Novell, Inc.
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

namespace GapiCodegen.Interfaces
{
    /// <summary>
    /// Interface to generate code for a type.
    /// </summary>
    public interface IGeneratable
    {
        /// <summary>
        /// The C name of the generatable.
        /// </summary>
        string CName { get; }

        /// <summary>
        /// The (short) C# name of the generatable.
        /// </summary>
        string Name { get; }

        /// <summary>
        /// The fully-qualified C# name of the generatable.
        /// </summary>
        string QualifiedName { get; }

        /// <summary>
        /// The type (possibly including "ref" or "out") to use in the import
        /// signature when passing this generatable to unmanaged code.
        /// </summary>
        string MarshalType { get; }

        /// <summary>
        /// The value returned by callbacks that are interrupted prematurely
        /// by managed exceptions or other conditions where an appropriate
        /// value can't be otherwise obtained.
        /// </summary>
        string DefaultValue { get; }

        /// <summary>
        /// Generates an expression to convert varName to MarshalType.
        /// </summary>
        /// <param name="varName"></param>
        /// <returns></returns>
        string CallByName(string varName);

        /// <summary>
        /// Generates an expression to convert var from MarshalType.
        /// </summary>
        /// <param name="var"></param>
        /// <returns></returns>
        string FromNative(string var);

        /// <summary>
        /// Generates code to get size of the type.
        /// </summary>
        /// <returns></returns>
        string GenerateGetSizeOf();

        /// <summary>
        /// Generates code to get size of the type.
        /// </summary>
        /// <returns></returns>
        string GenerateAlign();

        bool Validate();

        void Generate();

        void Generate(GenerationInfo generationInfo);
    }
}
