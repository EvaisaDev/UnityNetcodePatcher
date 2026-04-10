using System;
using System.Collections.Generic;
using System.ComponentModel;
using System.Diagnostics.CodeAnalysis;
using System.Text.RegularExpressions;
using NetcodePatcher.Build.Util;
using NuGet.Versioning;

namespace NetcodePatcher.Build.SymbolResolution;

public enum UnityReleaseLine
{
    Alpha,
    Beta,
    Final,
    Patch,
    A = Alpha,
    B = Beta,
    F = Final,
    P = Patch,
}

[TypeConverter(typeof(UnityVersionConverter))]
public sealed class UnityVersion : IComparable<UnityVersion>, IEquatable<UnityVersion>
{
    public static bool operator <(UnityVersion? left, UnityVersion? right) => Comparer<UnityVersion>.Default.Compare(left, right) < 0;

    public static bool operator >(UnityVersion? left, UnityVersion? right) => Comparer<UnityVersion>.Default.Compare(left, right) > 0;

    public static bool operator <=(UnityVersion? left, UnityVersion? right) => Comparer<UnityVersion>.Default.Compare(left, right) <= 0;

    public static bool operator >=(UnityVersion? left, UnityVersion? right) => Comparer<UnityVersion>.Default.Compare(left, right) >= 0;

    private UnityVersion() { }

    private static readonly Regex UnityVersionRegex = new(@"^(?<ver>[\d\.]+)(?:(?<line>[a-zA-Z])(?<rev>\d+))?$");

    public required NuGetVersion Version { get; init;  }
    public required UnityReleaseLine ReleaseLine { get; init; }
    public required int Revision { get; init; }

    public int Major => Version.Major;
    public int Minor => Version.Minor;
    public int Patch => Version.Patch;

    public static UnityVersion Parse(string versionString)
    {
        if (!TryParse(versionString, out var unityVersion))
            throw new ArgumentException($"Provided value {versionString} is not a valid Unity version", nameof(versionString));
        return unityVersion;
    }

    public static bool TryParse(string value, [NotNullWhen(true)] out UnityVersion? unityVersion)
    {
        unityVersion = null;
        var match = UnityVersionRegex.Match(value);
        if (!match.Success) return false;

        if (!NuGetVersion.TryParse(match.Groups["ver"].Value, out var version)) return false;
        if (String.IsNullOrWhiteSpace(match.Groups["line"].Value)) {
            unityVersion = new() {
                Version = version, ReleaseLine = UnityReleaseLine.Alpha, Revision = 0,
            };
            return true;
        }

        if (!Enum.TryParse<UnityReleaseLine>(match.Groups["line"].Value, true, out var releaseLine)) return false;
        if (!int.TryParse(match.Groups["rev"].Value, out var revision)) return false;
        unityVersion = new() {
            Version = version, ReleaseLine = releaseLine, Revision = revision
        };
        return true;
    }

    public int CompareTo(UnityVersion? other)
    {
        if (ReferenceEquals(this, other)) return 0;
        if (other is null) return 1;
        var versionComparison = Version.CompareTo(other.Version);
        if (versionComparison != 0) return versionComparison;
        var releaseLineComparison = ReleaseLine.CompareTo(other.ReleaseLine);
        if (releaseLineComparison != 0) return releaseLineComparison;
        return Revision.CompareTo(other.Revision);
    }

    public bool Equals(UnityVersion? other)
    {
        if (other is null) return false;
        if (ReferenceEquals(this, other)) return true;
        return Version.Equals(other.Version) && ReleaseLine == other.ReleaseLine && Revision == other.Revision;
    }

    public override bool Equals(object? obj)
    {
        return obj is UnityVersion version && Equals(version);
    }

    public override int GetHashCode()
    {
        return HashCode.Combine(Version, (int)ReleaseLine, Revision);
    }

    public static char ReleaseLineChar(UnityReleaseLine line)
    {
        switch (line) {
            case UnityReleaseLine.A: return 'a';
            case UnityReleaseLine.B: return 'b';
            case UnityReleaseLine.F: return 'f';
            case UnityReleaseLine.P: return 'p';
        }
        throw new ArgumentOutOfRangeException(nameof(line));
    }

    public override string ToString()
    {
        return $"{Version}{ReleaseLineChar(ReleaseLine)}{Revision}";
    }
}
