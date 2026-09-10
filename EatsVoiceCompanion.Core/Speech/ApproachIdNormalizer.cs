using System.Text;
using System.Text.RegularExpressions;

namespace EatsVoiceCompanion.Core.Speech;

public static partial class ApproachIdNormalizer
{
    private static readonly IReadOnlyDictionary<string, string> TokenAliases =
        new Dictionary<string, string>(StringComparer.OrdinalIgnoreCase)
        {
            ["zero"] = "0",
            ["oh"] = "0",
            ["one"] = "1",
            ["two"] = "2",
            ["three"] = "3",
            ["tree"] = "3",
            ["four"] = "4",
            ["fower"] = "4",
            ["five"] = "5",
            ["fife"] = "5",
            ["six"] = "6",
            ["seven"] = "7",
            ["eight"] = "8",
            ["nine"] = "9",
            ["niner"] = "9",
            ["left"] = "L",
            ["right"] = "R",
            ["center"] = "C",
            ["centre"] = "C",
            ["zulu"] = "Z",
            ["yankee"] = "Y",
            ["xray"] = "X",
            ["alpha"] = "A",
            ["visual"] = "VA",
            ["localizer"] = "LOC"
        };

    public static string Normalize(string spokenApproach)
    {
        if (string.IsNullOrWhiteSpace(spokenApproach))
        {
            throw new ArgumentException(
                "An approach identifier is required.",
                nameof(spokenApproach));
        }

        string normalized = Regex.Replace(
            spokenApproach.Trim().ToLowerInvariant(),
            @"[^\p{L}\p{N}]+",
            " ");
        string[] tokens = Regex.Split(normalized.Trim(), @"\s+");
        StringBuilder identifier = new();

        for (int index = 0; index < tokens.Length; index++)
        {
            string token = tokens[index];

            if (token is "the" or "approach" or "runway")
            {
                continue;
            }

            if (token == "back" &&
                index + 1 < tokens.Length &&
                tokens[index + 1] == "course")
            {
                identifier.Append("BC");
                index++;
                continue;
            }

            if (token == "x" &&
                index + 1 < tokens.Length &&
                tokens[index + 1] == "ray")
            {
                identifier.Append('X');
                index++;
                continue;
            }

            if (TokenAliases.TryGetValue(token, out string? replacement))
            {
                identifier.Append(replacement);
                continue;
            }

            if (!token.All(char.IsLetterOrDigit))
            {
                throw new ArgumentException(
                    $"'{token}' is not valid in an approach identifier.",
                    nameof(spokenApproach));
            }

            identifier.Append(token.ToUpperInvariant());
        }

        string result = identifier.ToString();

        if (!ApproachPattern().IsMatch(result) ||
            !KnownTypePattern().IsMatch(result))
        {
            throw new ArgumentException(
                "Use a supported approach type and runway or database ID, " +
                "such as ILS25L, RNAVY31, LOCBC13, VORA, or VA5.",
                nameof(spokenApproach));
        }

        return result;
    }

    [GeneratedRegex("^[A-Z0-9]{3,12}$", RegexOptions.CultureInvariant)]
    private static partial Regex ApproachPattern();

    [GeneratedRegex(
        "^(?:ILS|RNAV|NDB|VOR|GPS|LDA|LOC|VA)[A-Z0-9]+$",
        RegexOptions.CultureInvariant)]
    private static partial Regex KnownTypePattern();
}
