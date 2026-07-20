namespace Nexo.Core.Updates;

public sealed record ReleaseVersion(
    int Major,
    int Minor,
    int Patch,
    string PreRelease = "") : IComparable<ReleaseVersion>
{
    public bool IsPreRelease => !string.IsNullOrWhiteSpace(PreRelease);

    public static bool TryParse(string? value, out ReleaseVersion version)
    {
        version = new ReleaseVersion(0, 0, 0);
        if (string.IsNullOrWhiteSpace(value))
        {
            return false;
        }

        var normalized = value.Trim();
        if (normalized.StartsWith('v') || normalized.StartsWith('V'))
        {
            normalized = normalized[1..];
        }

        var buildSeparator = normalized.IndexOf('+');
        if (buildSeparator >= 0)
        {
            normalized = normalized[..buildSeparator];
        }

        var preRelease = string.Empty;
        var preReleaseSeparator = normalized.IndexOf('-');
        if (preReleaseSeparator >= 0)
        {
            preRelease = normalized[(preReleaseSeparator + 1)..].Trim();
            normalized = normalized[..preReleaseSeparator];
        }

        var numbers = normalized.Split('.', StringSplitOptions.TrimEntries);
        var patch = 0;
        if (numbers.Length is < 2 or > 4 ||
            !int.TryParse(numbers[0], out var major) ||
            !int.TryParse(numbers[1], out var minor) ||
            (numbers.Length >= 3 && !int.TryParse(numbers[2], out patch)))
        {
            return false;
        }
        if (major < 0 || minor < 0 || patch < 0)
        {
            return false;
        }

        version = new ReleaseVersion(major, minor, patch, preRelease);
        return true;
    }

    public int CompareTo(ReleaseVersion? other)
    {
        if (other is null)
        {
            return 1;
        }

        var result = Major.CompareTo(other.Major);
        if (result != 0) return result;

        result = Minor.CompareTo(other.Minor);
        if (result != 0) return result;

        result = Patch.CompareTo(other.Patch);
        if (result != 0) return result;

        if (!IsPreRelease && !other.IsPreRelease) return 0;
        if (!IsPreRelease) return 1;
        if (!other.IsPreRelease) return -1;

        return ComparePreRelease(PreRelease, other.PreRelease);
    }

    public override string ToString() =>
        IsPreRelease
            ? $"{Major}.{Minor}.{Patch}-{PreRelease}"
            : $"{Major}.{Minor}.{Patch}";

    private static int ComparePreRelease(string left, string right)
    {
        var leftParts = left.Split('.', StringSplitOptions.RemoveEmptyEntries);
        var rightParts = right.Split('.', StringSplitOptions.RemoveEmptyEntries);
        var count = Math.Max(leftParts.Length, rightParts.Length);

        for (var index = 0; index < count; index++)
        {
            if (index >= leftParts.Length) return -1;
            if (index >= rightParts.Length) return 1;

            var leftPart = leftParts[index];
            var rightPart = rightParts[index];
            var leftIsNumber = int.TryParse(leftPart, out var leftNumber);
            var rightIsNumber = int.TryParse(rightPart, out var rightNumber);

            int comparison;
            if (leftIsNumber && rightIsNumber)
            {
                comparison = leftNumber.CompareTo(rightNumber);
            }
            else if (leftIsNumber)
            {
                comparison = -1;
            }
            else if (rightIsNumber)
            {
                comparison = 1;
            }
            else
            {
                comparison = string.Compare(
                    leftPart,
                    rightPart,
                    StringComparison.OrdinalIgnoreCase);
            }

            if (comparison != 0)
            {
                return comparison;
            }
        }

        return 0;
    }
}
