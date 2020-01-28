// GtkSharp.Generation.CodeGenerator.cs - The main code generation engine.
//
// Author: Mike Kestner <mkestner@speakeasy.net>
//
// Copyright (c) 2001-2003 Mike Kestner
// Copyright (c) 2003-2004 Novell Inc.
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
using System.Linq;
using GapiCodegen.Generatables;
using GapiCodegen.Interfaces;
using GapiCodegen.Utils;
using Mono.Options;

namespace GapiCodegen
{
    public class Program
    {
        private static readonly LogWriter Log = new LogWriter("CodeGenerator");

        public static int Main(string[] args)
        {
            var outputDir = "";
            var assemblyName = "";
            var gapiDir = "";
            var abiCsUsings = "";
            var abiCsFile = "";
            var abiCFile = "";
            var glueFilename = "";
            var glueIncludes = "";
            var glueLibName = "";
            var schemaUri = "";
            var showHelp = false;

            var inputFilePaths = new List<string>();
            var includedFilePaths = new List<string>();

            var options = new OptionSet
            {
                {
                    "generate=", "Generate the C# code for this GAPI XML file.",
                    v => { inputFilePaths.Add(v); }
                },
                {
                    "I|include=", "GAPI XML file that contain symbols used in the main GAPI XML file.",
                    v => { includedFilePaths.Add(v); }
                },
                {
                    "outdir=", "Directory where the C# files will be generated.",
                    v => { outputDir = v; }
                },
                {
                    "assembly-name=", "Name of the assembly for which the code is generated.",
                    v => { assemblyName = v; }
                },
                {
                    "gapidir=", "GAPI XML data folder.",
                    v => { gapiDir = v; }
                },
                {
                    "abi-cs-filename=", "Filename for the generated CSharp ABI checker.",
                    v => { abiCsFile = v; }
                },
                {
                    "abi-cs-usings=", "Namespaces to use in the CSharp ABI checker.",
                    v => { abiCsUsings = v; }
                },
                {
                    "abi-c-filename=", "Filename for the generated C ABI checker.",
                    v => { abiCFile = v; }
                },
                {
                    "glue-filename=", "Filename for the generated C glue code.",
                    v => { glueFilename = v; }
                },
                {
                    "glue-includes=", "Content of #include directive to add in the generated C glue code.",
                    v => { glueIncludes = v; }
                },
                {
                    "gluelib-name=",
                    "Name of the C library into which the C glue code will be compiled. Used to generated correct DllImport attributes.",
                    v => { glueLibName = v; }
                },
                {
                    "schema=", "Validate all GAPI XML files against this XSD schema.",
                    v => { schemaUri = v; }
                },
                {
                    "h|help", "Show this message and exit",
                    v => showHelp = v != null
                }
            };

            if (showHelp)
            {
                ShowHelp(options);
                return 0;
            }

            if (inputFilePaths.Count == 0)
            {
                Console.WriteLine("You need to specify a file to process using the --generate option.");
                Console.WriteLine("Try `gapi-codegen --help` for more information.");
                return 64;
            }

            List<string> extra;

            try
            {
                extra = options.Parse(args);
            }
            catch (OptionException e)
            {
                Console.WriteLine($"gapi-codegen: {e.Message}");
                Console.WriteLine("Try `gapi-codegen --help` for more information.");

                return 64;
            }

            if (extra.Exists(v => v.StartsWith("--customdir")))
            {
                Console.WriteLine("Using .custom files is not supported anymore, use partial classes instead.");
                return 64;
            }

            if (!string.IsNullOrEmpty(schemaUri) && !File.Exists(schemaUri))
            {
                Console.WriteLine(
                    $"WARNING: Could not find schema file at '{schemaUri}', no validation will be done.");
                schemaUri = null;
            }

            var parser = new Parser();
            var symbolTable = SymbolTable.Table;

            foreach (var filePath in includedFilePaths)
            {
                Log.Info($"Parsing included gapi: {filePath}");

                var generatables = parser.Parse(filePath, schemaUri, gapiDir);

                symbolTable.AddTypes(generatables);
            }

            var gens = new List<IGeneratable>();

            foreach (var filename in inputFilePaths)
            {
                Log.Info($"Parsing included gapi: {filename}");

                var generatables = parser.Parse(filename, schemaUri, gapiDir);

                symbolTable.AddTypes(generatables);
                gens.AddRange(generatables);
            }

            // Now that everything is loaded, validate all the to-be-
            // generated generatables and then remove the invalid ones.
            var invalids = gens.Where(generatable => !generatable.Validate()).ToArray();

            foreach (var generatable in invalids)
                gens.Remove(generatable);

            GenerationInfo generationInfo = null;

            if (outputDir != "" || assemblyName != "" || glueFilename != "" || glueIncludes != "" || glueLibName != "")
            {
                generationInfo = new GenerationInfo(outputDir, assemblyName, glueFilename, glueIncludes, glueLibName,
                    abiCFile, abiCsFile, abiCsUsings);
            }

            foreach (var generatable in gens)
            {
                if (generationInfo == null)
                    generatable.Generate();
                else
                    generatable.Generate(generationInfo);
            }

            ObjectGen.GenerateMappers();

            generationInfo?.CloseWriters();

            Statistics.Report();
            return 0;
        }

        private static void ShowHelp(OptionSet optionSet)
        {
            Console.WriteLine("Usage: gapi-codegen [OPTIONS]+");
            Console.WriteLine();
            Console.WriteLine("Options:");
            optionSet.WriteOptionDescriptions(Console.Out);
        }
    }
}
