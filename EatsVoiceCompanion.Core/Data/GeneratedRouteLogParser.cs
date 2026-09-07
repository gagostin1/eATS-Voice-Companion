using System.Collections.ObjectModel;
using System.Text.RegularExpressions;

namespace EatsVoiceCompanion.Core.Data;

public sealed record GeneratedAircraftRoute(
    string Callsign,
    string Departure,
    string Destination,
    string Route);

public static partial class GeneratedRouteLogParser
{
    public static IReadOnlyDictionary<string, GeneratedAircraftRoute> Parse(
        IEnumerable<string> lines)
    {
        ArgumentNullException.ThrowIfNull(lines);

        Dictionary<string, GeneratedAircraftRoute> routes =
            new(StringComparer.OrdinalIgnoreCase);

        foreach (string? line in lines)
        {
            if (string.IsNullOrWhiteSpace(line))
            {
                continue;
            }

            Match match = GeneratedRoutePattern().Match(line.Trim());

            if (!match.Success)
            {
                continue;
            }

            string callsign = match.Groups["callsign"]
                .Value
                .ToUpperInvariant();

            routes[callsign] = new GeneratedAircraftRoute(
                callsign,
                match.Groups["departure"].Value.ToUpperInvariant(),
                match.Groups["destination"].Value.ToUpperInvariant(),
                match.Groups["route"].Value.ToUpperInvariant());
        }

        return new ReadOnlyDictionary<string, GeneratedAircraftRoute>(
            routes);
    }

    [GeneratedRegex(
        "^Generated IFR\\s+\\d+\\s+" +
        "(?<callsign>[A-Z0-9]{2,7})\\s+" +
        "(?<departure>[A-Z0-9]{2,5})\\s+" +
        "(?<destination>[A-Z0-9]{2,5})\\s+" +
        "(?<route>\\S+)\\s*$",
        RegexOptions.IgnoreCase | RegexOptions.CultureInvariant)]
    private static partial Regex GeneratedRoutePattern();
}
