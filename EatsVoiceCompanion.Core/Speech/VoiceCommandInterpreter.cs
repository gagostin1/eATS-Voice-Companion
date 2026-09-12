using EatsVoiceCompanion.Core.Commands;

namespace EatsVoiceCompanion.Core.Speech;

public enum VoiceInterpretationKind
{
    Strict,
    Recovered,
    BestHypothesis
}

public sealed record VoiceCommandInterpretation(
    ParsedVoiceCommand Command,
    VoiceInterpretationKind Kind,
    string InterpretedTranscript,
    string? RecoveredInstructionPhrase = null,
    bool StarWasCorrected = false,
    bool RouteFixWasCorrected = false,
    double ConfidenceScore = 1.0,
    int ValidHypothesisCount = 1);

public sealed class VoiceInterpretationException : InvalidOperationException
{
    public VoiceInterpretationException(
        string message,
        IReadOnlyList<string> candidateCallsigns,
        Exception innerException)
        : base(message, innerException)
    {
        CandidateCallsigns = candidateCallsigns;
    }

    public IReadOnlyList<string> CandidateCallsigns { get; }
}

public sealed class VoiceCommandInterpreter
{
    private readonly IReadOnlyDictionary<string, string> _airlineAliases;
    private readonly VoiceCommandParser _parser;

    public VoiceCommandInterpreter(
        IReadOnlyDictionary<string, string> airlineAliases)
    {
        ArgumentNullException.ThrowIfNull(airlineAliases);
        _airlineAliases = airlineAliases;
        _parser = new VoiceCommandParser(airlineAliases);
    }

    public VoiceCommandInterpretation Interpret(
        string transcript,
        string? controllerPosition,
        IReadOnlySet<string> activeCallsigns,
        IReadOnlyDictionary<string, string> activeStars,
        IReadOnlyDictionary<string, IReadOnlySet<string>>? activeRouteFixes = null)
    {
        ArgumentNullException.ThrowIfNull(activeCallsigns);
        ArgumentNullException.ThrowIfNull(activeStars);

        if (string.IsNullOrWhiteSpace(transcript))
        {
            throw new ArgumentException(
                "No speech was recognized.",
                nameof(transcript));
        }

        try
        {
            return InterpretCore(
                transcript,
                controllerPosition,
                activeCallsigns,
                activeStars,
                activeRouteFixes);
        }
        catch (Exception exception)
            when (activeCallsigns.Count > 0 &&
                  exception is ArgumentException or InvalidOperationException)
        {
            return CreateForcedFallback(transcript, activeCallsigns);
        }
    }

    private VoiceCommandInterpretation InterpretCore(
        string transcript,
        string? controllerPosition,
        IReadOnlySet<string> activeCallsigns,
        IReadOnlyDictionary<string, string> activeStars,
        IReadOnlyDictionary<string, IReadOnlySet<string>>? activeRouteFixes)
    {
        ArgumentNullException.ThrowIfNull(activeCallsigns);
        ArgumentNullException.ThrowIfNull(activeStars);

        ParsedVoiceCommand parsed;

        try
        {
            parsed = _parser.Parse(transcript, controllerPosition);
            _ = EatsTransmissionValidator.Validate(parsed.ToEatsCommand());
        }
        catch (Exception exception)
            when (exception is ArgumentException or
                InvalidOperationException)
        {
            return InterpretBestHypothesisOrThrow(
                transcript,
                controllerPosition,
                activeCallsigns,
                activeStars,
                activeRouteFixes,
                exception);
        }

        if (IsUnmatchedAbbreviatedNNumber(parsed, activeCallsigns))
        {
            VoiceTranscriptRecoveryResult? recovery =
                VoiceTranscriptRecovery.TryRecover(
                    transcript,
                    activeCallsigns,
                    _airlineAliases,
                    activeStars);

            if (recovery is not null &&
                !string.Equals(
                    recovery.Callsign,
                    parsed.Callsign,
                    StringComparison.OrdinalIgnoreCase))
            {
                parsed = _parser.Parse(recovery.RecoveredTranscript);
                return ApplyRouteFixContext(
                    Recovered(parsed, recovery),
                    activeRouteFixes);
            }

            IReadOnlyList<string> candidates =
                VoiceTranscriptRecovery.SuggestCallsigns(
                    transcript,
                    activeCallsigns,
                    _airlineAliases);

            if (candidates.Count > 1)
            {
                ArgumentException cause = new(
                    "The abbreviated N-number matches more than one " +
                    "active aircraft.",
                    nameof(transcript));

                return InterpretBestHypothesisOrThrow(
                    transcript,
                    controllerPosition,
                    activeCallsigns,
                    activeStars,
                    activeRouteFixes,
                    cause);
            }
        }

        if (activeCallsigns.Count > 0 &&
            !activeCallsigns.Contains(parsed.Callsign))
        {
            ArgumentException cause = new(
                $"The recognized callsign '{parsed.Callsign}' is not active.",
                nameof(transcript));

            return InterpretBestHypothesisOrThrow(
                transcript,
                controllerPosition,
                activeCallsigns,
                activeStars,
                activeRouteFixes,
                cause);
        }

        if (HasFuzzyNamedStarMismatch(parsed, activeStars))
        {
            VoiceTranscriptRecoveryResult? recovery =
                VoiceTranscriptRecovery.TryRecover(
                    transcript,
                    activeCallsigns,
                    _airlineAliases,
                    activeStars);

            if (recovery?.StarWasCorrected == true)
            {
                parsed = _parser.Parse(recovery.RecoveredTranscript);
                return ApplyRouteFixContext(
                    Recovered(parsed, recovery),
                    activeRouteFixes);
            }
        }

        return ApplyRouteFixContext(
            new VoiceCommandInterpretation(
                parsed,
                VoiceInterpretationKind.Strict,
                transcript),
            activeRouteFixes);
    }

    private VoiceCommandInterpretation CreateForcedFallback(
        string transcript,
        IReadOnlySet<string> activeCallsigns)
    {
        string fallbackCallsign =
            VoiceTranscriptRecovery.SelectBestActiveCallsign(
                transcript,
                activeCallsigns,
                _airlineAliases)!;
        ParsedVoiceCommand fallback = _parser.Parse(
            $"{fallbackCallsign} roger");

        return new VoiceCommandInterpretation(
            fallback,
            VoiceInterpretationKind.BestHypothesis,
            $"{fallbackCallsign} roger",
            "roger",
            ConfidenceScore: 0,
            ValidHypothesisCount: 0);
    }

    private VoiceCommandInterpretation InterpretBestHypothesisOrThrow(
        string transcript,
        string? controllerPosition,
        IReadOnlySet<string> activeCallsigns,
        IReadOnlyDictionary<string, string> activeStars,
        IReadOnlyDictionary<string, IReadOnlySet<string>>? activeRouteFixes,
        Exception originalException)
    {
        IReadOnlyList<VoiceTranscriptHypothesis> hypotheses =
            VoiceTranscriptRecovery.GenerateHypotheses(
                transcript,
                activeCallsigns,
                _airlineAliases,
                activeStars,
                controllerPosition);
        VoiceTranscriptRecoveryResult? conservativeRecovery =
            VoiceTranscriptRecovery.TryRecover(
                transcript,
                activeCallsigns,
                _airlineAliases,
                activeStars);
        List<ScoredInterpretation> valid = new();

        foreach (VoiceTranscriptHypothesis hypothesis in hypotheses)
        {
            try
            {
                string candidateTranscript = hypothesis.RecoveredTranscript;
                ParsedVoiceCommand candidate;
                bool hypothesisFixWasCorrected = false;

                try
                {
                    candidate = _parser.Parse(candidateTranscript);
                }
                catch (Exception exception)
                    when (exception is ArgumentException or
                        InvalidOperationException)
                {
                    string withoutInvalidTail =
                        TruncateInvalidTrailingInstruction(
                            candidateTranscript);

                    if (!string.Equals(
                            withoutInvalidTail,
                            candidateTranscript,
                            StringComparison.OrdinalIgnoreCase))
                    {
                        candidateTranscript = withoutInvalidTail;
                        candidate = _parser.Parse(candidateTranscript);
                        goto CandidateParsed;
                    }

                    candidateTranscript = CorrectHypothesisFixText(
                        hypothesis,
                        activeRouteFixes);
                    hypothesisFixWasCorrected = !string.Equals(
                        candidateTranscript,
                        hypothesis.RecoveredTranscript,
                        StringComparison.OrdinalIgnoreCase);

                    if (!hypothesisFixWasCorrected)
                    {
                        throw;
                    }

                    candidate = _parser.Parse(candidateTranscript);
                }

            CandidateParsed:
                _ = EatsTransmissionValidator.Validate(
                    candidate.ToEatsCommand());
                bool isConservativeRecovery = conservativeRecovery is not null &&
                    string.Equals(
                        hypothesis.RecoveredTranscript,
                        conservativeRecovery.RecoveredTranscript,
                        StringComparison.OrdinalIgnoreCase) &&
                    string.Equals(
                        candidateTranscript,
                        hypothesis.RecoveredTranscript,
                        StringComparison.OrdinalIgnoreCase);
                VoiceCommandInterpretation interpretation =
                    ApplyRouteFixContext(
                        new VoiceCommandInterpretation(
                            candidate,
                            isConservativeRecovery
                                ? VoiceInterpretationKind.Recovered
                                : VoiceInterpretationKind.BestHypothesis,
                            candidateTranscript,
                            hypothesis.InstructionPhrase,
                            hypothesis.StarWasCorrected,
                            RouteFixWasCorrected:
                                hypothesisFixWasCorrected,
                            ConfidenceScore: hypothesis.Score),
                        activeRouteFixes);

                valid.Add(new ScoredInterpretation(
                    interpretation,
                    hypothesis.Score));
            }
            catch (Exception exception)
                when (exception is ArgumentException or
                    InvalidOperationException)
            {
                // Invalid hypotheses are discarded before ranking.
            }
        }

        ScoredInterpretation[] ranked = valid
            .GroupBy(item => item.Interpretation.Command.ToEatsCommand())
            .Select(group => group.MaxBy(item => item.Score)!)
            .OrderByDescending(item => item.Score)
            .ToArray();

        if (ranked.Length > 0)
        {
            return ranked[0].Interpretation with
            {
                ValidHypothesisCount = ranked.Length
            };
        }

        string? fallbackCallsign =
            VoiceTranscriptRecovery.SelectBestActiveCallsign(
                transcript,
                activeCallsigns,
                _airlineAliases);

        if (fallbackCallsign is not null)
        {
            ParsedVoiceCommand fallback = _parser.Parse(
                $"{fallbackCallsign} roger");

            return new VoiceCommandInterpretation(
                fallback,
                VoiceInterpretationKind.BestHypothesis,
                $"{fallbackCallsign} roger",
                "roger",
                ConfidenceScore: 0,
                ValidHypothesisCount: 0);
        }

        IReadOnlyList<string> candidates = Array.Empty<string>();
        string message = originalException.Message;

        if (candidates.Count > 0)
        {
            message += " Possible active callsigns: " +
                       string.Join(", ", candidates) + ".";
        }

        throw new VoiceInterpretationException(
            message,
            candidates,
            originalException);
    }

    private static string CorrectHypothesisFixText(
        VoiceTranscriptHypothesis hypothesis,
        IReadOnlyDictionary<string, IReadOnlySet<string>>? activeRouteFixes)
    {
        if (activeRouteFixes is null ||
            !activeRouteFixes.TryGetValue(
                hypothesis.Callsign,
                out IReadOnlySet<string>? routeFixes) ||
            routeFixes.Count == 0)
        {
            return hypothesis.RecoveredTranscript;
        }

        string prefix =
            $"{hypothesis.Callsign} {hypothesis.InstructionPhrase}";
        string remainder = hypothesis.RecoveredTranscript[prefix.Length..]
            .Trim();
        string observedFix;
        string suffix;

        if (hypothesis.InstructionPhrase.Contains(
                "direct",
                StringComparison.Ordinal))
        {
            int connector = remainder.IndexOf(
                " then ",
                StringComparison.Ordinal);
            observedFix = connector < 0
                ? remainder
                : remainder[..connector];
            suffix = connector < 0 ? string.Empty : remainder[connector..];
        }
        else if (hypothesis.InstructionPhrase == "cross")
        {
            int atIndex = remainder.IndexOf(" at ", StringComparison.Ordinal);

            if (atIndex < 0 ||
                remainder[..atIndex].Contains(
                    " miles ",
                    StringComparison.Ordinal))
            {
                return hypothesis.RecoveredTranscript;
            }

            observedFix = remainder[..atIndex];
            suffix = remainder[atIndex..];
        }
        else
        {
            return hypothesis.RecoveredTranscript;
        }

        string? matchedFix = RouteFixMatcher.FindUniqueMatch(
            observedFix,
            routeFixes);

        return matchedFix is null
            ? hypothesis.RecoveredTranscript
            : $"{prefix} {matchedFix}{suffix}";
    }

    private static string TruncateInvalidTrailingInstruction(
        string candidateTranscript)
    {
        int altimeter = candidateTranscript.IndexOf(
            " altimeter ",
            StringComparison.OrdinalIgnoreCase);

        return altimeter > 0
            ? candidateTranscript[..altimeter].Trim()
            : candidateTranscript;
    }

    private static VoiceCommandInterpretation ApplyRouteFixContext(
        VoiceCommandInterpretation interpretation,
        IReadOnlyDictionary<string, IReadOnlySet<string>>? activeRouteFixes)
    {
        ParsedVoiceCommand command = interpretation.Command;

        if (activeRouteFixes is null ||
            !activeRouteFixes.TryGetValue(
                command.Callsign,
                out IReadOnlySet<string>? routeFixes) ||
            routeFixes.Count == 0)
        {
            return interpretation;
        }

        bool corrected = false;
        List<ParsedVoiceInstruction> instructions = new();

        foreach (ParsedVoiceInstruction instruction in command.Instructions)
        {
            if (!UsesRouteFix(instruction.InstructionType) ||
                string.IsNullOrWhiteSpace(instruction.TextValue) ||
                routeFixes.Contains(instruction.TextValue))
            {
                instructions.Add(instruction);
                continue;
            }

            string? matchedFix = RouteFixMatcher.FindUniqueMatch(
                instruction.TextValue,
                routeFixes);

            if (matchedFix is null)
            {
                ArgumentException cause = new(
                    $"The recognized fix '{instruction.TextValue}' could " +
                    "not be matched uniquely to this aircraft's flight " +
                    "plan or arrival. Correct the transcript and interpret " +
                    "it again.",
                    "transcript");

                throw new VoiceInterpretationException(
                    cause.Message,
                    Array.Empty<string>(),
                    cause);
            }

            instructions.Add(instruction with { TextValue = matchedFix });
            corrected = true;
        }

        if (!corrected)
        {
            return interpretation;
        }

        return interpretation with
        {
            Command = new ParsedVoiceCommand(command.Callsign, instructions),
            Kind = interpretation.Kind == VoiceInterpretationKind.Strict
                ? VoiceInterpretationKind.Recovered
                : interpretation.Kind,
            RouteFixWasCorrected = true
        };
    }

    private static bool UsesRouteFix(VoiceInstructionType instructionType)
    {
        return instructionType is VoiceInstructionType.ProceedDirect or
            VoiceInstructionType.CrossAtAltitude or
            VoiceInstructionType.CrossAtAltitudeAndSpeed or
            VoiceInstructionType.CrossDistanceAtAltitude or
            VoiceInstructionType.ComplyWithPublishedSpeeds;
    }

    private static VoiceCommandInterpretation Recovered(
        ParsedVoiceCommand parsed,
        VoiceTranscriptRecoveryResult recovery)
    {
        return new VoiceCommandInterpretation(
            parsed,
            VoiceInterpretationKind.Recovered,
            recovery.RecoveredTranscript,
            recovery.InstructionPhrase,
            recovery.StarWasCorrected);
    }

    private static bool IsUnmatchedAbbreviatedNNumber(
        ParsedVoiceCommand parsed,
        IReadOnlySet<string> activeCallsigns)
    {
        return parsed.Callsign.Length == 4 &&
               NNumberCallsignParser.IsValid(parsed.Callsign) &&
               !activeCallsigns.Contains(parsed.Callsign);
    }

    private static bool HasFuzzyNamedStarMismatch(
        ParsedVoiceCommand parsed,
        IReadOnlyDictionary<string, string> activeStars)
    {
        string? spokenStar = parsed.Instructions
            .Where(instruction =>
                instruction.InstructionType is
                    VoiceInstructionType.DescendVia or
                    VoiceInstructionType.DescendViaExceptMaintain)
            .Select(instruction => instruction.TextValue)
            .FirstOrDefault(value => !string.IsNullOrWhiteSpace(value));

        return spokenStar is not null &&
               activeStars.TryGetValue(
                   parsed.Callsign,
                   out string? assignedStar) &&
               !string.Equals(
                   spokenStar,
                   assignedStar,
                   StringComparison.OrdinalIgnoreCase);
    }

    private sealed record ScoredInterpretation(
        VoiceCommandInterpretation Interpretation,
        double Score);
}
