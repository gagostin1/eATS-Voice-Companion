using NAudio.Wave;

namespace EatsVoiceCompanion.App.Services;

public enum AudioQualitySeverity
{
    Good,
    Warning,
    Silent
}

public sealed record AudioQualityResult(
    double DurationSeconds,
    double RmsDbfs,
    double PeakDbfs,
    double ClippedSamplePercent,
    AudioQualitySeverity Severity,
    string Message)
{
    public bool IsEffectivelySilent => Severity == AudioQualitySeverity.Silent;
}

public sealed class AudioQualityAnalyzer
{
    private const double MinimumSpeechDurationSeconds = 0.15;
    private const double ShortRecordingSeconds = 0.45;
    private const double SilentRmsDbfs = -58;
    private const double SilentPeakDbfs = -45;
    private const double QuietRmsDbfs = -34;
    private const double ClippedSampleThreshold = 0.5;

    public AudioQualityResult Analyze(string wavFilePath)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(wavFilePath);

        using WaveFileReader reader = new(wavFilePath);
        ISampleProvider samples = reader.ToSampleProvider();
        float[] buffer = new float[4096];
        long sampleCount = 0;
        long clippedSamples = 0;
        double squareSum = 0;
        double peak = 0;
        int read;

        while ((read = samples.Read(buffer, 0, buffer.Length)) > 0)
        {
            for (int index = 0; index < read; index++)
            {
                double absolute = Math.Abs(buffer[index]);
                peak = Math.Max(peak, absolute);
                squareSum += buffer[index] * buffer[index];
                sampleCount++;

                if (absolute >= 0.995)
                {
                    clippedSamples++;
                }
            }
        }

        double duration = reader.TotalTime.TotalSeconds;
        double rms = sampleCount == 0
            ? 0
            : Math.Sqrt(squareSum / sampleCount);
        double rmsDbfs = ToDbfs(rms);
        double peakDbfs = ToDbfs(peak);
        double clippedPercent = sampleCount == 0
            ? 0
            : clippedSamples * 100d / sampleCount;

        if (duration < MinimumSpeechDurationSeconds ||
            (rmsDbfs < SilentRmsDbfs && peakDbfs < SilentPeakDbfs))
        {
            return new AudioQualityResult(
                duration,
                rmsDbfs,
                peakDbfs,
                clippedPercent,
                AudioQualitySeverity.Silent,
                "No meaningful audio was detected. Nothing was sent.");
        }

        List<string> warnings = [];

        if (duration < ShortRecordingSeconds)
        {
            warnings.Add("The recording was very short");
        }

        if (rmsDbfs < QuietRmsDbfs)
        {
            warnings.Add("the microphone level was low");
        }

        if (clippedPercent >= ClippedSampleThreshold)
        {
            warnings.Add("the microphone clipped");
        }

        if (warnings.Count > 0)
        {
            return new AudioQualityResult(
                duration,
                rmsDbfs,
                peakDbfs,
                clippedPercent,
                AudioQualitySeverity.Warning,
                string.Join("; ", warnings) +
                ". Recognition continued with best effort.");
        }

        return new AudioQualityResult(
            duration,
            rmsDbfs,
            peakDbfs,
            clippedPercent,
            AudioQualitySeverity.Good,
            $"Audio level looks good ({duration:0.0}s, {rmsDbfs:0} dBFS).");
    }

    private static double ToDbfs(double amplitude) =>
        amplitude <= 0
            ? -120
            : 20 * Math.Log10(amplitude);
}
