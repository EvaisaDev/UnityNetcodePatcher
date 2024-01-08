using System;
using System.Diagnostics;
using System.IO;
using System.Linq;
using System.Reflection;
using System.Threading.Tasks;
using Microsoft.Build.Framework;
using Serilog;
using Task = Microsoft.Build.Utilities.Task;

namespace NetcodePatcher.MSBuild;

public class NetcodePatchTask : Task
{
    [Required] 
    public ITaskItem[] Patch { get; set; } = null!;

    [Required]
    public ITaskItem[] ReferenceAssemblyPaths { get; set; } = null!;

    public string? NoOverwrite { get; set; }

    public string? DisableParallel { get; set; }
    
    public override bool Execute()
    {
        Serilog.Log.Logger = new LoggerConfiguration()
            .MinimumLevel.Information()
            .WriteTo.TaskLoggingHelper(Log)
            .CreateLogger();
        
        var toolVersion = typeof(NetcodePatchTask).Assembly
            .GetCustomAttribute<AssemblyInformationalVersionAttribute>()!
            .InformationalVersion;
        Serilog.Log.Information("Initializing NetcodePatcher v{Version:l}", toolVersion);
        
        var noOverwrite = false;
        if (!string.IsNullOrEmpty(NoOverwrite))
        {
            noOverwrite = bool.Parse(NoOverwrite);
        }
        
        var disableParallel = false;
        if (!string.IsNullOrEmpty(DisableParallel))
        {
            disableParallel = bool.Parse(DisableParallel);
        }
        
        var stopwatch = Stopwatch.StartNew();
        
        void RunPatch(ITaskItem patchSpecifier)
        {
            var pluginAssembly = new FileInfo(patchSpecifier.ItemSpec);
            
            var inputPath = pluginAssembly.FullName;
            var outputPath = pluginAssembly.DirectoryName!;
            
            if (patchSpecifier.GetMetadata("OutputPath") is { } rawOutput && !string.IsNullOrEmpty(rawOutput))
            {
                if (rawOutput.IndexOfAny(Path.GetInvalidPathChars()) != -1)
                    throw new FormatException($"String '{rawOutput}' is not a valid output path.");

                outputPath = rawOutput;
            }

            if (Path.GetFileNameWithoutExtension(pluginAssembly.Name).EndsWith("_original"))
            {
                Serilog.Log.Information("Skipping : {FileName}", pluginAssembly.Name);
                return;
            }
            
            if (Directory.Exists(outputPath) || string.IsNullOrEmpty(Path.GetExtension(outputPath)))
            {
                Directory.CreateDirectory(outputPath);
                outputPath = Path.Combine(outputPath, noOverwrite ? $"{Path.GetFileNameWithoutExtension(pluginAssembly.Name)}_patched{Path.GetExtension(pluginAssembly.Name)}" : pluginAssembly.Name);
            }
            
            Patcher.Patch(inputPath, outputPath, ReferenceAssemblyPaths.Select(info => info.ItemSpec).ToArray());
        }

        try
        {
            if (disableParallel || Patch.Length <= 1)
            {
                foreach (var taskItem in Patch)
                {
                    RunPatch(taskItem);
                }
            }
            else
            {
                Parallel.ForEach(Patch, RunPatch);
            }
        }
        catch (Exception exception)
        {
            Serilog.Log.Fatal(exception, "Netcode patching failed!");
            return false;
        }
        
        stopwatch.Stop();
        Serilog.Log.Information("Done in {Time}", stopwatch.Elapsed);
        
        return true;
    }
}