namespace EatsVoiceCompanion.App.Services;

public sealed record SpeechWorkflowResult(
    string Transcript,
    RecognitionContextResult ContextBeforeTranscription,
    RecognitionContextResult ContextAfterTranscription);

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
        RecognitionContextResult contextBefore =
            _recognitionContextService.Build(controllerPosition);
        contextBefore = _correctionMemoryService?.Enrich(contextBefore) ??
            contextBefore;

        contextProgress?.Report(contextBefore);

        string transcript =
            await _speechRecognitionService.TranscribeAsync(
                wavFilePath,
                speechProgress,
                contextBefore.Prompt,
                cancellationToken);

        cancellationToken.ThrowIfCancellationRequested();

        RecognitionContextResult contextAfter =
            _recognitionContextService.Build(controllerPosition);
        contextAfter = _correctionMemoryService?.Enrich(contextAfter) ??
            contextAfter;

        contextProgress?.Report(contextAfter);

        return new SpeechWorkflowResult(
            transcript,
            contextBefore,
            contextAfter);
    }
}
