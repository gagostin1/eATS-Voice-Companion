namespace EatsVoiceCompanion.Core.Speech;

public static class AviationDistanceParser
{
    private static readonly IReadOnlyDictionary<string, int> SmallNumbers =
        new Dictionary<string, int>(StringComparer.Ordinal)
        {
            ["zero"] = 0,
            ["one"] = 1,
            ["two"] = 2,
            ["three"] = 3,
            ["four"] = 4,
            ["five"] = 5,
            ["six"] = 6,
            ["seven"] = 7,
            ["eight"] = 8,
            ["niner"] = 9,
            ["nine"] = 9,
            ["ten"] = 10,
            ["eleven"] = 11,
            ["twelve"] = 12,
            ["thirteen"] = 13,
            ["fourteen"] = 14,
            ["fifteen"] = 15,
            ["sixteen"] = 16,
            ["seventeen"] = 17,
            ["eighteen"] = 18,
            ["nineteen"] = 19
        };

    private static readonly IReadOnlyDictionary<string, int> Tens =
        new Dictionary<string, int>(StringComparer.Ordinal)
        {
            ["twenty"] = 20,
            ["thirty"] = 30,
            ["forty"] = 40,
            ["fifty"] = 50,
            ["sixty"] = 60,
            ["seventy"] = 70,
            ["eighty"] = 80,
            ["ninety"] = 90
        };

    public static int Parse(string spokenDistance)
    {
        if (string.IsNullOrWhiteSpace(spokenDistance))
        {
            throw new ArgumentException(
                "A cross distance is required.",
                nameof(spokenDistance));
        }

        string normalized = spokenDistance.Trim().ToLowerInvariant();

        if (int.TryParse(normalized, out int numeric))
        {
            return Validate(numeric, spokenDistance);
        }

        try
        {
            return Validate(
                AviationNumberParser.Parse(normalized),
                spokenDistance);
        }
        catch (ArgumentException)
        {
            // Cardinal mileage such as "ten" is normal ATC phraseology.
        }

        string[] words = normalized.Split(
            ' ',
            StringSplitOptions.RemoveEmptyEntries);
        int hundredIndex = Array.IndexOf(words, "hundred");
        int result;

        if (hundredIndex >= 0)
        {
            if (hundredIndex != 1 ||
                !SmallNumbers.TryGetValue(words[0], out int hundreds) ||
                hundreds is < 1 or > 9)
            {
                throw Invalid(spokenDistance);
            }

            result = hundreds * 100 + ParseUnderHundred(words[2..]);
        }
        else
        {
            result = ParseUnderHundred(words);
        }

        return Validate(result, spokenDistance);
    }

    private static int ParseUnderHundred(string[] words)
    {
        if (words.Length == 0)
        {
            return 0;
        }

        if (words.Length == 1 &&
            SmallNumbers.TryGetValue(words[0], out int small))
        {
            return small;
        }

        if (words.Length is 1 or 2 &&
            Tens.TryGetValue(words[0], out int tens))
        {
            if (words.Length == 1)
            {
                return tens;
            }

            if (SmallNumbers.TryGetValue(words[1], out int units) &&
                units is >= 1 and <= 9)
            {
                return tens + units;
            }
        }

        throw Invalid(string.Join(' ', words));
    }

    private static int Validate(int value, string original)
    {
        if (value is < 1 or > 999)
        {
            throw new ArgumentOutOfRangeException(
                nameof(original),
                "Cross distance must be between 1 and 999 miles.");
        }

        return value;
    }

    private static ArgumentException Invalid(string value) =>
        new(
            $"'{value}' is not a recognized cross distance.",
            nameof(value));
}
