using BepInEx;
using BepInEx.Logging;
using Mono.Cecil;
using System.Collections.Generic;

namespace BepInNetcodePatcher
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

            var managedPath = Paths.ManagedPath;
            var pluginPath = Paths.PluginPath;

            //Patcher.Patch(pluginPath, managedPath);
        }

        private static readonly ManualLogSource Logger = BepInEx.Logging.Logger.CreateLogSource("NetcodePatcher.BepInEx");
    }
}
