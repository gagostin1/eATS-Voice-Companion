using System.Collections.ObjectModel;
using System.Text.RegularExpressions;

namespace EatsVoiceCompanion.Core.Data;

public sealed record DescendViaProcedure(
    string Name,
    string Destination);

public static partial class DescendViaProcedureParser
{
    public static IReadOnlyList<DescendViaProcedure> Parse(
        IEnumerable<string> lines)
    {
        ArgumentNullException.ThrowIfNull(lines);

        List<DescendViaProcedure> procedures = new();
        HashSet<string> seen = new(StringComparer.OrdinalIgnoreCase);

        foreach (string? line in lines)
        {
            if (string.IsNullOrWhiteSpace(line) ||
                line.TrimStart().StartsWith(';'))
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

            if (seen.Add(key))
            {
                procedures.Add(new DescendViaProcedure(name, destination));
            }
        }

        return new ReadOnlyCollection<DescendViaProcedure>(procedures);
    }

    [GeneratedRegex(
        "^(?<name>[A-Z][A-Z0-9]{1,7}[1-9])\\s+" +
        ".*(?:^|\\s)\\*DV\\d{2,3}" +
        ".*\\s(?<destination>[A-Z][A-Z0-9]{2,4})$",
        RegexOptions.IgnoreCase | RegexOptions.CultureInvariant)]
    private static partial Regex ProcedurePattern();
}
