using System.IO;

namespace EatsVoiceCompanion.App.Configuration;

public sealed class CompanionSettings
{
    public string ControllerPosition { get; set; } = "Atlanta Center";

    public string EatsDataDirectory { get; set; } = GetDefaultEatsDataDirectory();

    public int SnapshotFreshnessMinutes { get; set; } = 3;

    public int RecordingRetentionDays { get; set; } = 7;

    public int MaximumSavedRecordings { get; set; } = 100;

    public string? PreferredMicrophoneName { get; set; }

    public bool AutomaticallyStageVerifiedCommands { get; set; } = true;

    public static string GetDefaultEatsDataDirectory()
    {
        return Path.Combine(
            Environment.GetFolderPath(
                Environment.SpecialFolder.LocalApplicationData),
            "ATSim2020",
            "eATS");
    }

    public void Validate()
    {
        if (string.IsNullOrWhiteSpace(EatsDataDirectory))
        {
            throw new ArgumentException(
                "An eATS data directory is required.",
                nameof(EatsDataDirectory));
        }

        if (SnapshotFreshnessMinutes is < 1 or > 60)
        {
            throw new ArgumentOutOfRangeException(
                nameof(SnapshotFreshnessMinutes),
                "Snapshot freshness must be between 1 and 60 minutes.");
        }

        if (RecordingRetentionDays is < 1 or > 365)
        {
            throw new ArgumentOutOfRangeException(
                nameof(RecordingRetentionDays),
                "Recording retention must be between 1 and 365 days.");
        }

        if (MaximumSavedRecordings is < 1 or > 1000)
        {
            throw new ArgumentOutOfRangeException(
                nameof(MaximumSavedRecordings),
                "Maximum saved recordings must be between 1 and 1,000.");
        }

        EatsDataDirectory = Path.GetFullPath(EatsDataDirectory.Trim());
        ControllerPosition = ControllerPosition.Trim();
        PreferredMicrophoneName =
            string.IsNullOrWhiteSpace(PreferredMicrophoneName)
                ? null
                : PreferredMicrophoneName.Trim();
    }
}
