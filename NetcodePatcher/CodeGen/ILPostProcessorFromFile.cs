using System;
using System.IO;
using System.Linq;
using Mono.Cecil;
using Serilog;
using Unity.CompilationPipeline.Common.Diagnostics;
using Unity.CompilationPipeline.Common.ILPostProcessing;
using Unity.Netcode.Editor.CodeGen;

namespace NetcodePatcher.CodeGen;

public static class ILPostProcessorFromFile
{
    public static bool HasNetcodePatchedAttribute(ICompiledAssembly assembly)
    {
        // read
        AssemblyDefinition? assemblyDefinition = CodeGenHelpers.AssemblyDefinitionFor(assembly, out _);
        if (assemblyDefinition == null) return false;

        return assemblyDefinition.CustomAttributes.Any(
            attribute => attribute.Constructor.DeclaringType.FullName.EndsWith($".{ApplyPatchedAttributeILPP.AttributeNamespaceSuffix}.{ApplyPatchedAttributeILPP.AttributeName}")
        );
    }

    public static void ILPostProcessFile(string assemblyPath, string outputPath, string[] references, Action<string> onWarning, Action<string> onError)
    {
        var assemblyName = Path.GetFileNameWithoutExtension(assemblyPath);
        var assemblyDirectoryName = Path.GetDirectoryName(assemblyPath)!;
        var pdbPath = Path.Combine(assemblyDirectoryName, $"{assemblyName}.pdb");

        Log.Information("Reading : {FileName}", Path.GetFileName(assemblyPath));

        // read the original assembly from file
        ICompiledAssembly assembly = new CompiledAssemblyFromFile(assemblyPath) {
            References = references
        };

        if (HasNetcodePatchedAttribute(assembly))
        { 
            Log.Warning("Skipping {FileName} as it has already been patched.", Path.GetFileName(assemblyPath));
            return;
        }

        Log.Information("Patching : {FileName}", Path.GetFileName(assemblyPath));

        string? renameAssemblyPath = null;
        string? renamePdbPath = null;

        if (assemblyPath == outputPath)
        {
            // remove files with _original.dll and _original.pdb
            
            renameAssemblyPath = Path.Combine(assemblyDirectoryName, $"{assemblyName}_original.dll");
            renamePdbPath = Path.Combine(assemblyDirectoryName, $"{assemblyName}_original.pdb");

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

            File.Move(assemblyPath, renameAssemblyPath);
            File.Move(pdbPath, renamePdbPath);
        }

        ICompiledAssembly ApplyProcess<TProcessor>(ICompiledAssembly assemblyToApplyProcessTo) where TProcessor : ILPostProcessor, new()
        {
            var ilpp = new TProcessor();
            if (!ilpp.WillProcess(assembly)) return assemblyToApplyProcessTo;

            ILPostProcessResult result = ilpp.Process(assembly);

            // handle the error messages like Unity would
            foreach (DiagnosticMessage message in result.Diagnostics)
            {
                switch (message.DiagnosticType)
                {
                    case DiagnosticType.Warning:
                        onWarning(message.MessageData + $"{message.File}:{message.Line}");
                        continue;
                    case DiagnosticType.Error:
                        onError(message.MessageData + $"{message.File}:{message.Line}");
                        continue;
                }
            }

            return new CompiledAssemblyFromInMemoryAssembly(result.InMemoryAssembly, assemblyToApplyProcessTo.Name) {
                References = references
            };
        }

        try
        {
            assembly = ApplyProcess<NetworkBehaviourILPP>(assembly);
            assembly = ApplyProcess<INetworkMessageILPP>(assembly);
            assembly = ApplyProcess<INetworkSerializableILPP>(assembly);
            assembly = ApplyProcess<ApplyPatchedAttributeILPP>(assembly);

            var outputAssemblyName = Path.GetFileNameWithoutExtension(outputPath);
            var outputDirectoryName = Path.GetDirectoryName(outputPath)!;
            var outputPdbPath = Path.Combine(outputDirectoryName, $"{outputAssemblyName}.pdb");

            // save the weaved assembly to file.
            // some tests open it and check for certain IL code.
            File.WriteAllBytes(outputPath, assembly.InMemoryAssembly.PeData);
            File.WriteAllBytes(outputPdbPath, assembly.InMemoryAssembly.PdbData);

            Log.Information("Patched successfully : {FileName} -> {OutputPath}", Path.GetFileName(assemblyPath), Path.GetFileName(outputPath));
        }
        catch (Exception)
        {
            if (assemblyPath == outputPath)
            {
                // rename file from _original.dll to .dll
                if (File.Exists(renameAssemblyPath))
                {
                    File.Move(renameAssemblyPath!, assemblyPath);
                }

                if (File.Exists(renamePdbPath!))
                {
                    File.Move(renamePdbPath!, pdbPath);
                }
            }

            throw;
        }
    }
}