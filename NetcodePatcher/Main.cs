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
            Patcher.Logger.LogMessage("Initializing NetcodePatcher");
            HashSet<string> hashSet = new HashSet<string>();
            List<string> references = new List<string>()
            {
                managedPath + "\\Unity.Netcode.Runtime.dll",
                managedPath + "\\UnityEngine.CoreModule.dll",
                managedPath + "\\Unity.Netcode.Components.dll",
                managedPath + "\\Unity.Networking.Transport.dll",
            };

            foreach (string text3 in Directory.GetFiles(pluginPath, "*.dll", SearchOption.AllDirectories))
            {
                string fileName = Path.GetFileName(text3);
                if (!fileName.ToLower().Contains("mmhook"))
                {
                    foreach (TypeDefinition typeDefinition in AssemblyDefinition.ReadAssembly(text3).MainModule.Types)
                    {


                        if (typeDefinition.BaseType != null)
                        {
                            ;                           // check if subclass of NetworkBehaviour
                            if (typeDefinition.IsSubclassOf(typeof(NetworkBehaviour).FullName))
                            {

                                hashSet.Add(text3);
                                break;
                            }
                        }
                    }
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
                    Patcher.Logger.LogWarning($"Did not patch ({Path.GetFileName(text4)}): {exception.Message} (Already patched?)");
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