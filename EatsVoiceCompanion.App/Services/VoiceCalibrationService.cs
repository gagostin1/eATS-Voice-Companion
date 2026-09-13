using EatsVoiceCompanion.Core.Speech;

namespace EatsVoiceCompanion.App.Services;

public sealed record VoiceCalibrationPhrase(
    string Instruction,
    string ExpectedCommand,
    string[] ActiveCallsigns,
    Dictionary<string, string>? ActiveStars = null,
    Dictionary<string, string[]>? ActiveRouteFixes = null);

public sealed record VoiceCalibrationResult(
    string Transcript,
    string? GeneratedCommand,
    bool IsMatch,
    bool WasBestEffort,
    string? Error = null);

public static class VoiceCalibrationService
{
    private static readonly IReadOnlyDictionary<string, string> Aliases =
        new Dictionary<string, string>(StringComparer.OrdinalIgnoreCase)
        {
            ["Delta"] = "DAL",
            ["United"] = "UAL",
            ["American"] = "AAL",
            ["Blue Streak"] = "JIA"
        };

    public static IReadOnlyList<VoiceCalibrationPhrase> Phrases { get; } =
    [
        new(
            "Delta four eight two, climb and maintain flight level two three zero.",
            "DAL482 CM230",
            ["DAL482"]),
        new(
            "November two, eight, seven, Whiskey Romeo, turn left heading one eight zero.",
            "N287WR TLH180",
            ["N287WR"]),
        new(
            "Delta eight two five, cross OZZZI at and maintain one three thousand.",
            "DAL825 XOZZZI@130",
            ["DAL825"],
            ActiveRouteFixes: new(StringComparer.OrdinalIgnoreCase)
            {
                ["DAL825"] = ["OZZZI", "WINNG"]
            }),
        new(
            "American nine four zero, the Atlanta altimeter two niner niner two.",
            "AAL940 A2992",
            ["AAL940"]),
        new(
            "Blue Streak five zero three niner, descend via the BANKR Five arrival.",
            "JIA5039 DV",
            ["JIA5039"],
            ActiveStars: new(StringComparer.OrdinalIgnoreCase)
            {
                ["JIA5039"] = "BANKR5"
            }),
        new(
            "United seven one four, descend and maintain one two thousand.",
            "UAL714 DM120",
            ["UAL714"]),
        new(
            "American five two one, maintain speed two five zero.",
            "AAL521 S250",
            ["AAL521"]),
        new(
            "November six, zero, four, Lima, turn right heading three six zero.",
            "N604L TRH360",
            ["N604L"]),
        new(
            "Delta one two eight eight, proceed direct WINNG.",
            "DAL1288 ..WINNG",
            ["DAL1288"],
            ActiveRouteFixes: new(StringComparer.OrdinalIgnoreCase)
            {
                ["DAL1288"] = ["WINNG", "OZZZI"]
            }),
        new(
            "United three eight eight, squawk four three two one.",
            "UAL388 SQ4321",
            ["UAL388"])
    ];

    public static string RecognitionPrompt =>
        "Voice setup calibration with digit-by-digit aviation numbers. " +
        "Callsigns include Delta four eight two, November two eight seven " +
        "Whiskey Romeo, American nine four zero, Blue Streak five zero three " +
        "niner, United seven one four, and November six zero four Lima. " +
        "Fixes OZZZI and WINNG. BANKR Five arrival. " +
        "Atlanta altimeter two niner niner two.";

    public static VoiceCalibrationResult Evaluate(
        VoiceCalibrationPhrase phrase,
        string transcript)
    {
        ArgumentNullException.ThrowIfNull(phrase);

        if (string.IsNullOrWhiteSpace(transcript))
        {
            return new VoiceCalibrationResult(
                transcript,
                null,
                false,
                false,
                "No speech was recognized.");
        }

        try
        {
            IReadOnlyDictionary<string, string> stars =
                phrase.ActiveStars ??
                new Dictionary<string, string>(
                    StringComparer.OrdinalIgnoreCase);
            IReadOnlyDictionary<string, IReadOnlySet<string>> routeFixes =
                (phrase.ActiveRouteFixes ??
                 new Dictionary<string, string[]>(
                     StringComparer.OrdinalIgnoreCase))
                .ToDictionary(
                    pair => pair.Key,
                    pair => (IReadOnlySet<string>)pair.Value.ToHashSet(
                        StringComparer.OrdinalIgnoreCase),
                    StringComparer.OrdinalIgnoreCase);

            VoiceCommandInterpretation interpretation =
                new VoiceCommandInterpreter(Aliases).Interpret(
                    transcript,
                    null,
                    phrase.ActiveCallsigns.ToHashSet(
                        StringComparer.OrdinalIgnoreCase),
                    stars,
                    routeFixes);
            string command = interpretation.Command.ToEatsCommand();

            return new VoiceCalibrationResult(
                transcript,
                command,
                string.Equals(
                    command,
                    phrase.ExpectedCommand,
                    StringComparison.OrdinalIgnoreCase),
                interpretation.Kind != VoiceInterpretationKind.Strict);
        }
        catch (Exception exception)
            when (exception is ArgumentException or InvalidOperationException)
        {
            return new VoiceCalibrationResult(
                transcript,
                null,
                false,
                false,
                exception.Message);
        }
    }

    public static CorrectionHistoryEntry CreateHistoryEntry(
        VoiceCalibrationPhrase phrase,
        VoiceCalibrationResult result,
        string recordingFilePath)
    {
        ArgumentNullException.ThrowIfNull(phrase);
        ArgumentNullException.ThrowIfNull(result);

        return new CorrectionHistoryEntry
        {
            RecordingFilePath = recordingFilePath,
            OriginalTranscript = result.Transcript,
            GeneratedCommand = result.GeneratedCommand,
            WasBestEffort = result.WasBestEffort,
            ControllerPosition = "Voice setup calibration",
            RecognitionPrompt = RecognitionPrompt,
            ActiveCallsigns = phrase.ActiveCallsigns,
            ActiveStars = phrase.ActiveStars ??
                new Dictionary<string, string>(
                    StringComparer.OrdinalIgnoreCase),
            ActiveRouteFixes = phrase.ActiveRouteFixes ??
                new Dictionary<string, string[]>(
                    StringComparer.OrdinalIgnoreCase),
            ReviewStatus = result.IsMatch
                ? CorrectionReviewStatus.Correct
                : CorrectionReviewStatus.Corrected,
            CorrectedTranscript = result.IsMatch
                ? result.Transcript
                : phrase.Instruction,
            ExpectedCommand = phrase.ExpectedCommand,
            UseForLocalLearning = true
        };
    }
}
