using System.Collections.ObjectModel;

namespace EatsVoiceCompanion.Core.Data;

public static class ActiveStarResolver
{
    public static IReadOnlyDictionary<string, string> Resolve(
        IEnumerable<string> activeCallsigns,
        IReadOnlyDictionary<string, GeneratedAircraftRoute> routes,
        IEnumerable<DescendViaProcedure> procedures)
    {
        ArgumentNullException.ThrowIfNull(activeCallsigns);
        ArgumentNullException.ThrowIfNull(routes);
        ArgumentNullException.ThrowIfNull(procedures);

        Dictionary<string, HashSet<string>> proceduresByDestination =
            BuildProcedureIndex(procedures);
        Dictionary<string, string> activeStars =
            new(StringComparer.OrdinalIgnoreCase);

        foreach (string callsign in activeCallsigns)
        {
            if (!routes.TryGetValue(callsign, out GeneratedAircraftRoute? route))
            {
                continue;
            }

            string destination = NormalizeAirport(route.Destination);

            if (!proceduresByDestination.TryGetValue(
                    destination,
                    out HashSet<string>? destinationProcedures))
            {
                continue;
            }

            string[] routeElements = route.Route.Split(
                '.',
                StringSplitOptions.RemoveEmptyEntries |
                StringSplitOptions.TrimEntries);

            string[] matches = routeElements
                .Where(destinationProcedures.Contains)
                .Distinct(StringComparer.OrdinalIgnoreCase)
                .ToArray();

            if (matches.Length == 1)
            {
                activeStars[callsign] = matches[0];
            }
        }

        return new ReadOnlyDictionary<string, string>(activeStars);
    }

    private static Dictionary<string, HashSet<string>> BuildProcedureIndex(
        IEnumerable<DescendViaProcedure> procedures)
    {
        Dictionary<string, HashSet<string>> result =
            new(StringComparer.OrdinalIgnoreCase);

        foreach (DescendViaProcedure procedure in procedures)
        {
            string destination = NormalizeAirport(procedure.Destination);

            if (!result.TryGetValue(
                    destination,
                    out HashSet<string>? names))
            {
                names = new HashSet<string>(
                    StringComparer.OrdinalIgnoreCase);
                result[destination] = names;
            }

            names.Add(procedure.Name);
        }

        return result;
    }

    private static string NormalizeAirport(string airport)
    {
        string normalized = airport.Trim().ToUpperInvariant();

        return normalized.Length == 4 && normalized[0] == 'K'
            ? normalized[1..]
            : normalized;
    }
}
