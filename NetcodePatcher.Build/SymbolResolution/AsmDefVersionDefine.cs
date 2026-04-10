using System;
using System.Diagnostics.CodeAnalysis;
using System.Text.Json.Serialization;
using NuGet.Versioning;

namespace NetcodePatcher.Build.SymbolResolution;

public class AsmDefVersionDefine
{
    [JsonPropertyName("name")]
    public string ResourceName { get; set; } = null!;

    [JsonPropertyName("expression")]
    public string VersionRangeExpression { get; set; } = null!;

    [JsonPropertyName("define")]
    public string DefineSymbol { get; set; } = null!;

    [MemberNotNullWhen(true, nameof(PackageVersionRange))]
    public bool ResourceIsPackage => !ResourceIsUnity;

    [MemberNotNullWhen(true, nameof(UnityVersionRange))]
    public bool ResourceIsUnity => ResourceName is "Unity";

    public VersionRange? PackageVersionRange {
        get
        {
            if (!ResourceIsPackage) return null;
            if (String.IsNullOrWhiteSpace(VersionRangeExpression)) return VersionRange.All;
            return VersionRange.Parse(VersionRangeExpression);
        }
    }

    public UnityVersionRange? UnityVersionRange {
        get
        {
            if (!ResourceIsUnity) return null;
            return UnityVersionRange.Parse(VersionRangeExpression);
        }
    }
}
