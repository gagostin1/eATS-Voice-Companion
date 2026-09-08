using System.Text.RegularExpressions;

namespace EatsVoiceCompanion.Core.Speech;

public sealed record VoiceTranscriptRecoveryResult(
    string RecoveredTranscript,
    string Callsign,
    string InstructionPhrase,
    bool StarWasCorrected);

public static partial class VoiceTranscriptRecovery
{
    private const double MinimumInstructionSimilarity = 0.62;
    private const double MinimumAirlineSimilarity = 0.30;
    private const double MinimumCallsignScore = 0.60;
    private const double MinimumWinnerMargin = 0.08;
    private const double MinimumStarSimilarity = 0.55;

    private static readonly string[] InstructionPhrases =
    [
        "climb and maintain flight level",
        "descend and maintain flight level",
        "descend via except maintain",
        "cross",
        "turn left heading",
        "turn right heading",
        "fly heading",
        "climb and maintain",
        "descend and maintain",
        "maintain speed",
        "proceed direct to",
        "proceed direct",
        "descend via",
        "welcome",
        "roger"
    ];

    public static VoiceTranscriptRecoveryResult? TryRecover(
        string transcript,
        IEnumerable<string> activeCallsigns,
        IReadOnlyDictionary<string, string> airlineAliases,
        IReadOnlyDictionary<string, string> activeStars)
    {
        if (string.IsNullOrWhiteSpace(transcript))
        {
            return null;
        }

        ArgumentNullException.ThrowIfNull(activeCallsigns);
        ArgumentNullException.ThrowIfNull(airlineAliases);
        ArgumentNullException.ThrowIfNull(activeStars);

        string normalized = NormalizeObservedPhrases(
            NormalizeWords(transcript));
        string[] tokens = normalized.Split(
            ' ',
            StringSplitOptions.RemoveEmptyEntries);
        InstructionMatch? instruction = FindInstruction(tokens);

        if (instruction is null || instruction.StartIndex == 0)
        {
            return null;
        }

        string[] callsignAndPosition = tokens[..instruction.StartIndex];
        CallsignMatch? callsign = FindCallsign(
            callsignAndPosition,
            activeCallsigns,
            airlineAliases,
            out _);

        if (callsign is null)
        {
            return null;
        }

        string remainder = string.Join(
            ' ',
            tokens[(instruction.StartIndex + instruction.TokenCount)..]);
        remainder = CorrectFlightLevelRemainder(
            instruction.Phrase,
            remainder);
        bool starWasCorrected = false;

        if (instruction.Phrase == "descend via" &&
            activeStars.TryGetValue(
                callsign.Callsign,
                out string? assignedStar))
        {
            (remainder, starWasCorrected) = CorrectNamedStar(
                remainder,
                assignedStar);
        }

        string recovered = $"{callsign.Callsign} {instruction.Phrase}";

        if (remainder.Length > 0)
        {
            recovered += " " + remainder;
        }

        return new VoiceTranscriptRecoveryResult(
            recovered,
            callsign.Callsign,
            instruction.Phrase,
            starWasCorrected);
    }

    public static IReadOnlyList<string> SuggestCallsigns(
        string transcript,
        IEnumerable<string> activeCallsigns,
        IReadOnlyDictionary<string, string> airlineAliases,
        int maximumSuggestions = 3)
    {
        if (string.IsNullOrWhiteSpace(transcript) ||
            maximumSuggestions < 1)
        {
            return Array.Empty<string>();
        }

        ArgumentNullException.ThrowIfNull(activeCallsigns);
        ArgumentNullException.ThrowIfNull(airlineAliases);

        string normalized = NormalizeObservedPhrases(
            NormalizeWords(transcript));
        string[] tokens = normalized.Split(
            ' ',
            StringSplitOptions.RemoveEmptyEntries);
        InstructionMatch? instruction = FindInstruction(tokens);

        if (instruction is null || instruction.StartIndex == 0)
        {
            return Array.Empty<string>();
        }

        _ = FindCallsign(
            tokens[..instruction.StartIndex],
            activeCallsigns,
            airlineAliases,
            out IReadOnlyList<string> candidates);

        return candidates.Take(maximumSuggestions).ToArray();
    }

    private static InstructionMatch? FindInstruction(string[] tokens)
    {
        List<InstructionMatch> matches = new();

        foreach (string phrase in InstructionPhrases)
        {
            int phraseWordCount = phrase.Count(character => character == ' ') + 1;
            int minimumWords = Math.Max(1, phraseWordCount - 1);
            int maximumWords = phraseWordCount + 1;

            for (int start = 1; start < tokens.Length; start++)
            {
                for (int count = minimumWords;
                     count <= maximumWords && start + count <= tokens.Length;
                     count++)
                {
                    string candidate = string.Join(
                        ' ',
                        tokens.AsSpan(start, count).ToArray());
                    double similarity = Similarity(candidate, phrase);

                    if (similarity >= MinimumInstructionSimilarity)
                    {
                        matches.Add(new InstructionMatch(
                            start,
                            count,
                            phrase,
                            similarity));
                    }
                }
            }
        }

        return matches
            .OrderByDescending(match => match.Similarity)
            .ThenBy(match => match.StartIndex)
            .ThenByDescending(match => match.Phrase.Length)
            .FirstOrDefault();
    }

    private static CallsignMatch? FindCallsign(
        string[] tokens,
        IEnumerable<string> activeCallsigns,
        IReadOnlyDictionary<string, string> airlineAliases,
        out IReadOnlyList<string> candidates)
    {
        Dictionary<string, string[]> aliasesByDesignator = airlineAliases
            .Where(pair =>
                !string.IsNullOrWhiteSpace(pair.Key) &&
                !string.IsNullOrWhiteSpace(pair.Value))
            .GroupBy(
                pair => pair.Value.Trim().ToUpperInvariant(),
                StringComparer.OrdinalIgnoreCase)
            .ToDictionary(
                group => group.Key,
                group => group
                    .Select(pair => NormalizeWords(pair.Key))
                    .Append(group.Key.ToLowerInvariant())
                    .Distinct(StringComparer.OrdinalIgnoreCase)
                    .ToArray(),
                StringComparer.OrdinalIgnoreCase);

        List<CallsignMatch> matches = new();

        foreach (string rawCallsign in activeCallsigns)
        {
            string callsign = rawCallsign.Trim().ToUpperInvariant();
            string? designator = aliasesByDesignator.Keys
                .OrderByDescending(value => value.Length)
                .FirstOrDefault(value => callsign.StartsWith(
                    value,
                    StringComparison.OrdinalIgnoreCase));

            if (designator is null)
            {
                continue;
            }

            string expectedNumber = callsign[designator.Length..];

            if (expectedNumber.Length is < 1 or > 4 ||
                !expectedNumber.All(char.IsDigit))
            {
                continue;
            }

            for (int split = 1; split < tokens.Length; split++)
            {
                string spokenAirline = string.Join(' ', tokens[..split]);
                double airlineSimilarity = aliasesByDesignator[designator]
                    .Max(alias => Similarity(spokenAirline, alias));

                if (airlineSimilarity < MinimumAirlineSimilarity)
                {
                    continue;
                }

                int maximumEnd = Math.Min(tokens.Length, split + 5);

                for (int end = split + 1; end <= maximumEnd; end++)
                {
                    if (!TryParseFlightNumber(
                            string.Join(' ', tokens[split..end]),
                            out string? spokenNumber))
                    {
                        continue;
                    }

                    double numberSimilarity = Similarity(
                        spokenNumber!,
                        expectedNumber);
                    bool exactNumber = spokenNumber == expectedNumber;
                    double score = airlineSimilarity * 0.55 +
                                   numberSimilarity * 0.45;

                    if (exactNumber ||
                        (numberSimilarity >= 0.74 &&
                         airlineSimilarity >= 0.70))
                    {
                        matches.Add(new CallsignMatch(
                            callsign,
                            score,
                            exactNumber));
                    }
                }
            }
        }

        if (matches.Any(match => match.ExactNumber))
        {
            matches = matches
                .Where(match => match.ExactNumber)
                .ToList();
        }

        CallsignMatch[] ranked = matches
            .GroupBy(match => match.Callsign)
            .Select(group => group.MaxBy(match => match.Score)!)
            .OrderByDescending(match => match.Score)
            .ToArray();

        candidates = ranked
            .Select(match => match.Callsign)
            .ToArray();

        if (ranked.Length == 0 || ranked[0].Score < MinimumCallsignScore)
        {
            return null;
        }

        if (ranked.Length > 1 &&
            ranked[0].Score - ranked[1].Score < MinimumWinnerMargin)
        {
            return null;
        }

        return ranked[0];
    }

    private static (string Remainder, bool Corrected) CorrectNamedStar(
        string remainder,
        string assignedStar)
    {
        Match match = NamedStarRemainderPattern().Match(remainder);

        if (!match.Success)
        {
            return (remainder, false);
        }

        string? spokenStar = NormalizeLooseStar(
            match.Groups["star"].Value);

        if (spokenStar is null)
        {
            return (remainder, false);
        }

        if (string.Equals(
                spokenStar,
                assignedStar,
                StringComparison.OrdinalIgnoreCase))
        {
            return (remainder, false);
        }

        if (Similarity(spokenStar, assignedStar) < MinimumStarSimilarity)
        {
            return (remainder, false);
        }

        string suffix = match.Groups["suffix"].Value.Trim();
        string corrected =
            $"the {StarNameNormalizer.ToPromptPhrase(assignedStar)} arrival";

        if (suffix.Length > 0)
        {
            corrected += " " + suffix;
        }

        return (corrected, true);
    }

    private static string CorrectFlightLevelRemainder(
        string instructionPhrase,
        string remainder)
    {
        if (instructionPhrase is not "climb and maintain" and
            not "descend and maintain")
        {
            return remainder;
        }

        Match match = MangledFlightLevelPattern().Match(remainder);

        return match.Success
            ? "flight level " + match.Groups["value"].Value
            : remainder;
    }

    private static string? NormalizeLooseStar(string spokenStar)
    {
        try
        {
            return StarNameNormalizer.Normalize(spokenStar);
        }
        catch (ArgumentException)
        {
            string[] words = NormalizeWords(spokenStar).Split(
                ' ',
                StringSplitOptions.RemoveEmptyEntries);

            if (words.Length is < 2 or > 4)
            {
                return null;
            }

            int number;

            try
            {
                number = AviationNumberParser.Parse(words[^1]);
            }
            catch (ArgumentException)
            {
                return null;
            }

            if (number is < 1 or > 9)
            {
                return null;
            }

            string name = new(
                words[..^1]
                    .SelectMany(word => word)
                    .Where(char.IsLetterOrDigit)
                    .Select(char.ToUpperInvariant)
                    .ToArray());

            return name.Length == 0
                ? null
                : name + number;
        }
    }

    private static bool TryParseFlightNumber(
        string value,
        out string? flightNumber)
    {
        try
        {
            flightNumber = FlightNumberParser.Parse(value);
            return true;
        }
        catch (ArgumentException)
        {
            flightNumber = null;
            return false;
        }
    }

    private static double Similarity(string left, string right)
    {
        string normalizedLeft = Compact(left);
        string normalizedRight = Compact(right);

        if (normalizedLeft.Length == 0 || normalizedRight.Length == 0)
        {
            return 0;
        }

        int distance = LevenshteinDistance(
            normalizedLeft,
            normalizedRight);

        return 1.0 -
               (double)distance /
               Math.Max(normalizedLeft.Length, normalizedRight.Length);
    }

    private static int LevenshteinDistance(string left, string right)
    {
        int[] previous = Enumerable.Range(0, right.Length + 1).ToArray();
        int[] current = new int[right.Length + 1];

        for (int leftIndex = 1;
             leftIndex <= left.Length;
             leftIndex++)
        {
            current[0] = leftIndex;

            for (int rightIndex = 1;
                 rightIndex <= right.Length;
                 rightIndex++)
            {
                int substitution = previous[rightIndex - 1] +
                    (left[leftIndex - 1] == right[rightIndex - 1] ? 0 : 1);

                current[rightIndex] = Math.Min(
                    Math.Min(
                        current[rightIndex - 1] + 1,
                        previous[rightIndex] + 1),
                    substitution);
            }

            (previous, current) = (current, previous);
        }

        return previous[right.Length];
    }

    private static string Compact(string value)
    {
        return new string(
            NormalizeWords(value)
                .Where(char.IsLetterOrDigit)
                .ToArray());
    }

    private static string NormalizeWords(string value)
    {
        string normalized = Regex.Replace(
            value.Trim().ToLowerInvariant(),
            @"[^\p{L}\p{N}]+",
            " ");

        return Regex.Replace(normalized, @"\s+", " ").Trim();
    }

    private static string NormalizeObservedPhrases(string value)
    {
        string normalized = Regex.Replace(
            value,
            @"\b(?:climate maintain|climate maintainer|" +
            @"climbing to maintain)\b",
            "climb and maintain");

        return Regex.Replace(
            normalized,
            @"\b(?:to send|desun) via\b",
            "descend via");
    }

    [GeneratedRegex(
        "^(?:the )?(?<star>[a-z0-9]+(?: [a-z0-9]+){0,3}) " +
        "arrival(?: (?<suffix>.*))?$",
        RegexOptions.CultureInvariant)]
    private static partial Regex NamedStarRemainderPattern();

    [GeneratedRegex(
        "^(?:for|four) (?:available|a level|level) (?<value>.+)$",
        RegexOptions.CultureInvariant)]
    private static partial Regex MangledFlightLevelPattern();

    private sealed record InstructionMatch(
        int StartIndex,
        int TokenCount,
        string Phrase,
        double Similarity);

    private sealed record CallsignMatch(
        string Callsign,
        double Score,
        bool ExactNumber);
}
