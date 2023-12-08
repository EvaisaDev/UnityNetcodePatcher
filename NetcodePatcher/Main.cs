using Mono.Cecil;
using NetcodePatcher.CodeGen;
using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.IO;
using System.Linq;
using System.Reflection;
using Unity.Netcode;
using Unity.Netcode.Editor.CodeGen;
using UnityEngine;

namespace NetcodePatcher
{
    public static class Patcher
    {
        public const string NetcodePatcherVersion = "2.2.0";
        public static void Main(string[] args)
        {
            // check if enough args, otherwise print usage
            if (args.Length < 2)
            {
                Console.WriteLine("Usage: NetcodePatcher.exe <pluginPath> <managedPath>");
                return;
            }

            // get paths from args
            string pluginPath = args[0];
            string managedPath = args[1];

            // patch
            NetcodePatcher.Patcher.Patch(pluginPath, managedPath);
        }
        public class Logging
        {
            private readonly object lockObject = new object();
            public string filePath;

            public Logging(string fileName)
            {
                // set filepath to assembly location + filename
                filePath = Path.Combine(Path.GetDirectoryName(Assembly.GetExecutingAssembly().Location), fileName);

                // Use lock to ensure only one instance is modifying the file at a time
                lock (lockObject)
                {
                    // if file exists, empty file, else create file
                    if (File.Exists(filePath))
                    {
                        File.WriteAllText(filePath, "");
                    }
                    else
                    {
                        File.Create(filePath).Close(); // Close the FileStream to release the file lock
                    }
                }
            }

            public void LogMessage(string message)
            {
                // Use lock to ensure only one instance is modifying the file at a time
                lock (lockObject)
                {
                    // write message to file
                    File.AppendAllText(filePath, $"{message}\r\n");
                }

                // print message to console
                Console.WriteLine(message);
            }

            public void LogWarning(string message)
            {
                // Use lock to ensure only one instance is modifying the file at a time
                lock (lockObject)
                {
                    // write message to file
                    File.AppendAllText(filePath, $"[Warning]: {message}\r\n");
                }

                // print message to console
                Console.WriteLine($"[Warning]: {message}");
            }

            public void LogError(string message)
            {
                // Use lock to ensure only one instance is modifying the file at a time
                lock (lockObject)
                {
                    // write message to file
                    File.AppendAllText(filePath, $"[Error]: {message}\r\n");
                }

                // print message to console
                Console.WriteLine($"[Error]: {message}");
            }

            public void LogInfo(string message)
            {
                // Use lock to ensure only one instance is modifying the file at a time
                lock (lockObject)
                {
                    // write message to file
                    File.AppendAllText(filePath, $"[Info]: {message}\r\n");
                }

                // print message to console
                Console.WriteLine($"[Info]: {message}");
            }
        }

        public static void Patch(string pluginPath, string managedPath)
        {
            Patcher.Logger.LogMessage($"Initializing NetcodePatcher {NetcodePatcherVersion}");
            HashSet<string> hashSet = new HashSet<string>();
            List<string> references = new List<string>()
            {
                managedPath + "\\Unity.Netcode.Runtime.dll",
                managedPath + "\\UnityEngine.CoreModule.dll",
                managedPath + "\\Unity.Netcode.Components.dll",
                managedPath + "\\Unity.Networking.Transport.dll",
            };

            List<string> blackList = new List<string>()
            {
                "Unity.Netcode.Runtime",
                "UnityEngine.CoreModule",
                "Unity.Netcode.Components",
                "Unity.Networking.Transport",
                "Assembly-CSharp",
                "ClientNetworkTransform"
            };

            // remove files with _original.dll and _original.pdb in pluginPath
            foreach (string text in Directory.GetFiles(pluginPath, "*.*", SearchOption.AllDirectories))
            {
                string fileName = Path.GetFileName(text);
                if (fileName.ToLower().Contains("_original"))
                {
                    Patcher.Logger.LogMessage("Deleting : " + fileName);
                    File.Delete(text);
                }
            }

            foreach (string text3 in Directory.GetFiles(pluginPath, "*.dll", SearchOption.AllDirectories))
            {
                string fileName = Path.GetFileName(text3);
                if (!fileName.ToLower().Contains("mmhook"))
                {
                    //Patcher.Logger.LogMessage("Checking : " + fileName);
                    var found = false;

                    foreach (TypeDefinition typeDefinition in AssemblyDefinition.ReadAssembly(text3).MainModule.Types)
                    {


                        if (typeDefinition.BaseType != null)
                        {
                            // check if subclass of NetworkBehaviour
                            if (typeDefinition.IsSubclassOf(typeof(NetworkBehaviour).FullName))
                            {
                                var skip = false;
                                // check if contains blacklisted phrases
                                foreach (string blacklisted in blackList)
                                {
                                    if (fileName.ToLowerInvariant().Contains(blacklisted.ToLowerInvariant()))
                                    {
                                        skip = true;
                                    }
                                }

                                if (skip || hashSet.Contains(text3))
                                {
                                    break;
                                }

                                found = true;
                                hashSet.Add(text3);
                                Patcher.Logger.LogMessage($"Found NetworkBehaviour({typeof(NetworkBehaviour).FullName}) in : " + fileName);
                                break;
                            }
                        }
                    }

                    /*if (!found)
                    {
                        Patcher.Logger.LogWarning("Did not find any NetworkBehaviours in : " + fileName);
                    }
                    */
                }
            }
            foreach (string text4 in hashSet)
            {
                var success = true;
                try
                {
                    Patcher.Logger.LogMessage("Patching : " + Path.GetFileName(text4));

                    ILPostProcessorFromFile.ILPostProcessFile(text4, references.ToArray(), (warning) =>
                    {
                        // replace || with new line
                        warning = warning.Replace("||  ", "\r\n").Replace("||", " ");
                        Patcher.Logger.LogWarning($"Warning when patching ({Path.GetFileName(text4)}): {warning}");
                        success = false;
                    },
                    (error) =>
                    {
                        error = error.Replace("||  ", "\r\n").Replace("||", " ");
                        Patcher.Logger.LogError($"Error when patching ({Path.GetFileName(text4)}): {error}");
                        success = false;
                    });

                }
                catch (Exception exception)
                {
                    // error
                    Patcher.Logger.LogWarning($"Failed to patch ({Path.GetFileName(text4)}): {exception}");
                    success = false;
                }

                if (success)
                {
                    Patcher.Logger.LogMessage($"Patched ({Path.GetFileName(text4)}) successfully");
                }
            }

        }

        public static Logging Logger = new Logging("NetcodePatcher.log");
    }
}