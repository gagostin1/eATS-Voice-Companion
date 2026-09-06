using EatsVoiceCompanion.App.Services;

namespace EatsVoiceCompanion.Tests;

public sealed class RecordingStorageServiceTests : IDisposable
{
    private readonly string _directory = Path.Combine(
        Path.GetTempPath(),
        $"EatsVoiceCompanionRecordings-{Guid.NewGuid():N}");

    [Fact]
    public void CreateFilePath_ReturnsUniqueWavPathsInConfiguredDirectory()
    {
        RecordingStorageService service = new(7, 100, _directory);

        string first = service.CreateFilePath();
        string second = service.CreateFilePath();

        Assert.NotEqual(first, second);
        Assert.Equal(_directory, Path.GetDirectoryName(first));
        Assert.EndsWith(".wav", first, StringComparison.OrdinalIgnoreCase);
    }

    [Fact]
    public void Cleanup_RemovesExpiredAndExcessRecordingsOnly()
    {
        Directory.CreateDirectory(_directory);
        DateTime now = new(2026, 9, 6, 12, 0, 0, DateTimeKind.Utc);

        string expired = CreateRecording("recording-expired.wav", now.AddDays(-8));
        string older = CreateRecording("recording-older.wav", now.AddHours(-2));
        string newest = CreateRecording("recording-newest.wav", now.AddHours(-1));
        string unrelated = Path.Combine(_directory, "keep.txt");
        File.WriteAllText(unrelated, "keep");

        RecordingStorageService service = new(7, 1, _directory);
        int deleted = service.Cleanup(now);

        Assert.Equal(2, deleted);
        Assert.False(File.Exists(expired));
        Assert.False(File.Exists(older));
        Assert.True(File.Exists(newest));
        Assert.True(File.Exists(unrelated));
    }

    private string CreateRecording(string name, DateTime timestamp)
    {
        string path = Path.Combine(_directory, name);
        File.WriteAllText(path, "audio");
        File.SetLastWriteTimeUtc(path, timestamp);
        return path;
    }

    public void Dispose()
    {
        if (Directory.Exists(_directory))
        {
            Directory.Delete(_directory, recursive: true);
        }
    }
}
