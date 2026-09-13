using System.Collections.ObjectModel;
using System.Text.RegularExpressions;

namespace EatsVoiceCompanion.Core.Data;

public sealed record RouteProcedure(
    string Name,
    string Destination,
    IReadOnlySet<string> Fixes);

public static partial class RouteProcedureParser
{
    public static IReadOnlyList<RouteProcedure> Parse(
        IEnumerable<string> lines)
    {
        ArgumentNullException.ThrowIfNull(lines);

        Dictionary<string, ProcedureAccumulator> procedures =
            new(StringComparer.OrdinalIgnoreCase);

        foreach (string? line in lines)
        {
            if (string.IsNullOrWhiteSpace(line) ||
                line.TrimStart().StartsWith(';') ||
                line.TrimStart().StartsWith(
                    "*HOLD ",
                    StringComparison.OrdinalIgnoreCase))
            {
                continue;
            }

            Match match = ProcedurePattern().Match(line.Trim());

            if (!match.Success)
            {
                continue;
            }

            string name = match.Groups["name"].Value.ToUpperInvariant();
            string destination = match.Groups["destination"]
                .Value
                .ToUpperInvariant();
            string key = $"{name}|{destination}";

            if (!procedures.TryGetValue(key, out ProcedureAccumulator? item))
            {
                item = new ProcedureAccumulator(name, destination);
                procedures[key] = item;
            }

            foreach (string fix in ParseFixes(line, name, destination))
            {
                item.Fixes.Add(fix);
            }
        }

        return new ReadOnlyCollection<RouteProcedure>(
            procedures.Values
                .Select(item => new RouteProcedure(
                    item.Name,
                    item.Destination,
                    new HashSet<string>(
                        item.Fixes,
                        StringComparer.OrdinalIgnoreCase)))
                .ToArray());
    }

    private static IEnumerable<string> ParseFixes(
        string line,
        string procedureName,
        string destination)
    {
        foreach (string rawToken in line.Split(
                     ' ',
                     StringSplitOptions.RemoveEmptyEntries))
        {
            string token = rawToken.Trim().ToUpperInvariant();

            if (token.StartsWith('*'))
            {
                continue;
            }

            int constraintSeparator = token.IndexOf('/');
            if (constraintSeparator > 0)
            {
                token = token[..constraintSeparator];
            }

            if (token != procedureName &&
                token != destination &&
                FixPattern().IsMatch(token))
            {
                yield return token;
            }
        }
    }

    [GeneratedRegex(
        "^(?<name>[A-Z][A-Z0-9]{1,7}[0-9])\\s+" +
        ".*\\s(?<destination>[A-Z][A-Z0-9]{2,4})$",
        RegexOptions.IgnoreCase | RegexOptions.CultureInvariant)]
    private static partial Regex ProcedurePattern();

    [GeneratedRegex(
        "^[A-Z][A-Z0-9]{1,7}$",
        RegexOptions.CultureInvariant)]
    private static partial Regex FixPattern();

    private sealed record ProcedureAccumulator(
        string Name,
        string Destination)
    {
        public HashSet<string> Fixes { get; } =
            new(StringComparer.OrdinalIgnoreCase);
    }
}
