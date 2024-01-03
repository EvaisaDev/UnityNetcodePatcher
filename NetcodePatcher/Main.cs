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
using System.CommandLine;
using System.Threading.Tasks;

namespace NetcodePatcher
{
    public static class Patcher
    {
        public static bool noLogging = false;
        static async Task<int> Main(string[] args)
        {
            var fileOption = new Option<string>(
                name: "--plugins",
                description: "Path to patch folder, containing plugins to patch.");

            fileOption.AddAlias("-p");

            fileOption.IsRequired = false;

            fileOption.AddValidator(result =>
            {
                var path = result.Tokens.FirstOrDefault()?.Value;

                if (path == null)
                {
                    result.ErrorMessage = "Path to patch folder is required.";
                }

                if (!Directory.Exists(path))
                {
                    result.ErrorMessage = "Path to patch folder does not exist.";
                }
            });

            fileOption.ArgumentHelpName = "Path to patch folder";

            
            var depsOption = new Option<string>(
                name: "--deps",
                description: "Path to dependencies folder, containing any dependencies for patched plugins.");

            depsOption.AddAlias("-d");

            depsOption.IsRequired = false;

            depsOption.AddValidator(result =>
            {
                var path = result.Tokens.FirstOrDefault()?.Value;

                if (path == null)
                {
                    result.ErrorMessage = "Path to dependencies folder is required.";
                }

                if (!Directory.Exists(path))
                {
                    result.ErrorMessage = "Path to dependencies folder does not exist.";
                }
            });

            depsOption.ArgumentHelpName = "Path to dependencies folder";

            var noLoggingOption = new Option<bool>(
                name: "--no-logging",
                description: "Disable logging to file.");

            noLoggingOption.AddAlias("-n");

            noLoggingOption.IsRequired = false;

            // optionally support command separated file list
            var filesOption = new Option<string>(
                name: "--plugin-assemblies",
                description: "List of files to patch, separated by comma.");

            filesOption.AddAlias("-pa");

            filesOption.IsRequired = false;

            filesOption.AddValidator(result =>
            {
                var files = result.Tokens.FirstOrDefault()?.Value;

                if (files == null)
                {
                    result.ErrorMessage = "List of files to patch is required.";
                }
            });

            var depsAssembliesOption = new Option<string>(
                name: "--deps-assemblies",
                description: "List of files to patch, separated by comma.");

            depsAssembliesOption.AddAlias("-da");

            depsAssembliesOption.IsRequired = false;

            depsAssembliesOption.AddValidator(result =>
            {
                var files = result.Tokens.FirstOrDefault()?.Value;

                if (files == null)
                {
                    result.ErrorMessage = "List of files to patch is required.";
                }
            });

            // make sure arguments are given, either --plugins and --deps or --plugin-assemblies and --deps-assemblies

            var rootCommand = new RootCommand
            {
                fileOption,
                depsOption,
                filesOption,
                depsAssembliesOption,
                noLoggingOption,
            };

            rootCommand.Description = "NetcodePatcher";


            rootCommand.SetHandler((fileOptionArg, depsOptionArg, filesOptionArg, depsAssembliesOptionArg, noLoggingOptionArg) => { 

                var plugins = new List<string>();
                var deps = new List<string>();

                if (fileOptionArg != null && depsOptionArg != null)
                {
                    plugins = Directory.GetFiles(fileOptionArg, "*.dll", SearchOption.AllDirectories).ToList();
                    deps = Directory.GetFiles(depsOptionArg, "*.dll", SearchOption.AllDirectories).ToList();
                }
                else if (filesOptionArg != null && depsAssembliesOptionArg != null)
                {
                    plugins = filesOptionArg.Split(',').ToList();
                    deps = depsAssembliesOptionArg.Split(',').ToList();
                }
                else
                {
                    Console.WriteLine("Invalid arguments, either --plugins and --deps or --plugin-assemblies and --deps-assemblies must be given.");
                    return Task.FromResult(1);
                }

                Patch(plugins.ToArray(), deps.ToArray(), noLoggingOptionArg);

                return Task.FromResult(0);
            
            }, fileOption, depsOption, filesOption, depsAssembliesOption, noLoggingOption);


            return await rootCommand.InvokeAsync(args);
        }

        public class Logging
        {
            private readonly object lockObject = new object();
            public string filePath;

            public Logging(string fileName)
            {
                // set filepath to assembly location + filename
                filePath = Path.Combine(Path.GetDirectoryName(Assembly.GetExecutingAssembly().Location), fileName);

                // check if file is locked, if it is, create a new file with a index, if that is locked, increment etc.
                if (File.Exists(filePath))
                {
                    var index = 1;
                    while (true)
                    {
                        try
                        {
                            File.WriteAllText(filePath, "");
                            break;
                        }
                        catch (IOException)
                        {
                            filePath = Path.Combine(Path.GetDirectoryName(Assembly.GetExecutingAssembly().Location), $"{Path.GetFileNameWithoutExtension(fileName)}_{index}{Path.GetExtension(fileName)}");
                            index++;
                        }
                    }
                }
                else
                {
                    File.Create(filePath).Close(); // Close the FileStream to release the file lock
                }

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

        public static void Patch(string pluginPath, string managedPath, bool noLogging = false)
        {
            var plugins = Directory.GetFiles(managedPath, "*.dll", SearchOption.AllDirectories);
            var deps = Directory.GetFiles(managedPath, "*.dll", SearchOption.AllDirectories);

            Patch(plugins, deps, noLogging);
        }

        public static void Patch(string[] plugins, string[] deps, bool noLogging = false)
        {
            Patcher.noLogging = noLogging;

            if (!noLogging)
            {
                Patcher.Logger = new Logging("NetcodePatcher.log");
                // get version from assembly
                Patcher.Logger.LogMessage($"Initializing NetcodePatcher v{Assembly.GetExecutingAssembly().GetName().Version}");
            }

            HashSet<string> hashSet = new HashSet<string>();
            List<string> references = new List<string>();

            // include everything from managedPath
            foreach (string text2 in deps)
            {
                references.Add(text2);
            }

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
            foreach (string text in plugins)
            {
                string fileName = Path.GetFileName(text);
                if (fileName.ToLower().Contains("_original"))
                {
                    if (!noLogging)
                    {
                        Patcher.Logger.LogMessage("Deleting : " + fileName);
                    }
                    File.Delete(text);
                }
            }

            foreach (string text3 in plugins)
            {
                string fileName = Path.GetFileName(text3);
                if (!fileName.ToLower().Contains("mmhook"))
                {
                    //Patcher.Logger.LogMessage("Checking : " + fileName);

                    var found = false;

                    // create assembly resolver with references
                    /*
                    var assemblyResolver = new DefaultAssemblyResolver();
                    assemblyResolver.AddSearchDirectory(managedPath);
                    assemblyResolver.AddSearchDirectory(pluginPath);
                    */

                    var handle = AssemblyDefinition.ReadAssembly(text3);

                    foreach (TypeDefinition typeDefinition in handle.MainModule.Types)
                    {


                        if (typeDefinition.BaseType != null)
                        {
                            /*
                            if (!(typeDefinition == null || !typeDefinition.IsClass))
                            {
                                var baseTypeRef = typeDefinition.BaseType;
                                while (baseTypeRef != null)
                                {
                                    try
                                    {
                                        baseTypeRef = baseTypeRef.Resolve().BaseType;
                                    }
                                    catch (Exception e)
                                    {
                                        Patcher.Logger.LogWarning($"Failed to resolve base type: {e}");
                                        break;
                                    }
                    
                                }
                            }*/



                            // check if subclass of NetworkBehaviour
                            if (File.Exists(text3.Replace(".dll", ".pdb")))
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
                                if (!noLogging)
                                    Patcher.Logger.LogMessage($"Added ({fileName}) to patch list.");
                                break;
                            }
                        }
                    }

                    /*if (!found)
                    {
                        Patcher.Logger.LogMessage($"No NetworkBehaviour({typeof(NetworkBehaviour).FullName}) found in : " + fileName);
                    }*/

                    // dispose handle
                    handle.Dispose();
                }
            }
            foreach (string text4 in hashSet)
            {
                var success = true;
                try
                {
                    if (!noLogging)
                        Patcher.Logger.LogMessage("Patching : " + Path.GetFileName(text4));

                    ILPostProcessorFromFile.ILPostProcessFile(text4, references.ToArray(), (warning) =>
                    {
                        // replace || with new line
                        warning = warning.Replace("||  ", "\r\n").Replace("||", " ");
                        if (!noLogging)
                            Patcher.Logger.LogWarning($"Warning when patching ({Path.GetFileName(text4)}): {warning}");
                        success = false;
                    },
                    (error) =>
                    {
                        error = error.Replace("||  ", "\r\n").Replace("||", " ");
                        if (!noLogging)
                            Patcher.Logger.LogError($"Error when patching ({Path.GetFileName(text4)}): {error}");
                        success = false;
                    });

                }
                catch (Exception exception)
                {
                    // error
                    if (!noLogging)
                        Patcher.Logger.LogWarning($"Failed to patch ({Path.GetFileName(text4)}): {exception}");

                    // rename file from _original.dll to .dll
                    File.Move(text4.Replace(".dll", "_original.dll"), text4);
                    File.Move(text4.Replace(".dll", "_original.pdb"), text4.Replace(".dll", ".pdb"));

                    success = false;
                }

                if (success)
                {
                    if (!noLogging)
                        Patcher.Logger.LogMessage($"Patched ({Path.GetFileName(text4)}) successfully");
                }
            }

        }

        public static Logging Logger;
    }
}