using System;
using System.IO;
using System.Reflection;
using Serilog;

namespace NetcodePatcher.Tools.Common;

public class PatcherLoader
{
    private Type? _patcher;

    public Type Patcher => _patcher ??= LoadPatcher();

    private string _netcodeVersion;

    public PatcherLoader(string netcodeVersion)
    {
        _netcodeVersion = netcodeVersion;
    }

    public MethodInfo PatchMethod {
        get {
            var patcherPatchMethod = Patcher.GetMethod("Patch", BindingFlags.Public | BindingFlags.Static);
            if (patcherPatchMethod is null)
                throw new Exception("Failed to find `public static` `Patch` member in loaded patcher Type.");

            return patcherPatchMethod;
        }
    }

    private Type LoadPatcher() => LoadPatcherAssembly(_netcodeVersion)
        .FindPatcherType();

    private static Assembly LoadPatcherAssembly(string netcodeVersion)
    {
        try {
            return LoadPatcherAssemblyUnsafe(netcodeVersion);
        }
        catch (FileNotFoundException exc) {
            throw new ArgumentException($"The supplied Unity Netcode for GameObjects version '{netcodeVersion}' is either unknown or unsupported.", exc);
        }
        catch (Exception exc) {
            throw new ArgumentException($"Failed to load patcher for Netcode {netcodeVersion}", exc);
        }
    }

    private static Assembly LoadPatcherAssemblyUnsafe(string netcodeVersion)
    {
        var executingAssemblyDir = Path.GetDirectoryName(typeof(PatcherLoader).Assembly.Location)!;
        var patcherLocation = Path.GetFullPath(Path.Combine(executingAssemblyDir, $"NetcodePatcher.nv{netcodeVersion}.dll"));
        Log.Debug("Loading patcher from {PatcherLocation}", patcherLocation);
        PatcherLoadContext patcherLoadContext = new PatcherLoadContext("PatcherLoadContext", patcherLocation);
        var patcherAssemblyName = new AssemblyName(Path.GetFileNameWithoutExtension(patcherLocation));

        Assembly patcherAssembly;
        try {
            patcherAssembly = patcherLoadContext.LoadFromAssemblyName(patcherAssemblyName);
        }
        catch (FileNotFoundException exc) {
            throw new ArgumentException($"The supplied Unity Netcode for GameObjects version '{netcodeVersion}' is either unknown or unsupported.", exc);
        }

        return patcherAssembly;
    }
}
