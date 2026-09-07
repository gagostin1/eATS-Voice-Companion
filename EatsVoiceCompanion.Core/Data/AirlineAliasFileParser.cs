using System.Collections.ObjectModel;
using System.Text.RegularExpressions;

namespace EatsVoiceCompanion.Core.Data;

public static class AirlineAliasFileParser
{
    private static readonly Regex EntryPattern =
        new(
            @"^(?<designator>[A-Z0-9]{2,3})\s+" +
            @"(?<telephony>[A-Z0-9]+(?:_[A-Z0-9]+)*)" +
            @"(?:\s+\d{1,4}\s+\d{1,4})?$",
            RegexOptions.Compiled |
            RegexOptions.IgnoreCase);

    public static IReadOnlyDictionary<string, string> Parse(
        IEnumerable<string> lines)
    {
        ArgumentNullException.ThrowIfNull(lines);

        Dictionary<string, string> aliases =
            new(StringComparer.OrdinalIgnoreCase);

        HashSet<string> ambiguousAliases =
            new(StringComparer.OrdinalIgnoreCase);

        foreach (string? rawLine in lines)
        {
            if (string.IsNullOrWhiteSpace(rawLine))
            {
                continue;
            }

            string line = rawLine.Trim();

            if (line.StartsWith(';'))
            {
                continue;
            }

            int commentIndex = line.IndexOf(';');

            if (commentIndex >= 0)
            {
                line = line[..commentIndex].Trim();
            }

            Match match = EntryPattern.Match(line);

            if (!match.Success)
            {
                continue;
            }

            string designator =
                match.Groups["designator"]
                    .Value
                    .ToUpperInvariant();

            string spokenName =
                match.Groups["telephony"]
                    .Value
                    .Replace('_', ' ')
                    .ToUpperInvariant();

            if (ambiguousAliases.Contains(spokenName))
            {
                continue;
            }

            if (aliases.TryGetValue(
                    spokenName,
                    out string? existingDesignator) &&
                !string.Equals(
                    existingDesignator,
                    designator,
                    StringComparison.OrdinalIgnoreCase))
            {
                aliases.Remove(spokenName);
                ambiguousAliases.Add(spokenName);
                continue;
            }

            aliases[spokenName] = designator;
        }

        return new ReadOnlyDictionary<string, string>(
            aliases);
    }
}
