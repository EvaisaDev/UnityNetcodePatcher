using BepInEx;
using BepInEx.Logging;
using Mono.Cecil;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace BepInNetcodePatcher
{
    public static class Patcher
    {
        public static IEnumerable<string> TargetDLLs
        {
            get
            {
                return Patcher.CollectTargetDLLs();
            }
        }
        private static IEnumerable<string> CollectTargetDLLs()
        {
            return new List<string>();
        }

        public static void Patch(AssemblyDefinition _)
        {
        }

        public static void Initialize()
        {
            Logger.LogInfo("BepInNetcodePatcher initialized");

            var managedPath = Paths.ManagedPath;
            var pluginPath = Paths.PluginPath;

            NetcodePatcher.Patcher.Patch(pluginPath, managedPath);
        }

        private static readonly ManualLogSource Logger = BepInEx.Logging.Logger.CreateLogSource("BepInNetcodePatcher");
    }
}
