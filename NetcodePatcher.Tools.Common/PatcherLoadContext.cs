using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Reflection;
using System.Runtime.Loader;

namespace NetcodePatcher.Tools.Common;

class PatcherLoadContext : AssemblyLoadContext
{
    private static readonly HashSet<string> SharedDependencyAssemblyNames = [ "Serilog" ];
    private readonly AssemblyDependencyResolver _resolver;

    public PatcherLoadContext(string name, string pluginPath) : base(name)
    {
        _resolver = new AssemblyDependencyResolver(pluginPath);
    }

    protected override Assembly? Load(AssemblyName assemblyName)
    {
        var loadedAssembly = Default.Assemblies
            .FirstOrDefault(assembly => assembly.GetName() == assemblyName);
        if (loadedAssembly is not null) return loadedAssembly;

        string? assemblyPath = _resolver.ResolveAssemblyToPath(assemblyName);
        if (assemblyPath is null) return null;

        if (SharedDependencyAssemblyNames.Contains(Path.GetFileNameWithoutExtension(assemblyPath))) {
            return Default.LoadFromAssemblyPath(assemblyPath);
        }

        return LoadFromAssemblyPath(assemblyPath);
    }
}
