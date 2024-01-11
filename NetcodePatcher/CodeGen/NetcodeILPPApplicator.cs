using System;
using System.IO;
using System.Linq;
using Mono.Cecil;
using Mono.Cecil.Cil;
using Serilog;
using Unity.CompilationPipeline.Common.Diagnostics;
using Unity.CompilationPipeline.Common.ILPostProcessing;
using Unity.Netcode.Editor.CodeGen;

namespace NetcodePatcher.CodeGen;

public class NetcodeILPPApplicator
{
    public NetcodeILPPApplicator(string assemblyPath, string outputPath, string[] references)
    {
        AssemblyPath = assemblyPath;
        OutputPath = outputPath;
        References = references;
    }

    public Action<string> OnWarning { get; set; } = _ => { };
    public Action<string> OnError { get; set; } = _ => { };

    private string AssemblyPath { get; }
    private string OutputPath { get; }
    private string[] References { get; }

    private string AssemblyName => Path.GetFileNameWithoutExtension(AssemblyPath);
    private string AssemblyFileName => Path.GetFileName(AssemblyPath);
    private string AssemblyDirName => Path.GetDirectoryName(AssemblyPath)!;

    public static bool HasNetcodePatchedAttribute(ICompiledAssembly assembly)
    {
        // read
        var assemblyDefinition = CodeGenHelpers.AssemblyDefinitionFor(assembly, out _);
        if (assemblyDefinition is null) return false;

        return assemblyDefinition.CustomAttributes.Any(
            attribute => attribute.Constructor.DeclaringType.FullName.EndsWith(
                $".{ApplyPatchedAttributeILPP.AttributeNamespaceSuffix}.{ApplyPatchedAttributeILPP.AttributeName}")
        );
    }

    public void ApplyProcesses()
    {
        Log.Information("Reading : {FileName}", Path.GetFileName(AssemblyPath));

        CompiledAssemblyFromFile assemblyFromFile;
        try
        {
            // read the original assembly from file
            assemblyFromFile = new CompiledAssemblyFromFile(AssemblyPath)
            {
                References = References
            };
        }
        catch (InvalidDataException)
        {
            Log.Error("Couldn't find debug information for ({AssemblyFileName}), forced to skip", AssemblyFileName);
            return;
        }

        var debugSymbolsAreEmbedded = assemblyFromFile.DebugSymbolsAreEmbedded;
        var pdbPath = assemblyFromFile.PortableDebugSymbolsFilePath;
        ICompiledAssembly assembly = assemblyFromFile;

        if (HasNetcodePatchedAttribute(assembly))
        {
            Log.Warning("Skipping {FileName} as it has already been patched.", Path.GetFileName(AssemblyPath));
            return;
        }

        Log.Information("Patching : {FileName}", Path.GetFileName(AssemblyPath));

        string? renameAssemblyPath = null;
        string? renamePdbPath = null;

        if (AssemblyPath == OutputPath)
        {
            // remove files with _original.dll and _original.pdb

            renameAssemblyPath = Path.Combine(AssemblyDirName, $"{AssemblyName}_original.dll");
            if (File.Exists(renameAssemblyPath))
            {
                Log.Information("Deleting : {FileName}", Path.GetFileName(renameAssemblyPath));
                File.Delete(renameAssemblyPath);
            }
            File.Move(AssemblyPath, renameAssemblyPath);

            if (pdbPath is not null)
            {
                renamePdbPath = Path.Combine(Path.GetDirectoryName(pdbPath)!, $"{Path.GetFileNameWithoutExtension(pdbPath)}_original.pdb");
                if (File.Exists(renamePdbPath))
                {
                    Log.Information("Deleting : {FileName}", Path.GetFileName(renamePdbPath));
                    File.Delete(renamePdbPath);
                }
                File.Move(pdbPath, renamePdbPath);
            }
        }

        ICompiledAssembly ApplyProcess<TProcessor>(ICompiledAssembly assemblyToApplyProcessTo)
            where TProcessor : ILPostProcessor, new()
        {
            var ilpp = new TProcessor();
            if (!ilpp.WillProcess(assembly)) return assemblyToApplyProcessTo;

            var result = ilpp.Process(assembly);

            if (result is null)
                return assemblyToApplyProcessTo;

            // handle the error messages like Unity would
            foreach (var message in result.Diagnostics)
                switch (message.DiagnosticType)
                {
                    case DiagnosticType.Warning:
                        OnWarning(message.MessageData + $"{message.File}:{message.Line}");
                        continue;
                    case DiagnosticType.Error:
                        OnError(message.MessageData + $"{message.File}:{message.Line}");
                        continue;
                }

            return new CompiledAssemblyFromInMemoryAssembly(result.InMemoryAssembly, assemblyToApplyProcessTo.Name)
            {
                References = References
            };
        }

        try
        {
            assembly = ApplyProcess<NetworkBehaviourILPP>(assembly);
            assembly = ApplyProcess<INetworkMessageILPP>(assembly);
            assembly = ApplyProcess<INetworkSerializableILPP>(assembly);
            assembly = ApplyProcess<ApplyPatchedAttributeILPP>(assembly);

            using var peStream = new MemoryStream(assembly.InMemoryAssembly.PeData);
            using var symbolStream = new MemoryStream(assembly.InMemoryAssembly.PdbData);

            var assemblyDefinition = AssemblyDefinition.ReadAssembly(peStream, new ReaderParameters
            {
                ReadSymbols = true,
                SymbolStream = symbolStream
            });

            assemblyDefinition.Write(OutputPath, new WriterParameters
            {
                SymbolWriterProvider = debugSymbolsAreEmbedded
                    ? new EmbeddedPortablePdbWriterProvider()
                    : new DefaultSymbolWriterProvider(),
                WriteSymbols = true
            });

            Log.Information("Patched successfully : {FileName} -> {OutputPath}", Path.GetFileName(AssemblyPath),
                Path.GetFileName(OutputPath));
        }
        catch (Exception)
        {
            if (AssemblyPath == OutputPath)
            {
                // rename file from _original.dll to .dll
                if (File.Exists(renameAssemblyPath))
                {
                    if (File.Exists(AssemblyPath)) File.Delete(AssemblyPath);
                    File.Move(renameAssemblyPath!, AssemblyPath);
                }

                if (pdbPath is not null && File.Exists(renamePdbPath!))
                {
                    if (File.Exists(pdbPath)) File.Delete(pdbPath);
                    File.Move(renamePdbPath!, pdbPath!);
                }
            }

            throw;
        }
    }
}
