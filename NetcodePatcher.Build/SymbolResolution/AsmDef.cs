using System.Text.Json.Serialization;

namespace NetcodePatcher.Build.SymbolResolution;

public class AsmDef
{
    [JsonPropertyName("rootNamespace")]
    public string? RootNamespace { get; set; }

    [JsonPropertyName("versionDefines")]
    public AsmDefVersionDefine[] VersionDefines { get; set; } = [];
}
