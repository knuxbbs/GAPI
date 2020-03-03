// Copyright (c) 2011 Novell Inc.
//
// Permission is hereby granted, free of charge, to any person obtaining
// a copy of this software and associated documentation files (the
// "Software"), to deal in the Software without restriction, including
// without limitation the rights to use, copy, modify, merge, publish,
// distribute, sublicense, and/or sell copies of the Software, and to
// permit persons to whom the Software is furnished to do so, subject to
// the following conditions:
// 
// The above copyright notice and this permission notice shall be
// included in all copies or substantial portions of the Software.
// 
// THE SOFTWARE IS PROVIDED "AS IS", WITHOUT WARRANTY OF ANY KIND,
// EXPRESS OR IMPLIED, INCLUDING BUT NOT LIMITED TO THE WARRANTIES OF
// MERCHANTABILITY, FITNESS FOR A PARTICULAR PURPOSE AND
// NONINFRINGEMENT. IN NO EVENT SHALL THE AUTHORS OR COPYRIGHT HOLDERS BE
// LIABLE FOR ANY CLAIM, DAMAGES OR OTHER LIABILITY, WHETHER IN AN ACTION
// OF CONTRACT, TORT OR OTHERWISE, ARISING FROM, OUT OF OR IN CONNECTION
// WITH THE SOFTWARE OR THE USE OR OTHER DEALINGS IN THE SOFTWARE.

using System;

namespace GapiCodegen.Utils
{
    public class LogWriter
    {
        private readonly int _level;

        public LogWriter()
        {
            var level = Environment.GetEnvironmentVariable("CODEGEN_DEBUG");

            _level = 1;

            if (level != null)
            {
                _level = int.Parse(level);
            }
        }

        public LogWriter(string type) : this()
        {
            Type = type;
        }

        public string Member { get; set; }

        public string Type { get; set; }

        public void Warn(string warning)
        {
            if (_level > 0)
                Console.WriteLine(
                    $"WARN: {Type}{(!string.IsNullOrEmpty(Member) ? $".{Member}" : string.Empty)} - {warning}");
        }

        public void Info(string info)
        {
            if (_level > 1)
                Console.WriteLine(
                    $"INFO: {Type}{(!string.IsNullOrEmpty(Member) ? $".{Member}" : string.Empty)} - {info}");
        }
    }
}
