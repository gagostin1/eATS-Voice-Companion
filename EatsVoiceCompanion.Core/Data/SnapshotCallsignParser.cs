using System.Collections.ObjectModel;
using System.Text.RegularExpressions;

namespace EatsVoiceCompanion.Core.Data;

public static class SnapshotCallsignParser
{
    private static readonly Regex AircraftLinePattern =
        new(
            @"^(?<callsign>[A-Z][A-Z0-9]{1,6})\s+" +
            @"[NS]\d{4,6}(?:\.\d+)?/" +
            @"[EW]\d{5,7}(?:\.\d+)?(?:\s|$)",
            RegexOptions.Compiled |
            RegexOptions.IgnoreCase);

    public static IReadOnlyList<string> Parse(
        IEnumerable<string> lines)
    {
        ArgumentNullException.ThrowIfNull(lines);

        List<string> callsigns = new();

        HashSet<string> seen =
            new(StringComparer.OrdinalIgnoreCase);

        foreach (string? rawLine in lines)
        {
            if (string.IsNullOrWhiteSpace(rawLine))
            {
                continue;
            }

            Match match =
                AircraftLinePattern.Match(rawLine.Trim());

            if (!match.Success)
            {
                continue;
            }

            string callsign =
                match.Groups["callsign"]
                    .Value
                    .ToUpperInvariant();

            if (seen.Add(callsign))
            {
                callsigns.Add(callsign);
            }
        }

        return new ReadOnlyCollection<string>(
            callsigns);
    }
}