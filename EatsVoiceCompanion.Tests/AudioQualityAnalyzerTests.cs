using EatsVoiceCompanion.App.Services;
using NAudio.Wave;

namespace EatsVoiceCompanion.Tests;

public sealed class AudioQualityAnalyzerTests : IDisposable
{
    private readonly string _directory = Path.Combine(
        Path.GetTempPath(),
        $"EatsVoiceCompanionAudioQuality-{Guid.NewGuid():N}");

    [Fact]
    public void Analyze_SilenceIsRejected()
    {
        string path = WriteWave("silence.wav", 1, _ => 0);

        AudioQualityResult result = new AudioQualityAnalyzer().Analyze(path);

        Assert.True(result.IsEffectivelySilent);
        Assert.Equal(AudioQualitySeverity.Silent, result.Severity);
    }

    [Fact]
    public void Analyze_NormalSpeechLevelIsGood()
    {
        string path = WriteWave(
            "good.wav",
            1,
            index => 0.2f * MathF.Sin(2 * MathF.PI * 440 * index / 16000));

        AudioQualityResult result = new AudioQualityAnalyzer().Analyze(path);

        Assert.Equal(AudioQualitySeverity.Good, result.Severity);
        Assert.InRange(result.DurationSeconds, 0.99, 1.01);
    }

    [Fact]
    public void Analyze_LowLevelSpeechContinuesWithWarning()
    {
        string path = WriteWave(
            "quiet.wav",
            1,
            index => 0.005f * MathF.Sin(2 * MathF.PI * 440 * index / 16000));

        AudioQualityResult result = new AudioQualityAnalyzer().Analyze(path);

        Assert.Equal(AudioQualitySeverity.Warning, result.Severity);
        Assert.False(result.IsEffectivelySilent);
        Assert.Contains("low", result.Message, StringComparison.OrdinalIgnoreCase);
    }

    [Fact]
    public void Analyze_ClippingContinuesWithWarning()
    {
        string path = WriteWave("clipped.wav", 1, _ => 1);

        AudioQualityResult result = new AudioQualityAnalyzer().Analyze(path);

        Assert.Equal(AudioQualitySeverity.Warning, result.Severity);
        Assert.False(result.IsEffectivelySilent);
        Assert.Contains("clipped", result.Message, StringComparison.OrdinalIgnoreCase);
    }

    private string WriteWave(
        string fileName,
        double durationSeconds,
        Func<int, float> sampleFactory)
    {
        Directory.CreateDirectory(_directory);
        string path = Path.Combine(_directory, fileName);
        using WaveFileWriter writer = new(path, new WaveFormat(16000, 16, 1));
        int sampleCount = (int)(16000 * durationSeconds);

        for (int index = 0; index < sampleCount; index++)
        {
            writer.WriteSample(sampleFactory(index));
        }

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
