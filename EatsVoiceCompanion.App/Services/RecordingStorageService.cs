using System.IO;

namespace EatsVoiceCompanion.App.Services;

public sealed class RecordingStorageService
{
    private int _retentionDays;
    private int _maximumRecordings;

    public RecordingStorageService(
        int retentionDays,
        int maximumRecordings,
        string? recordingDirectory = null)
    {
        RecordingDirectory = recordingDirectory ?? Path.Combine(
            Path.GetTempPath(),
            "EatsVoiceCompanion");

        UpdatePolicy(retentionDays, maximumRecordings);
    }

    public string RecordingDirectory { get; }

    public void UpdatePolicy(
        int retentionDays,
        int maximumRecordings)
    {
        if (retentionDays is < 1 or > 365)
        {
            throw new ArgumentOutOfRangeException(nameof(retentionDays));
        }

        if (maximumRecordings is < 1 or > 1000)
        {
            throw new ArgumentOutOfRangeException(nameof(maximumRecordings));
        }

        _retentionDays = retentionDays;
        _maximumRecordings = maximumRecordings;
    }

    public string CreateFilePath()
    {
        Directory.CreateDirectory(RecordingDirectory);

        return Path.Combine(
            RecordingDirectory,
            $"recording-{DateTime.UtcNow:yyyyMMdd-HHmmss-fff}-" +
            $"{Guid.NewGuid():N}.wav");
    }

    public int Cleanup(DateTime? utcNow = null)
    {
        if (!Directory.Exists(RecordingDirectory))
        {
            return 0;
        }

        DateTime cutoff =
            (utcNow ?? DateTime.UtcNow).AddDays(-_retentionDays);

        FileInfo[] files = new DirectoryInfo(RecordingDirectory)
            .GetFiles("recording-*.wav", SearchOption.TopDirectoryOnly)
            .OrderByDescending(file => file.LastWriteTimeUtc)
            .ToArray();

        int deleted = 0;

        for (int index = 0; index < files.Length; index++)
        {
            FileInfo file = files[index];

            if (file.LastWriteTimeUtc >= cutoff &&
                index < _maximumRecordings)
            {
                continue;
            }

            try
            {
                file.Delete();
                deleted++;
            }
            catch (IOException)
            {
                // A recording that is still in use is retained for the next pass.
            }
            catch (UnauthorizedAccessException)
            {
                // Cleanup is best-effort and must not prevent recording.
            }
        }

        return deleted;
    }
}
