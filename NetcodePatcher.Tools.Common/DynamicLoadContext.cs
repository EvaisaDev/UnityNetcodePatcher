using System.Linq;
using System.Reflection;
using System.Runtime.Loader;

namespace NetcodePatcher.Tools.Common;

class DynamicLoadContext : AssemblyLoadContext
{
    private AssemblyDependencyResolver _resolver;

    public DynamicLoadContext(string name, string pluginPath) : base(name)
    {
        _resolver = new AssemblyDependencyResolver(pluginPath);
    }

    protected override Assembly? Load(AssemblyName assemblyName)
    {
        var loadedAssembly = AssemblyLoadContext.Default.Assemblies
            .FirstOrDefault(assembly => assembly.GetName() == assemblyName);
        if (loadedAssembly is not null) return loadedAssembly;

        string? assemblyPath = _resolver.ResolveAssemblyToPath(assemblyName);
        return assemblyPath is not null ? LoadFromAssemblyPath(assemblyPath) : null;
    }
}
