using System;
using System.IO;
using Unity.CompilationPipeline.Common.ILPostProcessing;

namespace NetcodePatcher.CodeGen;

public class CompiledAssemblyFromFile : ICompiledAssembly
{
    readonly string _assemblyPath;

    public string Name => Path.GetFileNameWithoutExtension(_assemblyPath);
    public string[] References { get; set; } = Array.Empty<string>();
    public string[] Defines { get; set; } = Array.Empty<string>();
    public InMemoryAssembly InMemoryAssembly { get; }

    public CompiledAssemblyFromFile(string assemblyPath)
    {
        _assemblyPath = assemblyPath;
        byte[] peData = File.ReadAllBytes(assemblyPath);
        string pdbFileName = Path.GetFileNameWithoutExtension(assemblyPath) + ".pdb";

        // if pdb is not found, try reading embedded pdb (?)

        byte[] pdbData = File.ReadAllBytes(Path.Combine(Path.GetDirectoryName(assemblyPath)!, pdbFileName));
        InMemoryAssembly = new InMemoryAssembly(peData, pdbData);
    }
}