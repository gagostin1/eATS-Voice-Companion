using System.Text;
using System.Text.RegularExpressions;

namespace EatsVoiceCompanion.Core.Speech;

public static class AviationFrequencyParser
{
    private static readonly IReadOnlyDictionary<string, char> DigitWords =
        new Dictionary<string, char>(StringComparer.OrdinalIgnoreCase)
        {
            ["zero"] = '0',
            ["oh"] = '0',
            ["one"] = '1',
            ["two"] = '2',
            ["three"] = '3',
            ["tree"] = '3',
            ["four"] = '4',
            ["fower"] = '4',
            ["five"] = '5',
            ["fife"] = '5',
            ["six"] = '6',
            ["seven"] = '7',
            ["eight"] = '8',
            ["nine"] = '9',
            ["niner"] = '9'
        };

    public static int ParseHundredths(string spokenFrequency)
    {
        if (string.IsNullOrWhiteSpace(spokenFrequency))
        {
            throw new ArgumentException(
                "A frequency is required.",
                nameof(spokenFrequency));
        }

        string normalized = Regex.Replace(
            spokenFrequency.Trim().ToLowerInvariant(),
            @"[^\p{L}\p{N}.]+",
            " ");
        normalized = Regex.Replace(normalized, @"\s+", " ").Trim();
        normalized = Regex.Replace(normalized, @"\bdecimal\b", "point");
        normalized = normalized.Replace('.', '|');
        normalized = Regex.Replace(normalized, @"\bpoint\b", "|");

        if (!normalized.Contains('|'))
        {
            Match numericParts = Regex.Match(
                normalized,
                @"^(?<whole>[0-9]{3}) (?<fraction>[0-9]{1,3})$");

            if (numericParts.Success)
            {
                normalized = numericParts.Groups["whole"].Value + "|" +
                    numericParts.Groups["fraction"].Value;
            }
        }

        string[] parts = normalized.Split('|');

        if (parts.Length != 2)
        {
            throw new ArgumentException(
                "State the frequency with 'point', such as one three two " +
                "point three seven.",
                nameof(spokenFrequency));
        }

        string wholeDigits = ParseDigits(parts[0]);
        string fractionDigits = ParseDigits(parts[1]);

        if (wholeDigits.Length != 3 ||
            fractionDigits.Length is < 1 or > 3)
        {
            throw new ArgumentException(
                "A VHF frequency requires three digits before the point " +
                "and one to three after it.",
                nameof(spokenFrequency));
        }

        if (fractionDigits.Length == 3)
        {
            if (fractionDigits[2] is not ('0' or '5'))
            {
                throw new ArgumentException(
                    "Only documented 25 kHz frequency channels are supported.",
                    nameof(spokenFrequency));
            }

            fractionDigits = fractionDigits[..2];
        }

        fractionDigits = fractionDigits.PadRight(2, '0');
        int hundredths = int.Parse(wholeDigits + fractionDigits);
        int finalHundredth = hundredths % 10;

        if (hundredths is < 11800 or > 13697 ||
            finalHundredth is not (0 or 2 or 5 or 7))
        {
            throw new ArgumentOutOfRangeException(
                nameof(spokenFrequency),
                "Frequency must be a valid 25 kHz civil VHF channel from " +
                "118.000 through 136.975 MHz.");
        }

        return hundredths;
    }

    public static bool IsDigitToken(string token) =>
        token.All(char.IsDigit) || DigitWords.ContainsKey(token);

    private static string ParseDigits(string value)
    {
        StringBuilder result = new();

        foreach (string token in value.Split(
                     ' ',
                     StringSplitOptions.RemoveEmptyEntries))
        {
            if (token.All(char.IsDigit))
            {
                result.Append(token);
            }
            else if (DigitWords.TryGetValue(token, out char digit))
            {
                result.Append(digit);
            }
            else
            {
                throw new ArgumentException(
                    $"'{token}' is not a recognized frequency digit.",
                    nameof(value));
            }
        }

        return result.ToString();
    }
}
