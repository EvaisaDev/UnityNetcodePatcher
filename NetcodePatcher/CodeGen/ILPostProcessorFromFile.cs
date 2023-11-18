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
                //Debug.Log("Will Process: " + assembly.Name);

                // process it like Unity would
                ILPostProcessResult result = ilpp.Process(assembly);

                // handle the error messages like Unity would
                foreach (DiagnosticMessage message in result.Diagnostics)
                {
                    if (message.DiagnosticType == DiagnosticType.Warning)
                    {
                        OnWarning(message.MessageData);
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
                        OnWarning(message.MessageData);
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
                        OnWarning(message.MessageData);
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
            var initialAssembly = new CompiledAssemblyFromFile(assemblyPath);
            initialAssembly.References = references;

            ICompiledAssembly assembly = initialAssembly;

            var result = NetworkBehaviourProcess(assembly, OnWarning, OnError);
            
            if (result != null)
            {
                assembly = new CompiledAssemblyFromInMemoryAssembly(result.InMemoryAssembly, assembly.Name);
            }
            /*
            result = INetworkMessageProcess(assembly, OnWarning, OnError);
            
            
            if (result != null)
            {
                assembly = new CompiledAssemblyFromInMemoryAssembly(result.InMemoryAssembly, assembly.Name);
            }

            result = INetworkSerializableProcess(assembly, OnWarning, OnError);
            */

            // save the weaved assembly to file.
            // some tests open it and check for certain IL code.
            File.WriteAllBytes(assemblyPath, result.InMemoryAssembly.PeData);
        
        }
    }
}
