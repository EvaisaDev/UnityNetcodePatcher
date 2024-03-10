using System;
using System.IO;

namespace NetcodePatcher.Tools.Common;

public record PatcherConfiguration()
{
    public required Version UnityVersion { get; init; }
    public required Version NetcodeVersion { get; init; }
    public required Version TransportVersion { get; init; }
    public required bool NativeCollectionSupport { get; init; }

    public string PatcherAssemblyFileName => Path.Combine(
        $"uv{UnityVersion.Major}.{UnityVersion.Minor}",
        $"tv{TransportVersion}",
        $"NetcodePatcher.uv{UnityVersion.Major}.{UnityVersion.Minor}.nv{NetcodeVersion}.tv{TransportVersion}.{(NativeCollectionSupport ? "withNativeCollectionSupport" : "withoutNativeCollectionSupport")}.dll"
    );

    public override string ToString()
    {
        return $"PatcherConfiguration {{\nUnity {UnityVersion},\nNetcode {NetcodeVersion},\nTransport {TransportVersion},\nNative collection support? {NativeCollectionSupport}\n}}";
    }
};
