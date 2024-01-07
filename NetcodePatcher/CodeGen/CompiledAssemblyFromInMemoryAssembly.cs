using System;
using Unity.CompilationPipeline.Common.ILPostProcessing;

namespace NetcodePatcher.CodeGen
{
    public class CompiledAssemblyFromInMemoryAssembly : ICompiledAssembly
    {
        readonly string _assemblyName;
        public string Name => _assemblyName;
        public string[] References { get; set; } = Array.Empty<string>();
        public string[] Defines { get; set; } = Array.Empty<string>();
        public InMemoryAssembly InMemoryAssembly { get; }

        public CompiledAssemblyFromInMemoryAssembly(InMemoryAssembly inMemoryAssembly, string name = "")
        {
            InMemoryAssembly = inMemoryAssembly;
            _assemblyName = name;
        }

    }
}
