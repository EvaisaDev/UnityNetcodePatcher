using System.Collections.Generic;
using System.Text.Json.Serialization;

namespace NetcodePatcher.Build.SymbolResolution;

public class UnityPackagesLock
{
    [JsonPropertyName("dependencies")]
    public Dictionary<string, UnityPackageLock> Dependencies { get; set; } = [];
}
