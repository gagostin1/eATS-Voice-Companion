using System.Globalization;
using System.Text.RegularExpressions;

namespace EatsVoiceCompanion.Core.Speech;

public static class ActiveCallsignPromptBuilder
{
    private static readonly Regex ActiveAirlinePattern =
        new(
            "^[A-Z0-9]{2,3}[0-9]{1,4}$",
            RegexOptions.Compiled);

    public static string Build(
        IEnumerable<string> activeCallsigns,
        IReadOnlyDictionary<string, string> airlineAliases,
        int maximumCallsigns = 40)
    {
        ArgumentNullException.ThrowIfNull(activeCallsigns);
        ArgumentNullException.ThrowIfNull(airlineAliases);

        if (maximumCallsigns < 1)
        {
            throw new ArgumentOutOfRangeException(
                nameof(maximumCallsigns),
                "The maximum must be at least one.");
        }

        AirlineDesignator[] designators =
            airlineAliases
                .Where(pair =>
                    !string.IsNullOrWhiteSpace(pair.Key) &&
                    !string.IsNullOrWhiteSpace(pair.Value))
                .GroupBy(
                    pair => pair.Value.Trim().ToUpperInvariant(),
                    StringComparer.OrdinalIgnoreCase)
                .Select(group => new AirlineDesignator(
                    group.Key,
                    SelectSpokenName(group)))
                .OrderByDescending(item =>
                    item.Designator.Length)
                .ToArray();

        List<string> promptCallsigns = new();

        HashSet<string> seen =
            new(StringComparer.OrdinalIgnoreCase);

        foreach (string? rawCallsign in activeCallsigns)
        {
            if (string.IsNullOrWhiteSpace(rawCallsign))
            {
                continue;
            }

            string callsign =
                rawCallsign.Trim().ToUpperInvariant();

            if (NNumberCallsignParser.IsValid(callsign))
            {
                string nNumberPhrase =
                    NNumberCallsignParser.ToPromptPhrase(callsign);

                if (seen.Add(nNumberPhrase))
                {
                    promptCallsigns.Add(nNumberPhrase);
                }

                if (promptCallsigns.Count >= maximumCallsigns)
                {
                    break;
                }

                continue;
            }

            if (!ActiveAirlinePattern.IsMatch(callsign))
            {
                continue;
            }

            foreach (AirlineDesignator airline in designators)
            {
                if (!callsign.StartsWith(
                        airline.Designator,
                        StringComparison.OrdinalIgnoreCase))
                {
                    continue;
                }

                string flightNumber =
                    callsign[airline.Designator.Length..];

                if (flightNumber.Length is < 1 or > 4 ||
                    !flightNumber.All(char.IsDigit))
                {
                    continue;
                }

                string promptCallsign =
                    $"{airline.SpokenName} {flightNumber}";

                if (seen.Add(promptCallsign))
                {
                    promptCallsigns.Add(promptCallsign);
                }

                break;
            }

            if (promptCallsigns.Count >= maximumCallsigns)
            {
                break;
            }
        }

        if (promptCallsigns.Count == 0)
        {
            return string.Empty;
        }

        return "Active aircraft callsigns: " +
               string.Join(
                   ". ",
                   promptCallsigns) +
               ".";
    }

    private static string SelectSpokenName(
        IGrouping<string, KeyValuePair<string, string>> group)
    {
        string spokenName =
            group
                .Select(pair => pair.Key.Trim())
                .OrderBy(name => name.Length)
                .First();

        if (spokenName.Length <= 4 &&
            spokenName.All(character =>
                !char.IsLetter(character) ||
                char.IsUpper(character)))
        {
            return spokenName;
        }

        return CultureInfo.InvariantCulture.TextInfo
            .ToTitleCase(spokenName.ToLowerInvariant());
    }

    private sealed record AirlineDesignator(
        string Designator,
        string SpokenName);
}
