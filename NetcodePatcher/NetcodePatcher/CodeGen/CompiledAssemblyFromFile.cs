#nullable enable

using System;
using System.IO;
using System.Reflection.PortableExecutable;
using NetcodePatcher.Extensions;
using Serilog;
using Unity.CompilationPipeline.Common.ILPostProcessing;

namespace NetcodePatcher.CodeGen;

public class CompiledAssemblyFromFile : ICompiledAssembly
{
    private readonly string _assemblyPath;

    public CompiledAssemblyFromFile(string assemblyPath)
    {
        _assemblyPath = assemblyPath;
        using var peSrcStream = new FileStream(assemblyPath, FileMode.Open, FileAccess.Read);

        var pdbData = ReadPdb(peSrcStream);

        peSrcStream.Seek(0, SeekOrigin.Begin);
        using var peStream = new MemoryStream();
        peSrcStream.CopyTo(peStream);
        var peData = peStream.ToArray();

        InMemoryAssembly = new InMemoryAssembly(peData, pdbData);
    }

    public bool DebugSymbolsAreEmbedded { get; private set; }
    public string? PortableDebugSymbolsFilePath { get; private set; }

    public string Name => Path.GetFileNameWithoutExtension(_assemblyPath);
    public string[] References { get; set; } = Array.Empty<string>();
    public string[] Defines { get; set; } = Array.Empty<string>();
    public InMemoryAssembly InMemoryAssembly { get; }

    public byte[] ReadPdb(FileStream peStream)
    {
        using var peReader = new PEReader(peStream, PEStreamOptions.LeaveOpen);
        var assemblyName = Path.GetFileNameWithoutExtension(_assemblyPath);

    if (!peReader.TryOpenAssociatedPortablePdb(_assemblyPath, File.OpenRead, out var pdbReaderProvider, out var pdbPath))
        throw new InvalidDataException(
            $"Failed to discover portable debug information for {Path.GetFileName(_assemblyPath)}"
        );

        var pdbReader = pdbReaderProvider!.GetMetadataReader();

        if (pdbPath is null)
        {
            Log.Information("Found embedded debug info : ({AssemblyName})", assemblyName);
            DebugSymbolsAreEmbedded = true;
        }
        else
        {
            Log.Information("Found debug info : ({PdbFileName})", Path.GetFileName(pdbPath));
            PortableDebugSymbolsFilePath = pdbPath;
        }

        return pdbReader.ReadAllBytes();
    }
}
