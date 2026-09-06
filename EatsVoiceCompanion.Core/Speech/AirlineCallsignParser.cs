using System.Text.RegularExpressions;

namespace EatsVoiceCompanion.Core.Speech;

public sealed class AirlineCallsignParser
{
    private static readonly Regex CompactCallsignPattern =
        new(
            "^[A-Z]{2,3}[0-9]{1,4}$",
            RegexOptions.Compiled);

    private static readonly Regex AirlineDesignatorPattern =
        new(
            "^[A-Z0-9]{2,3}$",
            RegexOptions.Compiled);

    private readonly AirlineAlias[] _aliases;

    public AirlineCallsignParser(
        IReadOnlyDictionary<string, string> airlineAliases)
    {
        ArgumentNullException.ThrowIfNull(airlineAliases);

        if (airlineAliases.Count == 0)
        {
            throw new ArgumentException(
                "At least one airline alias is required.",
                nameof(airlineAliases));
        }

        _aliases = airlineAliases
            .Select(pair => new AirlineAlias(
                NormalizeWords(pair.Key),
                NormalizeDesignator(pair.Value)))
            .OrderByDescending(alias => alias.SpokenName.Length)
            .ToArray();
    }

    public string Parse(string spokenCallsign)
    {
        if (string.IsNullOrWhiteSpace(spokenCallsign))
        {
            throw new ArgumentException(
                "A spoken callsign is required.",
                nameof(spokenCallsign));
        }

        string normalized = NormalizeWords(spokenCallsign);

        string compact =
            normalized.Replace(" ", string.Empty)
                .ToUpperInvariant();

        if (CompactCallsignPattern.IsMatch(compact))
        {
            return compact;
        }

        foreach (AirlineAlias alias in _aliases)
        {
            string prefix = alias.SpokenName + " ";

            if (!normalized.StartsWith(
                    prefix,
                    StringComparison.OrdinalIgnoreCase))
            {
                continue;
            }

            string flightNumberText =
                normalized[prefix.Length..];

            string flightNumber =
                FlightNumberParser.Parse(flightNumberText);

            string callsign =
                alias.Designator + flightNumber;

            if (!CompactCallsignPattern.IsMatch(callsign))
            {
                throw new ArgumentException(
                    "The resulting callsign is not valid.",
                    nameof(spokenCallsign));
            }

            return callsign;
        }

        throw new ArgumentException(
            "The airline callsign was not recognized.",
            nameof(spokenCallsign));
    }

    private static string NormalizeWords(string value)
    {
        string normalized = Regex.Replace(
            value.Trim().ToLowerInvariant(),
            @"[^\p{L}\p{N}]+",
            " ");

        return Regex.Replace(
            normalized,
            @"\s+",
            " ").Trim();
    }

    private static string NormalizeDesignator(
        string designator)
    {
        if (string.IsNullOrWhiteSpace(designator))
        {
            throw new ArgumentException(
                "An airline designator is required.",
                nameof(designator));
        }

        string normalized =
            designator.Trim().ToUpperInvariant();

        if (!AirlineDesignatorPattern.IsMatch(normalized))
        {
            throw new ArgumentException(
                "An airline designator must contain " +
                "two or three letters or digits.",
                nameof(designator));
        }

        return normalized;
    }

    private sealed record AirlineAlias(
        string SpokenName,
        string Designator);
}