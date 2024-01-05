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
        public static void Restore()
        {
            EditorApplication.Exit(0);
        }
    }
}