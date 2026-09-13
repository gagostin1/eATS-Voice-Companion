using System.Text.Json.Serialization;

namespace EatsVoiceCompanion.App.Services;

public enum CorrectionReviewStatus
{
    Unreviewed,
    Correct,
    Corrected
}

public sealed class CorrectionHistoryEntry
{
    public Guid Id { get; set; } = Guid.NewGuid();

    public DateTime RecordedAtUtc { get; set; } = DateTime.UtcNow;

    public string RecordingFilePath { get; set; } = string.Empty;

    public string OriginalTranscript { get; set; } = string.Empty;

    public string? GeneratedCommand { get; set; }

    public bool WasBestEffort { get; set; }

    public string ControllerPosition { get; set; } = string.Empty;

    public string RecognitionPrompt { get; set; } = string.Empty;

    public string[] ActiveCallsigns { get; set; } = [];

    public Dictionary<string, string> ActiveStars { get; set; } =
        new(StringComparer.OrdinalIgnoreCase);

    public Dictionary<string, string[]> ActiveRouteFixes { get; set; } =
        new(StringComparer.OrdinalIgnoreCase);

    public CorrectionReviewStatus ReviewStatus { get; set; }

    public string? CorrectedTranscript { get; set; }

    public string? ExpectedCommand { get; set; }

    public DateTime? LastReplayedAtUtc { get; set; }

    public string? LatestReplayTranscript { get; set; }

    public string? LatestReplayCommand { get; set; }

    public string? LatestReplayError { get; set; }

    [JsonIgnore]
    public string DisplayTitle =>
        $"{RecordedAtUtc.ToLocalTime():g}  ·  " +
        $"{ReviewStatus}  ·  " +
        (GeneratedCommand ?? "No command");
}

public sealed record CorrectionRegressionCase(
    int SchemaVersion,
    string AppVersion,
    DateTime RecordedAtUtc,
    string OriginalTranscript,
    string? GeneratedCommand,
    bool WasBestEffort,
    string CorrectedTranscript,
    string ExpectedCommand,
    string ControllerPosition,
    string? ActiveStar,
    string[] RouteFixes);
