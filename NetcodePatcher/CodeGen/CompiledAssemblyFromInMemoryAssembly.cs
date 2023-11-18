using System;
using System.Collections.Generic;
using System.IO;
using System.Text;
using Unity.CompilationPipeline.Common.ILPostProcessing;

namespace NetcodePatcher.CodeGen
{
    public class CompiledAssemblyFromInMemoryAssembly : ICompiledAssembly
    {
        readonly string _assemblyName;
        public string Name => _assemblyName;
        public string[] References { get; set; }
        public string[] Defines { get; set; }
        public InMemoryAssembly InMemoryAssembly { get; }

        public CompiledAssemblyFromInMemoryAssembly(InMemoryAssembly inMemoryAssembly, string name = "")
        {
            InMemoryAssembly = inMemoryAssembly;
            _assemblyName = name;
        }

    }
}
