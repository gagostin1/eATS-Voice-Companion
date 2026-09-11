namespace EatsVoiceCompanion.Core.Speech;

public enum VoiceInterpretationKind
{
    Strict,
    Recovered
}

public sealed record VoiceCommandInterpretation(
    ParsedVoiceCommand Command,
    VoiceInterpretationKind Kind,
    string InterpretedTranscript,
    string? RecoveredInstructionPhrase = null,
    bool StarWasCorrected = false);

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
        IReadOnlyDictionary<string, string> activeStars)
    {
        ArgumentNullException.ThrowIfNull(activeCallsigns);
        ArgumentNullException.ThrowIfNull(activeStars);

        ParsedVoiceCommand parsed;

        try
        {
            parsed = _parser.Parse(transcript, controllerPosition);
        }
        catch (Exception exception)
            when (exception is ArgumentException or
                InvalidOperationException)
        {
            VoiceTranscriptRecoveryResult? recovery =
                VoiceTranscriptRecovery.TryRecover(
                    transcript,
                    activeCallsigns,
                    _airlineAliases,
                    activeStars);

            if (recovery is null)
            {
                IReadOnlyList<string> candidates =
                    VoiceTranscriptRecovery.SuggestCallsigns(
                        transcript,
                        activeCallsigns,
                        _airlineAliases);

                string message = candidates.Count == 0
                    ? exception.Message
                    : exception.Message + " Possible active callsigns: " +
                      string.Join(", ", candidates) + ".";

                throw new VoiceInterpretationException(
                    message,
                    candidates,
                    exception);
            }

            parsed = _parser.Parse(recovery.RecoveredTranscript);
            return Recovered(parsed, recovery);
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
                return Recovered(parsed, recovery);
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

                throw new VoiceInterpretationException(
                    cause.Message + " Possible active callsigns: " +
                    string.Join(", ", candidates) + ".",
                    candidates,
                    cause);
            }
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
                return Recovered(parsed, recovery);
            }
        }

        return new VoiceCommandInterpretation(
            parsed,
            VoiceInterpretationKind.Strict,
            transcript);
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
}
