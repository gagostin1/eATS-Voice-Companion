using EatsVoiceCompanion.App.Services;

namespace EatsVoiceCompanion.Tests;

public sealed class AudioRecorderLifecycleTests : IDisposable
{
    private readonly string _directory = Path.Combine(
        Path.GetTempPath(),
        $"EatsVoiceCompanionAudio-{Guid.NewGuid():N}");

    [Fact]
    public void Stop_WhenIdle_IsSafeAndRemainsIdle()
    {
        using AudioRecorder recorder = CreateRecorder();

        recorder.Stop();

        Assert.False(recorder.IsRecording);
        Assert.Null(recorder.CurrentFilePath);
    }

    [Fact]
    public void Dispose_WhenIdle_IsSafe()
    {
        AudioRecorder recorder = CreateRecorder();

        recorder.Dispose();
        recorder.Dispose();

        Assert.False(recorder.IsRecording);
    }

    private AudioRecorder CreateRecorder()
    {
        return new AudioRecorder(
            new RecordingStorageService(7, 100, _directory));
    }

    public void Dispose()
    {
        if (Directory.Exists(_directory))
        {
            Directory.Delete(_directory, recursive: true);
        }
    }
}
