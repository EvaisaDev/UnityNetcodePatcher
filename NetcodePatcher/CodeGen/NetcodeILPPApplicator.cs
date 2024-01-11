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
    public Action<string> OnWarning { get; set; } = _ => { };
    public Action<string> OnError { get; set; } = _ => { };

    private string AssemblyPath { get; }
    private string OutputPath { get; }
    private string[] References { get; }

    private string AssemblyName => Path.GetFileNameWithoutExtension(AssemblyPath);
    private string AssemblyFileName => Path.GetFileName(AssemblyPath);
    private string AssemblyDirName => Path.GetDirectoryName(AssemblyPath)!;
    private string PdbPath => Path.Combine(AssemblyDirName, $"{AssemblyName}.pdb");
    
    public NetcodeILPPApplicator(string assemblyPath, string outputPath, string[] references)
    {
        AssemblyPath = assemblyPath;
        OutputPath = outputPath;
        References = references;
    }
    
    public static bool HasNetcodePatchedAttribute(ICompiledAssembly assembly)
    {
        // read
        AssemblyDefinition? assemblyDefinition = CodeGenHelpers.AssemblyDefinitionFor(assembly, out _);
        if (assemblyDefinition is null) return false;

        return assemblyDefinition.CustomAttributes.Any(
            attribute => attribute.Constructor.DeclaringType.FullName.EndsWith($".{ApplyPatchedAttributeILPP.AttributeNamespaceSuffix}.{ApplyPatchedAttributeILPP.AttributeName}")
        );
    }

    public void ApplyProcesses()
    {
        Log.Information("Reading : {FileName}", Path.GetFileName(AssemblyPath));

        CompiledAssemblyFromFile assemblyFromFile;
        try
        {
            // read the original assembly from file
            assemblyFromFile = new CompiledAssemblyFromFile(AssemblyPath) {
                References = References
            };
        }
        catch (InvalidDataException)
        {
            Log.Error("Couldn't find debug information for ({AssemblyFileName}), forced to skip", AssemblyFileName);
            return;
        }

        var debugSymbolsAreEmbedded = assemblyFromFile.DebugSymbolsAreEmbedded;
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
            renamePdbPath = Path.Combine(AssemblyDirName, $"{AssemblyName}_original.pdb");

            if (File.Exists(renameAssemblyPath))
            {
                Log.Information("Deleting : {FileName}", Path.GetFileName(renameAssemblyPath));
                File.Delete(renameAssemblyPath);
            }

            if (File.Exists(renamePdbPath))
            {
                Log.Information("Deleting : {FileName}", Path.GetFileName(renamePdbPath));
                File.Delete(renamePdbPath);
            }

            File.Move(AssemblyPath, renameAssemblyPath);
            if (!debugSymbolsAreEmbedded)
                File.Move(PdbPath, renamePdbPath);
        }

        ICompiledAssembly ApplyProcess<TProcessor>(ICompiledAssembly assemblyToApplyProcessTo) where TProcessor : ILPostProcessor, new()
        {
            var ilpp = new TProcessor();
            if (!ilpp.WillProcess(assembly)) return assemblyToApplyProcessTo;

            ILPostProcessResult result = ilpp.Process(assembly);

            if (result is null)
                return assemblyToApplyProcessTo;

            // handle the error messages like Unity would
            foreach (DiagnosticMessage message in result.Diagnostics)
            {
                switch (message.DiagnosticType)
                {
                    case DiagnosticType.Warning:
                        OnWarning(message.MessageData + $"{message.File}:{message.Line}");
                        continue;
                    case DiagnosticType.Error:
                        OnError(message.MessageData + $"{message.File}:{message.Line}");
                        continue;
                }
            }

            return new CompiledAssemblyFromInMemoryAssembly(result.InMemoryAssembly, assemblyToApplyProcessTo.Name) {
                References = References
            };
        }

        try
        {
            assembly = ApplyProcess<NetworkBehaviourILPP>(assembly);
            assembly = ApplyProcess<INetworkMessageILPP>(assembly);
            assembly = ApplyProcess<INetworkSerializableILPP>(assembly);
            assembly = ApplyProcess<ApplyPatchedAttributeILPP>(assembly);

            var outputAssemblyName = Path.GetFileNameWithoutExtension(OutputPath);
            var outputDirectoryName = Path.GetDirectoryName(OutputPath)!;
            var outputPdbPath = Path.Combine(outputDirectoryName, $"{outputAssemblyName}.pdb");
            
            if (!debugSymbolsAreEmbedded)
            {
                // save the weaved assembly to file.
                // some tests open it and check for certain IL code.
                File.WriteAllBytes(OutputPath, assembly.InMemoryAssembly.PeData);
                File.WriteAllBytes(outputPdbPath, assembly.InMemoryAssembly.PdbData);
                return;
            }

            using var peStream = new MemoryStream(assembly.InMemoryAssembly.PeData);
            using var symbolStream = new MemoryStream(assembly.InMemoryAssembly.PdbData);

            var assemblyDefinition = AssemblyDefinition.ReadAssembly(peStream);

            assemblyDefinition.Write(new WriterParameters
            {
                SymbolStream = symbolStream,
                WriteSymbols = true
            });

            Log.Information("Patched successfully : {FileName} -> {OutputPath}", Path.GetFileName(AssemblyPath), Path.GetFileName(OutputPath));
        }
        catch (Exception)
        {
            if (AssemblyPath == OutputPath)
            {
                // rename file from _original.dll to .dll
                if (File.Exists(renameAssemblyPath))
                {
                    File.Move(renameAssemblyPath!, AssemblyPath);
                }

                if (File.Exists(renamePdbPath!))
                {
                    File.Move(renamePdbPath!, PdbPath);
                }
            }

            throw;
        }
    }
}