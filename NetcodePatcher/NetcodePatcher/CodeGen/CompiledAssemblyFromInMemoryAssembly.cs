using System;
using Unity.CompilationPipeline.Common.ILPostProcessing;

namespace NetcodePatcher.CodeGen;

public class CompiledAssemblyFromInMemoryAssembly : ICompiledAssembly
{
    public CompiledAssemblyFromInMemoryAssembly(InMemoryAssembly inMemoryAssembly, string name = "")
    {
        InMemoryAssembly = inMemoryAssembly;
        Name = name;
    }

    public string Name { get; }

    public string[] References { get; set; } = Array.Empty<string>();
    public string[] Defines { get; set; } = Array.Empty<string>();
    public InMemoryAssembly InMemoryAssembly { get; }
}
