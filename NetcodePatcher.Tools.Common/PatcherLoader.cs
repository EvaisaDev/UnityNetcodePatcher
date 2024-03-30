using System;
using System.IO;
using System.Reflection;
using Serilog;

namespace NetcodePatcher.Tools.Common;

public class PatcherLoader
{
    private const string NetcodePatcherAssemblyNameString = "NetcodePatcher";
    private static readonly AssemblyName NetcodePatcherAssemblyName = new(NetcodePatcherAssemblyNameString);

    private Type? _patcher;

    public Type Patcher => _patcher ??= LoadPatcher();

    private readonly PatcherConfiguration _configuration;

    public PatcherLoader(PatcherConfiguration configuration)
    {
        _configuration = configuration;
    }

    public MethodInfo PatchMethod {
        get {
            var patcherPatchMethod = Patcher.GetMethod("Patch", BindingFlags.Public | BindingFlags.Static);
            if (patcherPatchMethod is null)
                throw new Exception("Failed to find `public static` `Patch` member in loaded patcher Type.");

            return patcherPatchMethod;
        }
    }

    private Type LoadPatcher() => LoadPatcherAssembly(_configuration)
        .FindPatcherType();

    private static Assembly LoadPatcherAssembly(PatcherConfiguration configuration)
    {
        try
        {
            return LoadPatcherAssemblyUnsafe(configuration);
        }
        catch (ArgumentException)
        {
            throw;
        }
        catch (FileNotFoundException exc) {
            throw new ArgumentException($"The supplied patch configuration is either unknown or unsupported.\n{configuration}", exc);
        }
        catch (Exception exc) {
            throw new ArgumentException($"Failed to load patcher for configuration {configuration}", exc);
        }
    }

    private static Assembly LoadPatcherAssemblyUnsafe(PatcherConfiguration configuration)
    {
        Log.Debug("Loading patcher from {PatcherLocation}",  configuration.PatcherAssemblyFile);
        PatcherLoadContext patcherLoadContext = new PatcherLoadContext("PatcherLoadContext", configuration);

        Assembly patcherAssembly;
        try {
            patcherAssembly = patcherLoadContext.LoadFromAssemblyName(NetcodePatcherAssemblyName);
        }
        catch (FileNotFoundException exc) {
            throw new ArgumentException($"The supplied patch configuration is either unknown or unsupported.\n{configuration}", exc);
        }

        return patcherAssembly;
    }
}
