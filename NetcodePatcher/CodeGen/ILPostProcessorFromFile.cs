using System;
using System.IO;
using Serilog;
using Unity.CompilationPipeline.Common.Diagnostics;
using Unity.CompilationPipeline.Common.ILPostProcessing;
using Unity.Netcode.Editor.CodeGen;

namespace NetcodePatcher.CodeGen;

public static class ILPostProcessorFromFile
{
    public static void ILPostProcessFile(string assemblyPath, string outputPath, string[] references, Action<string> onWarning, Action<string> onError)
    {
        var assemblyName = Path.GetFileNameWithoutExtension(assemblyPath);
        var assemblyDirectoryName = Path.GetDirectoryName(assemblyPath)!;
        var pdbPath = Path.Combine(assemblyDirectoryName, $"{assemblyName}.pdb");
            
        if (assemblyPath == outputPath)
        {
            // remove files with _original.dll and _original.pdb

            var newAssemblyPath = Path.Combine(assemblyDirectoryName, $"{assemblyName}_original.dll");
            var newPdbPath = Path.Combine(assemblyDirectoryName, $"{assemblyName}_original.pdb");

            if (File.Exists(newAssemblyPath))
            {
                Log.Information("Deleting : {FileName}", Path.GetFileName(newAssemblyPath));
                File.Delete(newAssemblyPath);
            }
            
            if (File.Exists(newPdbPath))
            {
                Log.Information("Deleting : {FileName}", Path.GetFileName(newPdbPath));
                File.Delete(newPdbPath);
            }

            File.Move(assemblyPath, newAssemblyPath);
            File.Move(pdbPath, newPdbPath);

            assemblyPath = newAssemblyPath;
            pdbPath = newPdbPath;
        }
            
        // read the original assembly from file

        ICompiledAssembly assembly = new CompiledAssemblyFromFile(assemblyPath) {
            References = references
        };

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

        assembly = ApplyProcess<NetworkBehaviourILPP>(assembly);
        assembly = ApplyProcess<INetworkMessageILPP>(assembly);
        assembly = ApplyProcess<INetworkSerializableILPP>(assembly);
        
        var outputAssemblyName = Path.GetFileNameWithoutExtension(outputPath);
        var outputDirectoryName = Path.GetDirectoryName(outputPath)!;
        var outputPdbPath = Path.Combine(outputDirectoryName, $"{outputAssemblyName}.pdb");
        
        // save the weaved assembly to file.
        // some tests open it and check for certain IL code.
        File.WriteAllBytes(outputPath, assembly.InMemoryAssembly.PeData);
        File.WriteAllBytes(outputPdbPath, assembly.InMemoryAssembly.PdbData);
    }
}