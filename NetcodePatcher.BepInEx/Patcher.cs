using BepInExCore = BepInEx;
using BepInEx.Logging;
using Mono.Cecil;
using System.Collections.Generic;
using System.Reflection;
using Unity.Netcode;
using Serilog.Core;
using Serilog.Events;
using System;
using Serilog;
using System.IO;
using System.Linq;
using BepInEx;

namespace NetcodePatcher.BepInEx
{
    public class BepInSink : ILogEventSink
    {
        private readonly IFormatProvider _formatProvider;

        public BepInSink(IFormatProvider formatProvider)
        {
            _formatProvider = formatProvider;
        }

        public void Emit(LogEvent logEvent)
        {
            var message = logEvent.RenderMessage(_formatProvider);

            // send to bepinex logger
            switch (logEvent.Level)
            {
                case LogEventLevel.Verbose:
                case LogEventLevel.Debug:
                    Patcher.Logger.LogDebug(message);
                    break;
                case LogEventLevel.Information:
                    Patcher.Logger.LogInfo(message);
                    break;
                case LogEventLevel.Warning:
                    Patcher.Logger.LogWarning(message);
                    break;
                case LogEventLevel.Error:
                case LogEventLevel.Fatal:
                    Patcher.Logger.LogError(message);
                    break;
                default:
                    break;
            }
        }
    }

    public static class Patcher
    {
        public static IEnumerable<string> TargetDLLs => CollectTargetDLLs();

        private static IEnumerable<string> CollectTargetDLLs()
        {
            return new List<String>();
        }

        public static void Patch(AssemblyDefinition assembly)
        {
        }


        public static void Initialize()
        {
            Logger.LogInfo("NetcodePatcher.BepInEx initialized");

            var logConfiguration = new LoggerConfiguration()
                .MinimumLevel.Is(LogEventLevel.Verbose)
                .WriteTo.Sink(new BepInSink(null)).CreateLogger();

            Log.Logger = logConfiguration;
            var pluginPath = BepInExCore.Paths.PluginPath;
            var managedPath = BepInExCore.Paths.DllSearchPaths;



            // get AssemblyDefinition and check if valid
            var pluginAssemblies = Directory.GetFiles(pluginPath, "*.dll", System.IO.SearchOption.AllDirectories);

            // patch all assemblies

            foreach (var assemblyPath in pluginAssemblies)
            {
                NetcodePatcher.Patcher.Patch(assemblyPath, assemblyPath, managedPath.ToArray());
            }

        }

        internal static readonly ManualLogSource Logger = BepInExCore.Logging.Logger.CreateLogSource("NetcodePatcher.BepInEx");
    }
}
