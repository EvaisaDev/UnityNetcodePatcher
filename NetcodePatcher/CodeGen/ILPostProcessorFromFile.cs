using System;
using System.Collections.Generic;
using System.IO;
using System.Text;
using Unity.CompilationPipeline.Common.Diagnostics;
using Unity.CompilationPipeline.Common.ILPostProcessing;
using Unity.Netcode.Editor.CodeGen;
using System.Security;
using System.Security.Permissions;

[assembly: SecurityPermission(SecurityAction.RequestMinimum, SkipVerification = true)]
namespace NetcodePatcher.CodeGen
{
    public static class ILPostProcessorFromFile
    {
        // read, weave, write file via ILPostProcessor

        public static ILPostProcessResult NetworkBehaviourProcess(ICompiledAssembly assembly, Action<string> OnWarning, Action<string> OnError)
        {
            NetworkBehaviourILPP ilpp = new NetworkBehaviourILPP();
            if (ilpp.WillProcess(assembly))
            {

                // process it like Unity would
                ILPostProcessResult result = ilpp.Process(assembly);


                // handle the error messages like Unity would
                
                foreach (DiagnosticMessage message in result.Diagnostics)
                {
                    if (message.DiagnosticType == DiagnosticType.Warning)
                    {
                        // console output
                        OnWarning(message.MessageData + $"{message.File}:{message.Line}");
                    }
                    else if (message.DiagnosticType == DiagnosticType.Error)
                    {
                        OnError(message.MessageData + $"{message.File}:{message.Line}");
                    }
                }

                return result;
            }
            return null;
        }

        public static ILPostProcessResult INetworkMessageProcess(ICompiledAssembly assembly, Action<string> OnWarning, Action<string> OnError)
        {
            INetworkMessageILPP ilpp = new INetworkMessageILPP();
            if (ilpp.WillProcess(assembly))
            {
                //Debug.Log("Will Process: " + assembly.Name);

                // process it like Unity would
                ILPostProcessResult result = ilpp.Process(assembly);

                // handle the error messages like Unity would
                foreach (DiagnosticMessage message in result.Diagnostics)
                {
                    if (message.DiagnosticType == DiagnosticType.Warning)
                    {
                        OnWarning(message.MessageData + $"{message.File}:{message.Line}");
                    }
                    else if (message.DiagnosticType == DiagnosticType.Error)
                    {
                        OnError(message.MessageData + $"{message.File}:{message.Line}");
                    }
                }

                return result;
            }
            return null;
        }

        public static ILPostProcessResult INetworkSerializableProcess(ICompiledAssembly assembly, Action<string> OnWarning, Action<string> OnError)
        {
            INetworkSerializableILPP ilpp = new INetworkSerializableILPP();
            if (ilpp.WillProcess(assembly))
            {
                //Debug.Log("Will Process: " + assembly.Name);

                // process it like Unity would
                ILPostProcessResult result = ilpp.Process(assembly);

                // handle the error messages like Unity would
                foreach (DiagnosticMessage message in result.Diagnostics)
                {
                    if (message.DiagnosticType == DiagnosticType.Warning)
                    {
                        OnWarning(message.MessageData + $"{message.File}:{message.Line}");
                    }
                    else if (message.DiagnosticType == DiagnosticType.Error)
                    {
                        OnError(message.MessageData + $"{message.File}:{message.Line}");
                    }
                }

                return result;
            }
            return null;
        }

        public static void ILPostProcessFile(string assemblyPath, string[] references, Action<string> OnWarning, Action<string> OnError)
        {
            // remove files with _original.dll and _original.pdb
            

            var newPath = assemblyPath.Replace(".dll", "_original.dll");
            string pdbFileName = Path.GetFileNameWithoutExtension(assemblyPath) + ".pdb";
            string pdbPath = Path.Combine(Path.GetDirectoryName(assemblyPath), pdbFileName);
            string newPdbPath = pdbPath.Replace(".pdb", "_original.pdb");

            File.Move(assemblyPath, newPath);
            File.Move(pdbPath, newPdbPath);

            // read the original assembly from file



            var initialAssembly = new CompiledAssemblyFromFile(newPath);
            initialAssembly.References = references;

            ICompiledAssembly assembly = initialAssembly;

            var result = NetworkBehaviourProcess(assembly, OnWarning, OnError);

            if (result != null)
            {
                var newAssembly = new CompiledAssemblyFromInMemoryAssembly(result.InMemoryAssembly, assembly.Name); 
                newAssembly.References = references;

                assembly = newAssembly;
            }
            
            result = INetworkMessageProcess(assembly, OnWarning, OnError);
          

            if (result != null)
            {
                var newAssembly = new CompiledAssemblyFromInMemoryAssembly(result.InMemoryAssembly, assembly.Name);
                newAssembly.References = references;

                assembly = newAssembly;
            }

            result = INetworkSerializableProcess(assembly, OnWarning, OnError);

            // save the weaved assembly to file.
            // some tests open it and check for certain IL code.
            File.WriteAllBytes(assemblyPath, result.InMemoryAssembly.PeData);
            File.WriteAllBytes(pdbPath, result.InMemoryAssembly.PdbData);
        
        }
    }
}
