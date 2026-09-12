namespace EatsVoiceCompanion.Core.Speech;

public static class RouteFixMatcher
{
    private const double MinimumSimilarity = 0.72;
    private const double MinimumWinnerMargin = 0.12;
    private static readonly IReadOnlyDictionary<string, string>
        SpokenCharacters = new Dictionary<string, string>(
            StringComparer.OrdinalIgnoreCase)
        {
            ["alpha"] = "a",
            ["bravo"] = "b",
            ["charlie"] = "c",
            ["delta"] = "d",
            ["echo"] = "e",
            ["foxtrot"] = "f",
            ["golf"] = "g",
            ["hotel"] = "h",
            ["india"] = "i",
            ["juliet"] = "j",
            ["kilo"] = "k",
            ["lima"] = "l",
            ["mike"] = "m",
            ["november"] = "n",
            ["oscar"] = "o",
            ["papa"] = "p",
            ["quebec"] = "q",
            ["romeo"] = "r",
            ["sierra"] = "s",
            ["tango"] = "t",
            ["uniform"] = "u",
            ["victor"] = "v",
            ["whiskey"] = "w",
            ["xray"] = "x",
            ["yankee"] = "y",
            ["zulu"] = "z",
            ["zee"] = "z",
            ["zed"] = "z",
            ["eye"] = "i",
            ["oh"] = "o",
            ["zero"] = "0",
            ["one"] = "1",
            ["two"] = "2",
            ["three"] = "3",
            ["tree"] = "3",
            ["four"] = "4",
            ["five"] = "5",
            ["six"] = "6",
            ["seven"] = "7",
            ["eight"] = "8",
            ["nine"] = "9",
            ["niner"] = "9"
        };

    public static string? FindUniqueMatch(
        string spokenFix,
        IEnumerable<string> routeFixes)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(spokenFix);
        ArgumentNullException.ThrowIfNull(routeFixes);

        string normalizedSpoken = Normalize(spokenFix);
        Match[] matches = routeFixes
            .Where(value => !string.IsNullOrWhiteSpace(value))
            .Distinct(StringComparer.OrdinalIgnoreCase)
            .Select(value => new Match(
                value.Trim().ToUpperInvariant(),
                Similarity(normalizedSpoken, Normalize(value))))
            .OrderByDescending(match => match.Score)
            .ThenBy(match => match.Fix, StringComparer.OrdinalIgnoreCase)
            .ToArray();

        if (matches.Length == 0 || matches[0].Score < MinimumSimilarity)
        {
            return null;
        }

        if (matches.Length > 1 &&
            matches[0].Score - matches[1].Score < MinimumWinnerMargin)
        {
            return null;
        }

        return matches[0].Fix;
    }

    private static double Similarity(string left, string right)
    {
        if (left.Length == 0 || right.Length == 0)
        {
            return 0;
        }

        double editScore = 1.0 -
            (double)LevenshteinDistance(left, right) /
            Math.Max(left.Length, right.Length);
        string leftSkeleton = ConsonantSkeleton(left);
        string rightSkeleton = ConsonantSkeleton(right);
        double phoneticScore = leftSkeleton.Length > 0 &&
                               leftSkeleton == rightSkeleton &&
                               Math.Abs(left.Length - right.Length) <= 2
            ? 0.90
            : 0;

        return Math.Max(editScore, phoneticScore);
    }

    private static string Normalize(string value)
    {
        string[] tokens = value
            .ToLowerInvariant()
            .Split(
                [' ', '-', '.', ',', '/'],
                StringSplitOptions.RemoveEmptyEntries |
                StringSplitOptions.TrimEntries);

        if (tokens.Length > 1 &&
            tokens.All(token => SpokenCharacters.ContainsKey(token)))
        {
            return string.Concat(tokens.Select(token =>
                SpokenCharacters[token]));
        }

        return new string(value
            .ToLowerInvariant()
            .Where(char.IsLetterOrDigit)
            .ToArray());
    }

    private static string ConsonantSkeleton(string value)
    {
        char previous = '\0';
        List<char> result = new();

        foreach (char rawCharacter in value)
        {
            char character = rawCharacter switch
            {
                'z' => 's',
                'c' => 'k',
                _ => rawCharacter
            };

            if ("aeiouy".Contains(character) || character == previous)
            {
                continue;
            }

            result.Add(character);
            previous = character;
        }

        return new string(result.ToArray());
    }

    private static int LevenshteinDistance(string left, string right)
    {
        int[] previous = Enumerable.Range(0, right.Length + 1).ToArray();
        int[] current = new int[right.Length + 1];

        for (int leftIndex = 1; leftIndex <= left.Length; leftIndex++)
        {
            current[0] = leftIndex;

            for (int rightIndex = 1; rightIndex <= right.Length; rightIndex++)
            {
                int substitution = previous[rightIndex - 1] +
                    (left[leftIndex - 1] == right[rightIndex - 1] ? 0 : 1);
                current[rightIndex] = Math.Min(
                    Math.Min(current[rightIndex - 1] + 1,
                        previous[rightIndex] + 1),
                    substitution);
            }

            (previous, current) = (current, previous);
        }

        return previous[right.Length];
    }

    private sealed record Match(string Fix, double Score);
}
