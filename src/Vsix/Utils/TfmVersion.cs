using System;
using System.Text.RegularExpressions;

namespace Disasmo;

public class TfmVersion : IComparable<TfmVersion>
{
    private static readonly Regex tfmRegex = new Regex(
        @"^(?<moniker>[a-z]+)(?<major>\d+)(?:\.(?<minor>\d+))(?:\.(?<patch>\d+))?|(?<major>\d)(?<minor>\d)?(?<patch>\d+)?",
        RegexOptions.Compiled | RegexOptions.IgnoreCase);

    public string Moniker { get; set; }
    public int? Major { get; set; }
    public int? Minor { get; set; }
    public int? Patch { get; set; }

    public int CompareTo(TfmVersion other)
    {
        if (other == null)
            return 1;

        var majorCmp = Nullable.Compare(Major, other.Major);
        if (majorCmp != 0)
            return majorCmp;

        var minorCmp = Nullable.Compare(Minor, other.Minor);
        if (minorCmp != 0)
            return minorCmp;

        return Nullable.Compare(Patch, other.Patch);
    }

    public override string ToString()
    {
        return $"{Moniker} {Major}.{Minor}.{Patch}";
    }

    public static TfmVersion Parse(string tfm)
    {
        var match = tfmRegex.Match(tfm);
        if (!match.Success)
            return null;

        var moniker = match.Groups["moniker"].Value;
        var major = match.Groups["major"].Success ? int.Parse(match.Groups["major"].Value) : (int?) null;
        var minor = match.Groups["minor"].Success ? int.Parse(match.Groups["minor"].Value) : (int?) null;
        var patch = match.Groups["patch"].Success ? int.Parse(match.Groups["patch"].Value) : (int?) null;

        return new TfmVersion
        {
            Moniker = moniker,
            Major = major,
            Minor = minor,
            Patch = patch,
        };
    }
}
