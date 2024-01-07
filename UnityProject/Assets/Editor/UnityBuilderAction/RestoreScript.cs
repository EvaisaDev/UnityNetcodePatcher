using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using UnityEditor;

namespace UnityBuilderAction
{
    public static class RestoreScript
    {
        private static readonly Dictionary<string, string> UnityEditorPath = new()
        {
            ["linux"] = "/opt/unity/Editor",
            ["windows"] = @"C:\UnityEditor\2022.3.9f1\Editor",
            ["macOS"] = "/Applications/Unity/Hub/Editor/2022.3.9f1/Unity.app/Contents/MacOS"
        };
        private static readonly string LocalCopyTargetPath = "UnityEditor";

        private static readonly string[] Secrets = {};
        
        public static void Restore()
        {
            // Gather values from args
            Dictionary<string, string> options = GetValidatedOptions();
            var hostPlatform = options["hostPlatform"];
            
            var assembliesFrom = new DirectoryInfo(Path.Combine(UnityEditorPath[hostPlatform], "Data", "Managed"));
            var assembliesTo = new DirectoryInfo(Path.Combine(Path.GetFullPath(LocalCopyTargetPath), "Data", "Managed"));
            
            RecursiveCopy(assembliesFrom, assembliesTo);
            
            EditorApplication.Exit(0);
        }
        
        private static void RecursiveCopy(DirectoryInfo from, DirectoryInfo to)
        {
            Directory.CreateDirectory(to.FullName);
                
            foreach (string entry in Directory.GetFiles(from.FullName))
            {
                var fileInfo = new FileInfo(entry);
                File.Copy(fileInfo.FullName, Path.Combine(to.FullName, fileInfo.Name));
            }
                
            foreach (string entry in Directory.GetDirectories(from.FullName))
            {
                var fromSubdir = new DirectoryInfo(entry);
                var toSubdir = new DirectoryInfo(Path.Combine(to.FullName, fromSubdir.Name));
                RecursiveCopy(fromSubdir, toSubdir);
            }
        }

        private static Dictionary<string, string> GetValidatedOptions()
        {
            ParseCommandLineArguments(out Dictionary<string, string> validatedOptions);
            
            if (!validatedOptions.TryGetValue("hostPlatform", out string _))
            {
                Console.WriteLine("Missing argument -hostPlatform");
                EditorApplication.Exit(120);
            }

            return validatedOptions;
        }
        
        private static void ParseCommandLineArguments(out Dictionary<string, string> providedArguments)
        {
            providedArguments = new Dictionary<string, string>();
            string[] args = Environment.GetCommandLineArgs();

            // Extract flags with optional values
            for (int current = 0, next = 1; current < args.Length; current++, next++)
            {
                // Parse flag
                bool isFlag = args[current].StartsWith("-");
                if (!isFlag) continue;
                string flag = args[current].TrimStart('-');

                // Parse optional value
                bool flagHasValue = next < args.Length && !args[next].StartsWith("-");
                string value = flagHasValue ? args[next].TrimStart('-') : "";
                bool secret = Secrets.Contains(flag);
                string displayValue = secret ? "*HIDDEN*" : "\"" + value + "\"";

                // Assign
                Console.WriteLine($"Found flag \"{flag}\" with value {displayValue}.");
                providedArguments.Add(flag, value);
            }
        }
    }
}