using System.Text.Json.Serialization;
using NetcodePatcher.Build.Util;
using NuGet.Versioning;

namespace NetcodePatcher.Build.SymbolResolution;

public class UnityPackageLock
{
    [JsonPropertyName("version")]
    [JsonConverter(typeof(JsonNuGetVersionConverter))]
    public NuGetVersion Version { get; set; } = null!;
}
