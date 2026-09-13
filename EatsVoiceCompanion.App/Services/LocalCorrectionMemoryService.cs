using System.Text.RegularExpressions;
using EatsVoiceCompanion.Core.Speech;

namespace EatsVoiceCompanion.App.Services;

public sealed class LocalCorrectionMemoryService
{
    private const int MaximumPromptExamples = 4;
    private const int MaximumPromptLength = 1_200;
    private readonly object _syncRoot = new();
    private CorrectionHistoryEntry[] _reviewedCorrections = [];

    public void Update(IEnumerable<CorrectionHistoryEntry> entries)
    {
        ArgumentNullException.ThrowIfNull(entries);

        CorrectionHistoryEntry[] reviewed = entries
            .Where(entry =>
                entry.ReviewStatus != CorrectionReviewStatus.Unreviewed &&
                entry.UseForLocalLearning &&
                !string.IsNullOrWhiteSpace(entry.ExpectedCommand))
            .OrderByDescending(entry => entry.RecordedAtUtc)
            .Take(100)
            .ToArray();

        lock (_syncRoot)
        {
            _reviewedCorrections = reviewed;
        }
    }

    public IReadOnlyList<string> GetLearnedFixMappings() =>
        BuildAliases(Snapshot())
            .Select(pair => $"{pair.Key} → {pair.Value}")
            .Distinct(StringComparer.OrdinalIgnoreCase)
            .OrderBy(value => value, StringComparer.OrdinalIgnoreCase)
            .ToArray();

    public RecognitionContextResult Enrich(
        RecognitionContextResult context)
    {
        ArgumentNullException.ThrowIfNull(context);

        CorrectionHistoryEntry[] corrections = Snapshot();
        HashSet<string> activeFixes = context.ActiveRouteFixes
            .SelectMany(pair => pair.Value)
            .ToHashSet(StringComparer.OrdinalIgnoreCase);
        string[] vocabulary = BuildAliases(corrections)
            .Where(pair => activeFixes.Contains(pair.Value))
            .Select(pair => $"{pair.Value} may be heard as {pair.Key}")
            .Distinct(StringComparer.OrdinalIgnoreCase)
            .Take(12)
            .ToArray();
        string[] examples = corrections
            .Where(entry =>
                entry.ReviewStatus == CorrectionReviewStatus.Corrected &&
                !string.IsNullOrWhiteSpace(entry.CorrectedTranscript))
            .Where(entry => IsRelevant(entry, activeFixes))
            .Select(entry => ExtractInstructionExample(
                entry.CorrectedTranscript!))
            .Where(value => value.Length > 0)
            .Distinct(StringComparer.OrdinalIgnoreCase)
            .Take(MaximumPromptExamples)
            .ToArray();
        List<string> additions = [];

        if (vocabulary.Length > 0)
        {
            additions.Add(
                "Locally reviewed fix pronunciations: " +
                string.Join(". ", vocabulary) + ".");
        }

        if (examples.Length > 0)
        {
            additions.Add(
                "Locally reviewed ATC examples: " +
                string.Join(". ", examples) + ".");
        }

        if (additions.Count == 0)
        {
            return context;
        }

        string adaptivePrompt = string.Join(' ', additions);

        if (adaptivePrompt.Length > MaximumPromptLength)
        {
            adaptivePrompt = adaptivePrompt[..MaximumPromptLength];
        }

        return context with
        {
            Prompt = $"{context.Prompt} {adaptivePrompt}".Trim()
        };
    }

    public string ApplyLearnedFixAliases(
        string transcript,
        IReadOnlySet<string> activeCallsigns,
        IReadOnlyDictionary<string, IReadOnlySet<string>> activeRouteFixes,
        IReadOnlyDictionary<string, string> airlineAliases)
    {
        if (string.IsNullOrWhiteSpace(transcript) ||
            activeCallsigns.Count == 0)
        {
            return transcript;
        }

        string? callsign = VoiceTranscriptRecovery.SelectBestActiveCallsign(
            transcript,
            activeCallsigns,
            airlineAliases);

        if (callsign is null ||
            !activeRouteFixes.TryGetValue(
                callsign,
                out IReadOnlySet<string>? routeFixes))
        {
            return transcript;
        }

        string adapted = transcript;
        var applicable = BuildAliases(Snapshot())
            .Where(pair => routeFixes.Contains(pair.Value))
            .GroupBy(pair => pair.Key, StringComparer.OrdinalIgnoreCase)
            .Where(group => group
                .Select(pair => pair.Value)
                .Distinct(StringComparer.OrdinalIgnoreCase)
                .Count() == 1)
            .Select(group => group.First());

        foreach ((string observed, string canonical) in applicable)
        {
            string alias = Regex.Escape(observed).Replace("\\ ", @"\s+");
            string prefix =
                @"(?<prefix>\b(?:cross|(?:proceed|clear|cleared)\s+direct" +
                @"(?:\s+to)?|(?:comply\s+with|resume)\b[^,.]*?\bat)\s+)";
            adapted = Regex.Replace(
                adapted,
                prefix + alias + @"(?=\s+(?:at|then)\b|\s*[,\.]|$)",
                match => match.Groups["prefix"].Value + canonical,
                RegexOptions.IgnoreCase | RegexOptions.CultureInvariant);
        }

        return adapted;
    }

    private CorrectionHistoryEntry[] Snapshot()
    {
        lock (_syncRoot)
        {
            return [.. _reviewedCorrections];
        }
    }

    private static IReadOnlyList<KeyValuePair<string, string>> BuildAliases(
        IEnumerable<CorrectionHistoryEntry> corrections)
    {
        List<KeyValuePair<string, string>> aliases = [];

        foreach (CorrectionHistoryEntry entry in corrections)
        {
            string? canonical = ExtractExpectedFix(entry.ExpectedCommand!);
            string? observed = ExtractSpokenFix(
                entry.OriginalTranscript,
                entry.ExpectedCommand!);

            if (canonical is null || observed is null ||
                string.Equals(
                    Normalize(observed),
                    Normalize(canonical),
                    StringComparison.OrdinalIgnoreCase))
            {
                continue;
            }

            aliases.Add(new KeyValuePair<string, string>(
                observed.Trim(),
                canonical));
        }

        return aliases;
    }

    private static string? ExtractExpectedFix(string command)
    {
        foreach (string token in command.Split(
                     ' ',
                     StringSplitOptions.RemoveEmptyEntries))
        {
            if (token.StartsWith("..", StringComparison.Ordinal) &&
                token.Length > 2)
            {
                return token[2..].ToUpperInvariant();
            }

            if (token.StartsWith('X'))
            {
                int separator = token.IndexOf('@');

                if (separator > 1)
                {
                    return token[1..separator].ToUpperInvariant();
                }
            }

            if (token.StartsWith("CWS@", StringComparison.Ordinal) &&
                token.Length > 4)
            {
                return token[4..].ToUpperInvariant();
            }
        }

        return null;
    }

    private static string? ExtractSpokenFix(
        string transcript,
        string expectedCommand)
    {
        string pattern = expectedCommand.Split(' ').Any(token =>
                token.StartsWith('X'))
            ? @"\bcross\s+(?<fix>[\p{L}\p{N}-]+(?:\s+[\p{L}\p{N}-]+)?)\s+at\b"
            : expectedCommand.Contains("..", StringComparison.Ordinal)
                ? @"\b(?:proceed|clear|cleared)\s+direct(?:\s+to)?\s+" +
                  @"(?<fix>[\p{L}\p{N}-]+(?:\s+[\p{L}\p{N}-]+)?)" +
                  @"(?=\s+then\b|\s*[,\.]|$)"
                : @"\b(?:comply\s+with|resume)\b[^,.]*?\bat\s+" +
                  @"(?<fix>[\p{L}\p{N}-]+(?:\s+[\p{L}\p{N}-]+)?)" +
                  @"(?=\s*[,\.]|$)";
        Match match = Regex.Match(
            transcript,
            pattern,
            RegexOptions.IgnoreCase | RegexOptions.CultureInvariant);

        return match.Success ? match.Groups["fix"].Value : null;
    }

    private static bool IsRelevant(
        CorrectionHistoryEntry entry,
        IReadOnlySet<string> activeFixes)
    {
        string? fix = ExtractExpectedFix(entry.ExpectedCommand!);
        return fix is null || activeFixes.Contains(fix);
    }

    private static string ExtractInstructionExample(string transcript)
    {
        Match match = Regex.Match(
            transcript,
            @"\b(?:cross|turn|fly|climb|descend|maintain|proceed|clear|" +
            @"cleared|the\s+\w+\s+altimeter|squawk|contact|say|report)\b.*$",
            RegexOptions.IgnoreCase | RegexOptions.CultureInvariant);

        return match.Success ? match.Value.Trim().TrimEnd('.') : string.Empty;
    }

    private static string Normalize(string value) => new(
        value.ToLowerInvariant().Where(char.IsLetterOrDigit).ToArray());
}
