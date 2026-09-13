using System.Text.RegularExpressions;

namespace EatsVoiceCompanion.Core.Speech;

public sealed record VoiceTranscriptRecoveryResult(
    string RecoveredTranscript,
    string Callsign,
    string InstructionPhrase,
    bool StarWasCorrected);

public sealed record VoiceTranscriptHypothesis(
    string RecoveredTranscript,
    string Callsign,
    string InstructionPhrase,
    double Score,
    bool StarWasCorrected);

public static partial class VoiceTranscriptRecovery
{
    private const double MinimumInstructionSimilarity = 0.62;
    private const double MinimumAirlineSimilarity = 0.30;
    private const double MinimumCallsignScore = 0.60;
    private const double MinimumWinnerMargin = 0.08;
    private const double MinimumStarSimilarity = 0.55;
    private const double MinimumHypothesisInstructionSimilarity = 0.42;

    private static readonly string[] InstructionPhrases =
    [
        "climb and maintain flight level",
        "descend and maintain flight level",
        "descend via except maintain",
        "descend at pilots discretion maintain flight level",
        "descend at pilots discretion to flight level",
        "descend at pilots discretion maintain",
        "descend at pilots discretion to",
        "expedite through flight level",
        "expedite to flight level",
        "expedite through",
        "expedite to",
        "report leaving flight level",
        "report reaching flight level",
        "report leaving",
        "report reaching",
        "say altitude",
        "altimeter",
        "say normal speed and mach",
        "say normal speed or mach",
        "say normal speed",
        "say indicated speed",
        "say airspeed",
        "say mach number",
        "say mach",
        "resume normal speed",
        "intercept the final approach course",
        "intercept final approach course",
        "cleared for the approach",
        "cleared for approach",
        "cleared approach",
        "fly present heading",
        "say approach request",
        "reduce speed to final approach speed",
        "reduce to final approach speed",
        "contact frequency",
        "remain this frequency",
        "say again",
        "stand by",
        "standby",
        "stop altitude squawk",
        "squawk altitude",
        "squawk normal",
        "squawk standby",
        "squawk vfr",
        "squawk ident",
        "ident",
        "squawk",
        "contact",
        "expect approach",
        "expect",
        "expedite",
        "comply with published speed restrictions at",
        "comply with speed restrictions at",
        "comply with published speeds at",
        "resume published speed at",
        "cross",
        "turn left heading",
        "turn right heading",
        "fly heading",
        "climb and maintain",
        "descend and maintain",
        "maintain speed",
        "maintain mach",
        "proceed direct to",
        "proceed direct",
        "cleared direct to",
        "cleared direct",
        "clear direct to",
        "clear direct",
        "descend via",
        "welcome",
        "roger"
    ];

    private static readonly IReadOnlySet<string> InstructionAnchorWords =
        new HashSet<string>(StringComparer.OrdinalIgnoreCase)
        {
            "altimeter", "altitude", "approach", "climb", "comply",
            "contact", "cross", "descend", "direct", "expedite",
            "fly", "heading", "ident", "intercept", "mach", "remain",
            "report", "resume", "roger", "say", "speed", "squawk",
            "stand", "standby", "stop", "turn", "welcome"
        };

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

        string normalized = SeparateLeadingAirlineNumber(
            NormalizeObservedPhrases(NormalizeWords(transcript)));
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

        if (instruction.Phrase is "contact" or "contact frequency" &&
            !remainder
                .Split(' ', StringSplitOptions.RemoveEmptyEntries)
                .Any(AviationFrequencyParser.IsDigitToken))
        {
            return null;
        }

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

        string normalized = SeparateLeadingAirlineNumber(
            NormalizeObservedPhrases(NormalizeWords(transcript)));
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

    public static IReadOnlyList<VoiceTranscriptHypothesis>
        GenerateHypotheses(
            string transcript,
            IEnumerable<string> activeCallsigns,
            IReadOnlyDictionary<string, string> airlineAliases,
            IReadOnlyDictionary<string, string> activeStars,
            string? controllerPosition = null,
            int maximumHypotheses = 12)
    {
        if (string.IsNullOrWhiteSpace(transcript) || maximumHypotheses < 1)
        {
            return Array.Empty<VoiceTranscriptHypothesis>();
        }

        ArgumentNullException.ThrowIfNull(activeCallsigns);
        ArgumentNullException.ThrowIfNull(airlineAliases);
        ArgumentNullException.ThrowIfNull(activeStars);

        string[] active = activeCallsigns
            .Where(value => !string.IsNullOrWhiteSpace(value))
            .Select(value => value.Trim().ToUpperInvariant())
            .Distinct(StringComparer.OrdinalIgnoreCase)
            .ToArray();
        string normalized = SeparateLeadingAirlineNumber(
            NormalizeObservedPhrases(NormalizeWords(transcript)));
        string[] tokens = normalized.Split(
            ' ',
            StringSplitOptions.RemoveEmptyEntries);
        List<VoiceTranscriptHypothesis> hypotheses = new();

        foreach (InstructionMatch instruction in FindInstructionMatches(
                     tokens,
                     MinimumHypothesisInstructionSimilarity)
                 .Take(maximumHypotheses * 2))
        {
            if (instruction.StartIndex == 0)
            {
                continue;
            }

            string[] callsignAndPosition = tokens[..instruction.StartIndex];
            CallsignMatch? callsign = FindCallsign(
                callsignAndPosition,
                active,
                airlineAliases,
                out _,
                allowHypothesisMatch: true);

            if (callsign is null && active.Length > 0)
            {
                string forcedCallsign = SelectBestActiveCallsign(
                    string.Join(' ', callsignAndPosition),
                    active,
                    airlineAliases)!;
                callsign = new CallsignMatch(
                    forcedCallsign,
                    Score: active.Length == 1 ? 0.50 : 0.30,
                    ExactNumber: false);
            }

            if (callsign is null)
            {
                continue;
            }

            string remainder = string.Join(
                ' ',
                tokens[(instruction.StartIndex +
                    instruction.TokenCount)..]);
            remainder = CorrectFlightLevelRemainder(
                instruction.Phrase,
                remainder);
            remainder = ExpandBareAltimeter(
                instruction.Phrase,
                remainder,
                controllerPosition);
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

            string recovered = instruction.Phrase == "altimeter"
                ? $"{callsign.Callsign} the " +
                  $"{GetAltimeterFacility(controllerPosition)} altimeter"
                : $"{callsign.Callsign} {instruction.Phrase}";

            if (remainder.Length > 0)
            {
                recovered += " " + remainder;
            }

            hypotheses.Add(new VoiceTranscriptHypothesis(
                recovered,
                callsign.Callsign,
                instruction.Phrase,
                instruction.Similarity * 0.70 + callsign.Score * 0.30,
                starWasCorrected));
        }

        return hypotheses
            .GroupBy(
                hypothesis => hypothesis.RecoveredTranscript,
                StringComparer.OrdinalIgnoreCase)
            .Select(group => group.MaxBy(item => item.Score)!)
            .OrderByDescending(hypothesis => hypothesis.Score)
            .Take(maximumHypotheses)
            .ToArray();
    }

    public static string? SelectBestActiveCallsign(
        string transcript,
        IEnumerable<string> activeCallsigns,
        IReadOnlyDictionary<string, string> airlineAliases)
    {
        string[] active = activeCallsigns
            .Where(value => !string.IsNullOrWhiteSpace(value))
            .Select(value => value.Trim().ToUpperInvariant())
            .Distinct(StringComparer.OrdinalIgnoreCase)
            .ToArray();

        if (active.Length == 0)
        {
            return null;
        }

        string normalized = SeparateLeadingAirlineNumber(
            NormalizeObservedPhrases(NormalizeWords(transcript)));
        string[] tokens = normalized.Split(
            ' ',
            StringSplitOptions.RemoveEmptyEntries);
        string[] observedFlightNumbers = Regex.Matches(
                normalized,
                @"\b\d{1,4}\b",
                RegexOptions.CultureInvariant)
            .Select(match => match.Value.TrimStart('0'))
            .Select(value => value.Length == 0 ? "0" : value)
            .Distinct(StringComparer.Ordinal)
            .ToArray();
        string[] exactNumberMatches = active
            .Where(callsign => !callsign.StartsWith('N'))
            .Where(callsign =>
            {
                string digits = new(
                    callsign.Where(char.IsDigit).ToArray());
                string normalizedDigits = digits.TrimStart('0');
                normalizedDigits = normalizedDigits.Length == 0
                    ? "0"
                    : normalizedDigits;
                return observedFlightNumbers.Contains(
                    normalizedDigits,
                    StringComparer.Ordinal);
            })
            .ToArray();

        if (exactNumberMatches.Length == 1)
        {
            return exactNumberMatches[0];
        }

        CallsignMatch? direct = FindCallsign(
            tokens,
            active,
            airlineAliases,
            out _,
            allowHypothesisMatch: true);

        if (direct is not null)
        {
            return direct.Callsign;
        }

        string observedDigits = new(
            normalized.Where(char.IsDigit).ToArray());
        bool soundsLikeNNumber = tokens.FirstOrDefault() is "november" or "n";

        return active
            .Select(callsign => new
            {
                Callsign = callsign,
                Score = FallbackCallsignScore(
                    callsign,
                    observedDigits,
                    soundsLikeNNumber,
                    normalized,
                    airlineAliases)
            })
            .OrderByDescending(item => item.Score)
            .ThenBy(item => item.Callsign, StringComparer.Ordinal)
            .First()
            .Callsign;
    }

    private static InstructionMatch? FindInstruction(string[] tokens)
    {
        return FindInstructionMatches(tokens, MinimumInstructionSimilarity)
            .FirstOrDefault();
    }

    private static IReadOnlyList<InstructionMatch> FindInstructionMatches(
        string[] tokens,
        double minimumSimilarity)
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

                    if (similarity >= minimumSimilarity &&
                        HasInstructionAnchor(candidate, phrase))
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
            .GroupBy(match => new
            {
                match.StartIndex,
                match.Phrase
            })
            .Select(group => group
                .OrderByDescending(match => match.Similarity)
                .ThenBy(match => Math.Abs(
                    match.TokenCount -
                    (match.Phrase.Count(character => character == ' ') + 1)))
                .First())
            .OrderByDescending(match => match.Similarity)
            .ThenBy(match => match.StartIndex)
            .ThenByDescending(match => match.Phrase.Length)
            .ToArray();
    }

    private static bool HasInstructionAnchor(
        string candidate,
        string phrase)
    {
        string[] candidateWords = candidate
            .Split(' ', StringSplitOptions.RemoveEmptyEntries)
            .Where(word => word.Length >= 3)
            .ToArray();
        string[] phraseAnchors = phrase
            .Split(' ', StringSplitOptions.RemoveEmptyEntries)
            .Where(InstructionAnchorWords.Contains)
            .ToArray();

        return phraseAnchors.Any(anchor =>
            candidateWords.Any(word => Similarity(word, anchor) >= 0.72));
    }

    private static CallsignMatch? FindCallsign(
        string[] tokens,
        IEnumerable<string> activeCallsigns,
        IReadOnlyDictionary<string, string> airlineAliases,
        out IReadOnlyList<string> candidates,
        bool allowHypothesisMatch = false)
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

        AddNNumberMatches(tokens, activeCallsigns, matches);

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
                        (allowHypothesisMatch
                            ? numberSimilarity >= 0.50 &&
                              airlineSimilarity >= 0.45
                            : numberSimilarity >= 0.74 &&
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

        double minimumScore = allowHypothesisMatch
            ? 0.50
            : MinimumCallsignScore;

        if (ranked.Length == 0 || ranked[0].Score < minimumScore)
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

    private static void AddNNumberMatches(
        string[] tokens,
        IEnumerable<string> activeCallsigns,
        ICollection<CallsignMatch> matches)
    {
        for (int end = 1; end <= Math.Min(tokens.Length, 7); end++)
        {
            if (!NNumberCallsignParser.TryParse(
                    string.Join(' ', tokens[..end]),
                    out string spokenRegistration))
            {
                continue;
            }

            bool mayBeAbbreviated = spokenRegistration.Length == 4;
            string suffix = spokenRegistration[1..];

            foreach (string rawCallsign in activeCallsigns)
            {
                string active = rawCallsign.Trim().ToUpperInvariant();

                if (!NNumberCallsignParser.IsValid(active))
                {
                    continue;
                }

                bool exact = string.Equals(
                    active,
                    spokenRegistration,
                    StringComparison.OrdinalIgnoreCase);
                bool suffixMatch = mayBeAbbreviated &&
                    active.EndsWith(
                        suffix,
                        StringComparison.OrdinalIgnoreCase);

                if (exact || suffixMatch)
                {
                    matches.Add(new CallsignMatch(
                        active,
                        Score: 1.0,
                        ExactNumber: true));
                }
            }
        }
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

        string rawSpokenStar = match.Groups["star"].Value;
        string spokenStar = NormalizeLooseStar(rawSpokenStar) ??
            Compact(rawSpokenStar);

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

        if (match.Success)
        {
            return "flight level " + match.Groups["value"].Value;
        }

        int connectorIndex = new[]
            {
                remainder.IndexOf(" then ", StringComparison.Ordinal),
                remainder.IndexOf(" and ", StringComparison.Ordinal)
            }
            .Where(index => index >= 0)
            .DefaultIfEmpty(remainder.Length)
            .Min();
        string altitudeCandidate = remainder[..connectorIndex].Trim();

        try
        {
            int possibleFlightLevel =
                AviationNumberParser.Parse(altitudeCandidate);

            if (possibleFlightLevel is >= 180 and <= 600)
            {
                return "flight level " + altitudeCandidate +
                       remainder[connectorIndex..];
            }
        }
        catch (ArgumentException)
        {
            // Preserve the original value for the normal parser to assess.
        }

        return remainder;
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

        normalized = Regex.Replace(normalized, @"\s+", " ").Trim();

        return Regex.Replace(
            normalized,
            @"\bpilot s discretion\b",
            "pilots discretion");
    }

    private static string NormalizeObservedPhrases(string value)
    {
        string normalized = Regex.Replace(
            value,
            @"\b(?:climate maintain|climate maintainer|" +
            @"climbing to maintain|climb in and maintain)\b",
            "climb and maintain");

        normalized = Regex.Replace(
            normalized,
            @"\bsquak\b",
            "squawk");

        normalized = Regex.Replace(
            normalized,
            @"\baltitood\b",
            "altitude");

        normalized = Regex.Replace(
            normalized,
            @"\bturn loft\b",
            "turn left");

        normalized = Regex.Replace(
            normalized,
            @"\bdescend v\b",
            "descend via");

        normalized = Regex.Replace(
            normalized,
            @"\bdescend via of the\b",
            "descend via the");

        normalized = Regex.Replace(
            normalized,
            @"\b(?:foot|put|flute) level\b",
            "flight level");

        normalized = Regex.Replace(
            normalized,
            @"\bfl\s*(?<value>\d{2,3})\b",
            "flight level ${value}");

        normalized = Regex.Replace(
            normalized,
            @"\b(?<first>\d)99er\b",
            "${first} niner niner");

        normalized = Regex.Replace(
            normalized,
            @"\b(?<first>\d)9er\b",
            "${first} niner");

        const string aviationDigit =
            @"(?:\d+|zero|oh|one|two|three|tree|four|fower|" +
            @"five|fife|six|seven|eight|nine|niner)";

        for (int pass = 0; pass < 2; pass++)
        {
            normalized = Regex.Replace(
                normalized,
                $@"\b(?<digit>{aviationDigit})\s+or\s+" +
                $@"(?={aviationDigit}\b)",
                "${digit} ");
        }

        normalized = Regex.Replace(
            normalized,
            @"(?<!proceed )(?<!cleared )(?<!clear )\bdirect\b",
            "proceed direct");

        return Regex.Replace(
            normalized,
            @"\b(?:to send|desun) via\b",
            "descend via");
    }

    private static string SeparateLeadingAirlineNumber(string value)
    {
        return Regex.Replace(
            value,
            @"^(?<airline>[a-z]{2,})(?<number>\d{1,4})(?=\s|$)",
            "${airline} ${number}");
    }

    private static string GetAltimeterFacility(string? controllerPosition)
    {
        string normalized = NormalizeWords(controllerPosition ?? string.Empty);
        string[] words = normalized.Split(
            ' ',
            StringSplitOptions.RemoveEmptyEntries);

        return words.FirstOrDefault(word =>
                   word is not ("center" or "approach" or "departure")) ??
               "local";
    }

    private static string ExpandBareAltimeter(
        string instructionPhrase,
        string remainder,
        string? controllerPosition)
    {
        if (instructionPhrase == "altimeter")
        {
            return remainder;
        }

        const string marker = " altimeter ";
        int index = remainder.IndexOf(
            marker,
            StringComparison.OrdinalIgnoreCase);

        if (index < 0)
        {
            return remainder;
        }

        string facility = GetAltimeterFacility(controllerPosition);
        string canonicalFacility = $" the {facility} altimeter ";

        if (remainder.Contains(
                canonicalFacility,
                StringComparison.OrdinalIgnoreCase))
        {
            return remainder;
        }

        string existingFacility = $" {facility} altimeter ";
        int facilityIndex = remainder.IndexOf(
            existingFacility,
            StringComparison.OrdinalIgnoreCase);

        if (facilityIndex >= 0)
        {
            return remainder[..facilityIndex] +
                   canonicalFacility +
                   remainder[(facilityIndex + existingFacility.Length)..];
        }

        return remainder[..index] +
               $" the {facility} altimeter " +
               remainder[(index + marker.Length)..];
    }

    private static double FallbackCallsignScore(
        string callsign,
        string observedDigits,
        bool soundsLikeNNumber,
        string normalizedTranscript,
        IReadOnlyDictionary<string, string> airlineAliases)
    {
        string callsignDigits = new(callsign.Where(char.IsDigit).ToArray());
        double numberScore = observedDigits.Length == 0
            ? 0
            : Similarity(observedDigits, callsignDigits);
        double typeScore = soundsLikeNNumber == callsign.StartsWith('N')
            ? 0.25
            : 0;
        double airlineScore = airlineAliases
            .Where(pair => callsign.StartsWith(
                pair.Value,
                StringComparison.OrdinalIgnoreCase))
            .Select(pair => Similarity(
                normalizedTranscript.Split(' ')[0],
                NormalizeWords(pair.Key).Split(' ')[0]))
            .DefaultIfEmpty(0)
            .Max();

        return numberScore * 0.55 + airlineScore * 0.30 + typeScore;
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
