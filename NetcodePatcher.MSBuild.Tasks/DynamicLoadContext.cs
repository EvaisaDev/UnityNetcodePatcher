using System;
using System.Reflection;
using System.Runtime.Loader;

namespace NetcodePatcher.MSBuild;

class DynamicLoadContext : AssemblyLoadContext
    {
        private AssemblyDependencyResolver _resolver;

        public DynamicLoadContext(string name, string pluginPath) : base(name)
        {
            _resolver = new AssemblyDependencyResolver(pluginPath);
        }

        protected override Assembly? Load(AssemblyName assemblyName)
        {
            string? assemblyPath = _resolver.ResolveAssemblyToPath(assemblyName);
            return assemblyPath is not null ? LoadFromAssemblyPath(assemblyPath) : null;
        }

        protected override IntPtr LoadUnmanagedDll(string unmanagedDllName)
        {
            string? libraryPath = _resolver.ResolveUnmanagedDllToPath(unmanagedDllName);
            return libraryPath is not null ? LoadUnmanagedDllFromPath(libraryPath) : IntPtr.Zero;
        }
    }
