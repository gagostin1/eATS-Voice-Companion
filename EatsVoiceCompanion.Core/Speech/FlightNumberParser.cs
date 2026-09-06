using System.Text;
using System.Text.RegularExpressions;

namespace EatsVoiceCompanion.Core.Speech;

public static class FlightNumberParser
{
    private static readonly IReadOnlyDictionary<string, string>
        SingleDigits =
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

    private static readonly IReadOnlyDictionary<string, string>
        Teens =
            new Dictionary<string, string>(
                StringComparer.OrdinalIgnoreCase)
            {
                ["ten"] = "10",
                ["eleven"] = "11",
                ["twelve"] = "12",
                ["thirteen"] = "13",
                ["fourteen"] = "14",
                ["fifteen"] = "15",
                ["sixteen"] = "16",
                ["seventeen"] = "17",
                ["eighteen"] = "18",
                ["nineteen"] = "19"
            };

    private static readonly IReadOnlyDictionary<string, string>
        Tens =
            new Dictionary<string, string>(
                StringComparer.OrdinalIgnoreCase)
            {
                ["twenty"] = "20",
                ["thirty"] = "30",
                ["forty"] = "40",
                ["fifty"] = "50",
                ["sixty"] = "60",
                ["seventy"] = "70",
                ["eighty"] = "80",
                ["ninety"] = "90"
            };

    public static string Parse(string spokenFlightNumber)
    {
        if (string.IsNullOrWhiteSpace(spokenFlightNumber))
        {
            throw new ArgumentException(
                "A spoken flight number is required.",
                nameof(spokenFlightNumber));
        }

        string normalized =
            NormalizeWords(spokenFlightNumber);

        if (normalized.All(char.IsDigit))
        {
            return ValidateResult(
                normalized,
                spokenFlightNumber);
        }

        string[] tokens = normalized.Split(
            ' ',
            StringSplitOptions.RemoveEmptyEntries);

        StringBuilder digits = new();

        for (int index = 0;
             index < tokens.Length;
             index++)
        {
            string token = tokens[index];

            if (token.All(char.IsDigit))
            {
                digits.Append(token);
                continue;
            }

            if (SingleDigits.TryGetValue(
                    token,
                    out string? singleDigit))
            {
                if (index + 1 < tokens.Length &&
                    tokens[index + 1] == "hundred")
                {
                    if (index + 2 != tokens.Length)
                    {
                        throw new ArgumentException(
                            "Numbers following 'hundred' " +
                            "are not supported yet.",
                            nameof(spokenFlightNumber));
                    }

                    digits.Append(singleDigit);
                    digits.Append("00");
                    index++;
                    continue;
                }

                if (index + 1 < tokens.Length &&
                    tokens[index + 1] == "thousand")
                {
                    if (index + 2 != tokens.Length)
                    {
                        throw new ArgumentException(
                            "Numbers following 'thousand' " +
                            "are not supported.",
                            nameof(spokenFlightNumber));
                    }

                    digits.Append(singleDigit);
                    digits.Append("000");
                    index++;
                    continue;
                }

                digits.Append(singleDigit);
                continue;
            }

            if (Teens.TryGetValue(
                    token,
                    out string? teenDigits))
            {
                digits.Append(teenDigits);
                continue;
            }

            if (Tens.TryGetValue(
                    token,
                    out string? tensDigits))
            {
                int groupedValue =
                    int.Parse(tensDigits);

                if (index + 1 < tokens.Length &&
                    SingleDigits.TryGetValue(
                        tokens[index + 1],
                        out string? followingDigit) &&
                    followingDigit != "0")
                {
                    groupedValue +=
                        int.Parse(followingDigit);

                    index++;
                }

                digits.Append(groupedValue);
                continue;
            }

            throw new ArgumentException(
                $"'{token}' is not recognized in a flight number.",
                nameof(spokenFlightNumber));
        }

        return ValidateResult(
            digits.ToString(),
            spokenFlightNumber);
    }

    private static string ValidateResult(
        string digits,
        string originalValue)
    {
        if (!Regex.IsMatch(digits, "^[0-9]{1,4}$"))
        {
            throw new ArgumentException(
                "A flight number must contain one to four digits.",
                nameof(originalValue));
        }

        return digits;
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
}