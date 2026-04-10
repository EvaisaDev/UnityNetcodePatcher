using System;
using System.Diagnostics.CodeAnalysis;
using System.Linq;
using System.Text;

namespace NetcodePatcher.Build.SymbolResolution;

public class UnityVersionRange : IEquatable<UnityVersionRange>
{
    private readonly bool _includeMinVersion;
    private readonly bool _includeMaxVersion;
    private readonly UnityVersion? _minVersion;
    private readonly UnityVersion? _maxVersion;

    public static readonly UnityVersionRange All = new(includeMaxVersion: true);

    public static readonly UnityVersionRange None =
        new(UnityVersion.Parse("0.0.0a0"), false, UnityVersion.Parse("0.0.0a0"));

    protected UnityVersionRange(
        UnityVersion? minVersion = null,
        bool includeMinVersion = true,
        UnityVersion? maxVersion = null,
        bool includeMaxVersion = false
    ) {
        _minVersion = minVersion;
        _includeMinVersion = includeMinVersion;
        _maxVersion = maxVersion;
        _includeMaxVersion = includeMaxVersion;
    }

    [MemberNotNullWhen(true, nameof(MinVersion))]
    public bool HasLowerBound => _minVersion is not null;

    [MemberNotNullWhen(true, nameof(MaxVersion))]
    public bool HasUpperBound => _maxVersion is not null;

    [MemberNotNullWhen(true, nameof(MinVersion))]
    [MemberNotNullWhen(true, nameof(MaxVersion))]
    public bool HasLowerAndUpperBounds => HasLowerBound && HasUpperBound;

    [MemberNotNullWhen(true, nameof(MinVersion))]
    public bool IsMinInclusive => HasLowerBound && _includeMinVersion;

    [MemberNotNullWhen(true, nameof(MaxVersion))]
    public bool IsMaxInclusive => HasUpperBound && _includeMaxVersion;

    public UnityVersion? MinVersion => _minVersion;

    public UnityVersion? MaxVersion => _maxVersion;

    public bool Satisfies(UnityVersion version)
    {
        bool result = true;
        if (HasLowerBound) {
            if (IsMinInclusive)
                result &= MinVersion.CompareTo(version) <= 0;
            else {
                result &= MinVersion.CompareTo(version) < 0;
            }
        }
        if (HasUpperBound) {
            if (IsMaxInclusive)
                result &= MaxVersion.CompareTo(version) >= 0;
            else {
                result &= MaxVersion.CompareTo(version) > 0;
            }
        }
        return result;
    }

    public bool Equals(UnityVersionRange? other)
    {
        if (other is null) return false;
        if (ReferenceEquals(this, other)) return true;
        return _includeMinVersion == other._includeMinVersion && _includeMaxVersion == other._includeMaxVersion && Equals(_minVersion, other._minVersion) && Equals(_maxVersion, other._maxVersion);
    }

    public override bool Equals(object? obj)
    {
        return obj is UnityVersionRange other && Equals(other);
    }

    public override int GetHashCode()
    {
        return HashCode.Combine(_includeMinVersion, _includeMaxVersion, _minVersion, _maxVersion);
    }

    public static UnityVersionRange Parse(string value)
    {
        if (!TryParse(value, out var versionRange))
            throw new ArgumentException($"Provided value {value} is not a valid unity version range", nameof(value));
        return versionRange;
    }

    public static bool TryParse(string value, [NotNullWhen(true)] out UnityVersionRange? versionRange)
    {
        versionRange = null;
        if (value is null) return false;
        var trimmedValue = value.Trim();
        if (string.IsNullOrEmpty(trimmedValue))
            return false;

        bool includeMinVersion;
        bool includeMaxVersion;
        string? minStr;
        string? maxStr;
        UnityVersion? min = null;
        UnityVersion? max = null;

        if (value[0] == '(' || value[0] == '[') {
            switch (value[0]) {
                case '(':
                    includeMinVersion = false;
                    break;
                case '[':
                    includeMinVersion = true;
                    break;
                default:
                    return false;
            }

            switch (value[^1]) {
                case ')':
                    includeMaxVersion = false;
                    break;
                case ']':
                    includeMaxVersion = true;
                    break;
                default:
                    return false;
            }
            var parts = trimmedValue.Substring(1, value.Length - 2).Split(',');
            // at most two parts permitted
            if (parts.Length > 2) return false;
            // e.g. '[]', '(], '(,,]', '[,]' are not permitted
            if (parts.All(string.IsNullOrEmpty)) return false;
            // endpoints must both be included when there is only a single version string
            if (parts.Length == 1 && !(includeMinVersion & includeMaxVersion)) return false;

            minStr = parts[0];
            maxStr = parts[^1];
        } else {
            includeMinVersion = true;
            includeMaxVersion = false;
            minStr = trimmedValue;
            maxStr = null;
        }

        if (minStr.Trim() is "0") minStr = null;
        if (!string.IsNullOrWhiteSpace(minStr) && !UnityVersion.TryParse(minStr, out min))
            return false;
        if (!string.IsNullOrWhiteSpace(maxStr) && !UnityVersion.TryParse(maxStr, out max))
            return false;

        if (min is not null && max is not null) {
            var compare = min.CompareTo(max);
            // min strictly greater than max is not permitted
            if (compare > 0) return false;
            // e.g. '[1.10.2, 1.10.2]', '[1.5.6]' are permitted
            // and '(1.2.3, 1.2.3)' is permitted (eqv. to 'UnityVersionRange.None')
            // but '[1.2.3, 1.2.3)', '(1.2.3, 1.2.3]' are not permitted
            if (compare == 0 && includeMinVersion ^ includeMaxVersion) return false;
        }

        versionRange = new UnityVersionRange(min, includeMinVersion, max, includeMaxVersion);
        return true;
    }

    public override string ToString()
    {
        var builder = new StringBuilder();
        builder.Append(IsMinInclusive ? '[' : '(');
        if (HasLowerBound) builder.Append(MinVersion);
        builder.Append(", ");
        if (HasUpperBound) builder.Append(MaxVersion);
        builder.Append(IsMaxInclusive ? ']' : ')');
        return builder.ToString();
    }
}
