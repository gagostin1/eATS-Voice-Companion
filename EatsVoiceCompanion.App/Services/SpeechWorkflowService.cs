using System.Diagnostics;

namespace EatsVoiceCompanion.App.Services;

public sealed record SpeechWorkflowTimings(
    double ContextBeforeMs,
    double TranscriptionMs,
    double ContextAfterMs,
    double TotalMs);

public sealed record SpeechWorkflowResult(
    string Transcript,
    RecognitionContextResult ContextBeforeTranscription,
    RecognitionContextResult ContextAfterTranscription,
    SpeechWorkflowTimings Timings);

public sealed class SpeechWorkflowService
{
    private readonly ISpeechRecognitionService _speechRecognitionService;
    private readonly RecognitionContextService _recognitionContextService;
    private readonly LocalCorrectionMemoryService? _correctionMemoryService;

    public SpeechWorkflowService(
        ISpeechRecognitionService speechRecognitionService,
        RecognitionContextService recognitionContextService,
        LocalCorrectionMemoryService? correctionMemoryService = null)
    {
        ArgumentNullException.ThrowIfNull(speechRecognitionService);
        ArgumentNullException.ThrowIfNull(recognitionContextService);

        _speechRecognitionService = speechRecognitionService;
        _recognitionContextService = recognitionContextService;
        _correctionMemoryService = correctionMemoryService;
    }

    public async Task<SpeechWorkflowResult> RunAsync(
        string wavFilePath,
        string? controllerPosition,
        IProgress<string>? speechProgress = null,
        IProgress<RecognitionContextResult>? contextProgress = null,
        CancellationToken cancellationToken = default)
    {
        Stopwatch totalStopwatch = Stopwatch.StartNew();
        Stopwatch stageStopwatch = Stopwatch.StartNew();
        RecognitionContextResult contextBefore =
            _recognitionContextService.Build(controllerPosition);
        contextBefore = _correctionMemoryService?.Enrich(contextBefore) ??
            contextBefore;
        double contextBeforeMs = stageStopwatch.Elapsed.TotalMilliseconds;

        contextProgress?.Report(contextBefore);

        stageStopwatch.Restart();
        string transcript =
            await _speechRecognitionService.TranscribeAsync(
                wavFilePath,
                speechProgress,
                contextBefore.Prompt,
                cancellationToken);
        double transcriptionMs = stageStopwatch.Elapsed.TotalMilliseconds;

        cancellationToken.ThrowIfCancellationRequested();

        stageStopwatch.Restart();
        RecognitionContextResult contextAfter =
            _recognitionContextService.Build(controllerPosition);
        contextAfter = _correctionMemoryService?.Enrich(contextAfter) ??
            contextAfter;
        double contextAfterMs = stageStopwatch.Elapsed.TotalMilliseconds;

        contextProgress?.Report(contextAfter);

        return new SpeechWorkflowResult(
            transcript,
            contextBefore,
            contextAfter,
            new SpeechWorkflowTimings(
                contextBeforeMs,
                transcriptionMs,
                contextAfterMs,
                totalStopwatch.Elapsed.TotalMilliseconds));
    }
}
