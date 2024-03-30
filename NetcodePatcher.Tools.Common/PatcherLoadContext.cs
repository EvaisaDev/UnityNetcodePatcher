using System.Collections.Generic;
using System.Diagnostics.CodeAnalysis;
using System.IO;
using System.Linq;
using System.Reflection;
using System.Runtime.Loader;

namespace NetcodePatcher.Tools.Common;

class PatcherLoadContext : AssemblyLoadContext
{
    private static readonly HashSet<string> SpecificAssemblyNames = [ "NetcodePatcher", "Unity.Netcode.Runtime" ];
    private static readonly HashSet<string> SharedDependencyAssemblyNames = [ "Serilog" ];
    private readonly PatcherConfiguration _configuration;

    public PatcherLoadContext(string name, PatcherConfiguration configuration) : base(name)
    {
        _configuration = configuration;
    }

    protected override Assembly? Load(AssemblyName assemblyName)
    {
        var loadedAssembly = Default.Assemblies
            .FirstOrDefault(assembly => assembly.GetName() == assemblyName);
        if (loadedAssembly is not null) return loadedAssembly;

        if (assemblyName.Name is null) return null;

        if (SharedDependencyAssemblyNames.Contains(assemblyName.Name))
            return Default.LoadFromAssemblyName(assemblyName);

        string? assemblyPath = ResolveAssemblyToPath(assemblyName);
        if (assemblyPath is null) return null;
        return LoadFromAssemblyPath(assemblyPath);
    }

    private string? ResolveAssemblyToPath(AssemblyName assemblyName)
    {
        if (assemblyName.Name is null) return null;

        if (SpecificAssemblyNames.Contains(assemblyName.Name))
        {
            if (TryResolveSpecificAssemblyToPath(assemblyName, out var specificPath))
                return specificPath;
        }

        if (TryResolveCommonAssemblyToPath(assemblyName, out var commonPath))
            return commonPath;

        return null;

        bool TryResolveCommonAssemblyToPath(AssemblyName assemblyName, [MaybeNullWhen(false)] out string path)
        {
            path = Path.Combine(_configuration.PatcherCommonAssemblyDir, $"{assemblyName.Name}.dll");
            if (File.Exists(path))
                return true;

            path = null;
            return false;
        }

        bool TryResolveSpecificAssemblyToPath(AssemblyName assemblyName, [MaybeNullWhen(false)] out string path)
        {
            path = Path.Combine(_configuration.PatcherNetcodeSpecificAssemblyDir, $"{assemblyName.Name}.dll");
            if (File.Exists(path))
                return true;

            path = null;
            return false;
        }
    }
}
