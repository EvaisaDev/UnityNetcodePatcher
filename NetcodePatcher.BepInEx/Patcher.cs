using BepInExCore = BepInEx;
using BepInEx.Logging;
using Mono.Cecil;
using System.Collections.Generic;

namespace NetcodePatcher.BepInEx
{
    public static class Patcher
    {
        public static IEnumerable<string> TargetDLLs => CollectTargetDLLs();

        private static IEnumerable<string> CollectTargetDLLs()
        {
            return new List<string>();
        }

        public static void Patch(AssemblyDefinition _)
        {
        }

        public static void Initialize()
        {
            Logger.LogInfo("NetcodePatcher.BepInEx initialized");

            var managedPath = BepInExCore.Paths.ManagedPath;
            var pluginPath = BepInExCore.Paths.PluginPath;

            //Patcher.Patch(pluginPath, managedPath);
        }

        private static readonly ManualLogSource Logger = BepInExCore.Logging.Logger.CreateLogSource("NetcodePatcher.BepInEx");
    }
}
