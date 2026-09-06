using System.Text;
using System.Text.RegularExpressions;

namespace EatsVoiceCompanion.Core.Speech;

public static class AviationNumberParser
{
    private static readonly IReadOnlyDictionary<string, string>
        DigitWords =
            new Dictionary<string, string>(
                StringComparer.OrdinalIgnoreCase)
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
                ["niner"] = "9"
            };

    public static int Parse(string spokenNumber)
    {
        if (string.IsNullOrWhiteSpace(spokenNumber))
        {
            throw new ArgumentException(
                "A spoken number is required.",
                nameof(spokenNumber));
        }

        string normalized = Regex.Replace(
            spokenNumber.Trim().ToLowerInvariant(),
            @"[^\p{L}\p{N}]+",
            " ");

        normalized = Regex.Replace(
            normalized,
            @"\s+",
            " ").Trim();

        if (int.TryParse(normalized, out int numericValue))
        {
            return numericValue;
        }

        string[] tokens = normalized.Split(
            ' ',
            StringSplitOptions.RemoveEmptyEntries);

        StringBuilder digits = new();

        foreach (string token in tokens)
        {
            if (token.All(char.IsDigit))
            {
                digits.Append(token);
                continue;
            }

            if (!DigitWords.TryGetValue(
                    token,
                    out string? digit))
            {
                throw new ArgumentException(
                    $"'{token}' is not a recognized aviation digit.",
                    nameof(spokenNumber));
            }

            digits.Append(digit);
        }

        if (!int.TryParse(digits.ToString(), out int result))
        {
            throw new ArgumentException(
                "The spoken number is too large.",
                nameof(spokenNumber));
        }

        return result;
    }
}