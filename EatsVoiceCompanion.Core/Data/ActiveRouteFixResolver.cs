using System.Collections.ObjectModel;
using System.Text.RegularExpressions;

namespace EatsVoiceCompanion.Core.Data;

public static partial class ActiveRouteFixResolver
{
    public static IReadOnlyDictionary<string, IReadOnlySet<string>> Resolve(
        IEnumerable<string> activeCallsigns,
        IReadOnlyDictionary<string, GeneratedAircraftRoute> routes,
        IEnumerable<DescendViaProcedure> procedures)
    {
        ArgumentNullException.ThrowIfNull(procedures);

        return Resolve(
            activeCallsigns,
            routes,
            procedures.Select(procedure => new RouteProcedure(
                procedure.Name,
                procedure.Destination,
                procedure.Fixes ?? new HashSet<string>(
                    StringComparer.OrdinalIgnoreCase))));
    }

    public static IReadOnlyDictionary<string, IReadOnlySet<string>> Resolve(
        IEnumerable<string> activeCallsigns,
        IReadOnlyDictionary<string, GeneratedAircraftRoute> routes,
        IEnumerable<RouteProcedure> procedures)
    {
        ArgumentNullException.ThrowIfNull(activeCallsigns);
        ArgumentNullException.ThrowIfNull(routes);
        ArgumentNullException.ThrowIfNull(procedures);

        RouteProcedure[] procedureArray = procedures.ToArray();
        HashSet<string> procedureNames = procedureArray
            .Select(item => item.Name)
            .ToHashSet(StringComparer.OrdinalIgnoreCase);
        Dictionary<string, IReadOnlySet<string>> result =
            new(StringComparer.OrdinalIgnoreCase);

        foreach (string callsign in activeCallsigns)
        {
            if (!routes.TryGetValue(callsign, out GeneratedAircraftRoute? route))
            {
                continue;
            }

            HashSet<string> fixes = new(StringComparer.OrdinalIgnoreCase);
            string[] routeElements = route.Route.Split(
                '.',
                StringSplitOptions.RemoveEmptyEntries |
                StringSplitOptions.TrimEntries);

            foreach (string rawElement in routeElements)
            {
                string element = rawElement.Trim().ToUpperInvariant();

                if (IsRouteFix(element, route, procedureNames))
                {
                    fixes.Add(element);
                }
            }

            foreach (RouteProcedure procedure in procedureArray.Where(
                         item => routeElements.Contains(
                             item.Name,
                             StringComparer.OrdinalIgnoreCase)))
            {
                fixes.UnionWith(procedure.Fixes);
            }

            if (fixes.Count > 0)
            {
                result[callsign] = fixes;
            }
        }

        return new ReadOnlyDictionary<string, IReadOnlySet<string>>(result);
    }

    private static bool IsRouteFix(
        string element,
        GeneratedAircraftRoute route,
        IReadOnlySet<string> procedureNames)
    {
        return FixPattern().IsMatch(element) &&
               !AirwayPattern().IsMatch(element) &&
               !procedureNames.Contains(element) &&
               !IsAirport(element, route.Departure) &&
               !IsAirport(element, route.Destination);
    }

    private static bool IsAirport(string value, string airport)
    {
        string normalizedAirport = airport.Trim().ToUpperInvariant();
        string shortAirport = normalizedAirport.Length == 4 &&
                              normalizedAirport[0] == 'K'
            ? normalizedAirport[1..]
            : normalizedAirport;

        return value == normalizedAirport || value == shortAirport;
    }

    [GeneratedRegex("^[A-Z][A-Z0-9]{1,7}$", RegexOptions.CultureInvariant)]
    private static partial Regex FixPattern();

    [GeneratedRegex(@"^[VQJT]\d{1,4}$", RegexOptions.CultureInvariant)]
    private static partial Regex AirwayPattern();
}
