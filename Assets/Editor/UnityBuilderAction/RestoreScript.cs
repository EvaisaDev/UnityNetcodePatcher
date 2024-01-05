using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using UnityEditor;
using UnityEditor.Build.Reporting;

namespace UnityBuilderAction
{
    public static class RestoreScript
    {
        private static readonly string UnityEditorPath = "/opt/unity/Editor";
        private static readonly string LocalCopyTargetPath = "UnityEditor";
        
        public static void Restore()
        {
            void RecursiveCopy(DirectoryInfo from, DirectoryInfo to)
            {
                foreach (string entry in Directory.GetFiles(from.FullName))
                {
                    var fileInfo = new FileInfo(entry);
                    File.Copy(fileInfo.FullName, Path.Combine(to.FullName, fileInfo.Name));
                }
                
                foreach (string entry in Directory.GetDirectories(from.FullName))
                {
                    var dirInfo = new DirectoryInfo(entry);
                    RecursiveCopy(dirInfo, new DirectoryInfo(Path.Combine(to.FullName, dirInfo.Name)));
                }
            }
            
            var assembliesFrom = new DirectoryInfo(Path.Combine(UnityEditorPath, "Data", "Managed"));
            var assembliesTo = new DirectoryInfo(Path.Combine(Path.GetFullPath(LocalCopyTargetPath), "Data", "Managed"));

            Directory.CreateDirectory(assembliesTo.FullName);
            RecursiveCopy(assembliesFrom, assembliesTo);
            
            EditorApplication.Exit(0);
        }
    }
}