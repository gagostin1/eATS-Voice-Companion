using EatsVoiceCompanion.App.Services;

namespace EatsVoiceCompanion.Tests;

public sealed class SpeechWorkflowServiceTests
{
    [Fact]
    public async Task RunAsync_UsesContextAndRefreshesAfterTranscription()
    {
        DateTime now = DateTime.UtcNow;
        CountingSnapshotService snapshots = new(
            new EatsSnapshotData(["AAL123"], now));
        FakeSpeechRecognitionService speech = new("recognized text");
        RecognitionContextService context = new(
            new RunningProcessDetector(),
            snapshots,
            new Dictionary<string, string>
            {
                ["AMERICAN"] = "AAL"
            },
            TimeSpan.FromMinutes(3),
            () => now);
        SpeechWorkflowService workflow = new(speech, context);

        SpeechWorkflowResult result = await workflow.RunAsync(
            "recording.wav",
            "Atlanta Center");

        Assert.Equal("recognized text", result.Transcript);
        Assert.Equal(2, snapshots.LoadCount);
        Assert.Contains("American 123", speech.AdditionalPrompt);
        Assert.True(result.ContextAfterTranscription.HasFreshSnapshot);
    }

    [Fact]
    public async Task RunAsync_CancellationStopsBeforeContextRefresh()
    {
        DateTime now = DateTime.UtcNow;
        CountingSnapshotService snapshots = new(
            new EatsSnapshotData(["AAL123"], now));
        CancelableSpeechRecognitionService speech = new();
        RecognitionContextService context = new(
            new RunningProcessDetector(),
            snapshots,
            new Dictionary<string, string>
            {
                ["AMERICAN"] = "AAL"
            },
            TimeSpan.FromMinutes(3),
            () => now);
        SpeechWorkflowService workflow = new(speech, context);

        using CancellationTokenSource cancellation = new();
        Task<SpeechWorkflowResult> operation = workflow.RunAsync(
            "recording.wav",
            "Atlanta Center",
            cancellationToken: cancellation.Token);

        await speech.Started;
        cancellation.Cancel();

        await Assert.ThrowsAnyAsync<OperationCanceledException>(
            () => operation);
        Assert.Equal(1, snapshots.LoadCount);
    }

    private sealed class FakeSpeechRecognitionService(string transcript)
        : ISpeechRecognitionService
    {
        public string? AdditionalPrompt { get; private set; }

        public Task<string> TranscribeAsync(
            string wavFilePath,
            IProgress<string>? progress = null,
            string? additionalPrompt = null,
            CancellationToken cancellationToken = default)
        {
            AdditionalPrompt = additionalPrompt;
            return Task.FromResult(transcript);
        }
    }

    private sealed class CancelableSpeechRecognitionService
        : ISpeechRecognitionService
    {
        private readonly TaskCompletionSource _started = new(
            TaskCreationOptions.RunContinuationsAsynchronously);

        public Task Started => _started.Task;

        public async Task<string> TranscribeAsync(
            string wavFilePath,
            IProgress<string>? progress = null,
            string? additionalPrompt = null,
            CancellationToken cancellationToken = default)
        {
            _started.SetResult();
            await Task.Delay(Timeout.InfiniteTimeSpan, cancellationToken);
            return string.Empty;
        }
    }

    private sealed class CountingSnapshotService(EatsSnapshotData snapshot)
        : IEatsSnapshotService
    {
        public int LoadCount { get; private set; }

        public EatsSnapshotData Load()
        {
            LoadCount++;
            return snapshot;
        }
    }

    private sealed class RunningProcessDetector : IEatsProcessDetector
    {
        public EatsProcessInfo FindRunningInstance()
        {
            return new EatsProcessInfo(
                1,
                "eATS",
                (nint)1,
                null,
                null);
        }
    }
}
