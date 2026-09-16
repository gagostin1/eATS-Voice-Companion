using EatsVoiceCompanion.App.Configuration;

namespace EatsVoiceCompanion.Tests;

public sealed class CompanionSettingsServiceTests : IDisposable
{
    private readonly string _directory = Path.Combine(
        Path.GetTempPath(),
        $"EatsVoiceCompanionSettings-{Guid.NewGuid():N}");

    [Fact]
    public void SaveAndLoad_RoundTripsSettings()
    {
        string path = Path.Combine(_directory, "settings.json");
        CompanionSettingsService service = new(path);

        CompanionSettings expected = new()
        {
            ControllerPosition = "Jacksonville Center",
            EatsDataDirectory = _directory,
            SnapshotFreshnessMinutes = 5,
            RecordingRetentionDays = 14,
            MaximumSavedRecordings = 50,
            PreferredMicrophoneName = "Test microphone",
            AutomaticallyStageVerifiedCommands = false,
            AlwaysOnTop = true,
            ParticipateInRecognitionImprovement = false,
            ContributionNoticeShown = true,
            ContributionConsentVersion = 2,
            ContributionInstallationId =
                "63bd5c25-0c57-4f5d-8235-dcbf5ee0e44c",
            PushToTalkHotkey = "Ctrl+Shift+F9",
            VoiceCalibrationVersion = 1,
            LastUpdateCheckUtc = new DateTimeOffset(
                2026,
                9,
                15,
                18,
                30,
                0,
                TimeSpan.Zero)
        };

        service.Save(expected);
        CompanionSettings actual = service.Load();

        Assert.Equal(expected.ControllerPosition, actual.ControllerPosition);
        Assert.Equal(expected.EatsDataDirectory, actual.EatsDataDirectory);
        Assert.Equal(expected.SnapshotFreshnessMinutes, actual.SnapshotFreshnessMinutes);
        Assert.Equal(expected.RecordingRetentionDays, actual.RecordingRetentionDays);
        Assert.Equal(expected.MaximumSavedRecordings, actual.MaximumSavedRecordings);
        Assert.Equal(expected.PreferredMicrophoneName, actual.PreferredMicrophoneName);
        Assert.Equal(
            expected.AutomaticallyStageVerifiedCommands,
            actual.AutomaticallyStageVerifiedCommands);
        Assert.Equal(expected.AlwaysOnTop, actual.AlwaysOnTop);
        Assert.Equal(
            expected.ParticipateInRecognitionImprovement,
            actual.ParticipateInRecognitionImprovement);
        Assert.Equal(
            expected.ContributionNoticeShown,
            actual.ContributionNoticeShown);
        Assert.Equal(
            expected.ContributionConsentVersion,
            actual.ContributionConsentVersion);
        Assert.Equal(
            expected.ContributionInstallationId,
            actual.ContributionInstallationId);
        Assert.Equal(expected.PushToTalkHotkey, actual.PushToTalkHotkey);
        Assert.Equal(
            expected.VoiceCalibrationVersion,
            actual.VoiceCalibrationVersion);
        Assert.Equal(
            expected.LastUpdateCheckUtc,
            actual.LastUpdateCheckUtc);
    }

    [Fact]
    public void Load_ReturnsDefaultsWhenFileDoesNotExist()
    {
        CompanionSettings result =
            new CompanionSettingsService(
                Path.Combine(_directory, "missing.json"))
            .Load();

        Assert.Equal(3, result.SnapshotFreshnessMinutes);
        Assert.Equal(7, result.RecordingRetentionDays);
        Assert.Equal(100, result.MaximumSavedRecordings);
        Assert.True(result.AutomaticallyStageVerifiedCommands);
        Assert.False(result.AlwaysOnTop);
        Assert.True(result.ParticipateInRecognitionImprovement);
        Assert.False(result.ContributionNoticeShown);
        Assert.Equal(0, result.ContributionConsentVersion);
        Assert.True(Guid.TryParse(
            result.ContributionInstallationId,
            out _));
        Assert.Null(result.PushToTalkHotkey);
        Assert.Equal(0, result.VoiceCalibrationVersion);
        Assert.Null(result.LastUpdateCheckUtc);
    }

    [Fact]
    public void Load_EnablesAutoStagingForLegacySettings()
    {
        Directory.CreateDirectory(_directory);
        string path = Path.Combine(_directory, "legacy-settings.json");
        File.WriteAllText(
            path,
            $$"""
            {
              "ControllerPosition": "Atlanta Center",
              "EatsDataDirectory": "{{_directory.Replace("\\", "\\\\")}}",
              "SnapshotFreshnessMinutes": 3,
              "RecordingRetentionDays": 7,
              "MaximumSavedRecordings": 100
            }
            """);

        CompanionSettings result =
            new CompanionSettingsService(path).Load();

        Assert.True(result.AutomaticallyStageVerifiedCommands);
        Assert.True(result.ParticipateInRecognitionImprovement);
        Assert.False(result.ContributionNoticeShown);
        Assert.Equal(0, result.ContributionConsentVersion);
    }

    public void Dispose()
    {
        if (Directory.Exists(_directory))
        {
            Directory.Delete(_directory, recursive: true);
        }
    }
}
