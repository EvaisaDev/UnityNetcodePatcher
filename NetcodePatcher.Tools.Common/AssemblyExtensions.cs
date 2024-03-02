using System;
using System.Linq;
using System.Reflection;

namespace NetcodePatcher.Tools.Common;

internal static class AssemblyExtensions
{
    public static Type FindPatcherType(this Assembly patcherAssembly) => patcherAssembly
        .GetTypes()
        .First(t => t is { IsPublic: true, Name: "Patcher" });
}
