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
            PreferredMicrophoneName = "Test microphone"
        };

        service.Save(expected);
        CompanionSettings actual = service.Load();

        Assert.Equal(expected.ControllerPosition, actual.ControllerPosition);
        Assert.Equal(expected.EatsDataDirectory, actual.EatsDataDirectory);
        Assert.Equal(expected.SnapshotFreshnessMinutes, actual.SnapshotFreshnessMinutes);
        Assert.Equal(expected.RecordingRetentionDays, actual.RecordingRetentionDays);
        Assert.Equal(expected.MaximumSavedRecordings, actual.MaximumSavedRecordings);
        Assert.Equal(expected.PreferredMicrophoneName, actual.PreferredMicrophoneName);
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
    }

    public void Dispose()
    {
        if (Directory.Exists(_directory))
        {
            Directory.Delete(_directory, recursive: true);
        }
    }
}
