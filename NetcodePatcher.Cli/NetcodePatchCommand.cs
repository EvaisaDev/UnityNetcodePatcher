using System;
using System.Collections.Generic;
using System.CommandLine;
using System.IO;
using System.Linq;
using System.CommandLine.NamingConventionBinder;
using System.Diagnostics;
using System.Reflection;
using System.Threading.Tasks;
using NetcodePatcher.Cli.Extensions;
using NetcodePatcher.Tools.Common;
using Serilog;
using Serilog.Events;

namespace NetcodePatcher.Cli;

public sealed class NetcodePatchCommand : RootCommand
{
    public NetcodePatchCommand()
    {
        Name = "netcode-patch";
        Description = "Netcode patch given assemblies";

        Add(new Argument<FileSystemInfo>("plugin","Paths to patch folder/file") { Arity = ArgumentArity.ExactlyOne }.ExistingOnly().NoUnc());
        Add(new Argument<FileSystemInfo[]>("dependencies", "Paths to dependency folders/files") { Arity = ArgumentArity.ZeroOrMore }.ExistingOnly().NoUnc());
        Add(new Option<string>(["--netcode-version", "-nv"], () => "1.5.2", "Netcode for GameObjects version"));
        Add(new Option<string>(["--transport-version", "-tv"], () => "1.0.0", "Unity Transport version"));
        Add(new Option<string>(["--unity-version", "-uv"], () => "2022.3.9", "Unity editor version (major.minor)"));
        Add(new Option<bool>(["--unity-netcode-native-collection-support", "-unncs"], () => false, "Whether Netcode's native collection support is enabled"));
        Add(new Option<string?>(["--output", "-o"], "Output folder/file path").LegalFilePathsOnly());
        Add(new Option<bool>("--no-overwrite", "Sets output path to [assembly]_patched.dll, as opposed to renaming the original assembly"));
        Add(new Option<bool>("--disable-parallel", "Don't patch in parallel"));
        Add(new Option<LogEventLevel>("--log-level", () => LogEventLevel.Information, "Sets the minimum log-level. Messages below this are ignored."));
        Add(new Option<string?>("--log-file", "Set a filepath to log to.") { Arity = ArgumentArity.ExactlyOne }.LegalFilePathsOnly());

        Handler = HandlerDescriptor.FromDelegate(Handle).GetCommandHandler();
    }

    private static void Handle(
        FileSystemInfo plugin,
        FileSystemInfo[] dependencies,
        string netcodeVersion,
        string transportVersion,
        string unityVersion,
        bool nativeCollectionSupport,
        string? output,
        bool noOverwrite,
        bool disableParallel,
        LogEventLevel minimumLogEventLevel,
        string? logFile
    ) {
        var logConfiguration = new LoggerConfiguration()
            .MinimumLevel.Is(minimumLogEventLevel)
            .WriteTo.Console();

        if (logFile is not null)
            logConfiguration = logConfiguration.WriteTo.File(logFile, rollingInterval: RollingInterval.Infinite);

        Log.Logger = logConfiguration.CreateLogger();

        var toolVersion = typeof(Program).Assembly
            .GetCustomAttribute<AssemblyInformationalVersionAttribute>()!
            .InformationalVersion;
        Log.Information("Initializing NetcodePatcher v{Version:l}", toolVersion);

        Log.Debug("Provided 'plugins' input: {Plugins}", plugin);
        Log.Debug("Provided 'dependencies' input: {Dependencies}", dependencies);

        var pluginAssemblies = new List<FileInfo>();

        switch (plugin)
        {
            case DirectoryInfo directoryInfo:
                pluginAssemblies.AddRange(directoryInfo.GetFiles("*.dll", new EnumerationOptions { RecurseSubdirectories = true }));
                break;
            case FileInfo fileInfo:
                pluginAssemblies.Add(fileInfo);
                break;
        }

        Log.Information("Patching {Count} assemblies:\n{Assemblies}", pluginAssemblies.Count, pluginAssemblies.Select(x => x.Name));

        var dependencyAssemblies = new List<FileInfo>();
        foreach (var fileSystemInfo in dependencies)
        {
            switch (fileSystemInfo)
            {
                case DirectoryInfo directoryInfo:
                    dependencyAssemblies.AddRange(directoryInfo.GetFiles("*.dll", new EnumerationOptions { RecurseSubdirectories = true }));
                    break;
                case FileInfo fileInfo:
                    dependencyAssemblies.Add(fileInfo);
                    break;
            }
        }
        Log.Information("Found {Count} dependency assemblies:\n{Assemblies}", dependencyAssemblies.Count, dependencyAssemblies.Select(x => x.Name));

        var patcherConfiguration = new PatcherConfiguration {
            UnityVersion = Version.Parse(unityVersion),
            NetcodeVersion = Version.Parse(netcodeVersion),
            TransportVersion = Version.Parse(transportVersion),
            NativeCollectionSupport = nativeCollectionSupport,
        };
        var patcherLoader = new PatcherLoader(patcherConfiguration);
        var patchMethod = patcherLoader.PatchMethod;

        var stopwatch = Stopwatch.StartNew();

        void Patch(FileInfo pluginAssembly)
        {
            if (dependencyAssemblies is null)
                throw new NullReferenceException($"{nameof(dependencyAssemblies)} is null!");

            var inputPath = pluginAssembly.FullName;
            var outputPath = output ?? pluginAssembly.DirectoryName!;

            if (Path.GetFileNameWithoutExtension(pluginAssembly.Name).EndsWith("_original"))
            {
                Log.Information("Skipping : {FileName}", pluginAssembly.Name);
                return;
            }

            if (Directory.Exists(outputPath) || string.IsNullOrEmpty(Path.GetExtension(outputPath)))
            {
                Directory.CreateDirectory(outputPath);
                outputPath = Path.Combine(outputPath, noOverwrite ? $"{Path.GetFileNameWithoutExtension(pluginAssembly.Name)}_patched{Path.GetExtension(pluginAssembly.Name)}" : pluginAssembly.Name);
            }

            patchMethod.Invoke(null, [inputPath, outputPath, dependencyAssemblies.Select(info => info.FullName).ToArray()]);
        }

        if (disableParallel || pluginAssemblies.Count <= 1)
        {
            pluginAssemblies.ForEach(Patch);
        }
        else
        {
            Parallel.ForEach(pluginAssemblies, Patch);
        }

        stopwatch.Stop();
        Log.Information("Done in {Time}", stopwatch.Elapsed);
    }
}
