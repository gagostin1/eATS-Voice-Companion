using System.Diagnostics;
using System.IO;
using System.Text;
using Whisper.net;
using Whisper.net.Ggml;

namespace EatsVoiceCompanion.App.Services;

public interface ISpeechRecognitionService
{
    Task<string> TranscribeAsync(
        string wavFilePath,
        IProgress<string>? progress = null,
        string? additionalPrompt = null,
        CancellationToken cancellationToken = default);
}

public sealed class SpeechRecognitionService : ISpeechRecognitionService, IDisposable
{
    private const string ModelFileName = "ggml-small.en.bin";
    private const long ExpectedModelFileSizeBytes = 487_614_201;
    private const string ExpectedModelSha256 =
        "C6138D6D58ECC8322097E0F987C32F1BE8BB0A18532A3F88F734D1BBF9C41E5D";
    private const string RecognitionPrompt =
    "Air traffic control phraseology. " +
    "Delta one two three, turn left heading two seven zero. " +
    "United seven one four, climb and maintain flight level two three zero. " +
    "Fly heading. Turn right heading. " +
    "American four five, descend and maintain one zero thousand. " +
    "United seven fourteen. " +
    "American five twenty-one. " +
    "Asiana twenty-five. " +
    "Blue Streak fifty-five ninety-six. " +
    "Endeavor forty-eight twenty-six. " +
    "Descend and maintain. Maintain speed. Proceed direct. " +
    "Cross OZZZI at and maintain one two thousand at two five zero knots. " +
    "The Atlanta altimeter two niner niner two. " +
    "Descend via. Descend via the BANKR Five arrival. " +
    "Descend via except maintain one two thousand. " +
    "Descend at pilot's discretion, maintain one two thousand. " +
    "Expedite. Expedite descent through flight level two eight zero. " +
    "Report leaving flight level two four zero. " +
    "Report reaching one two thousand. Say altitude. " +
    "Comply with speed restrictions at HOMER. " +
    "Resume published speed at HOMER. " +
    "Squawk four three two one. Squawk ident. " +
    "Squawk altitude. Squawk normal. Squawk standby. Squawk VFR. " +
    "Stop altitude squawk. " +
    "Cross ten miles northwest of BURGL at flight level three three zero. " +
    "Say again.";

    private readonly AppLogger? _logger;
    private readonly SemaphoreSlim _factoryLock = new(1, 1);
    private readonly SemaphoreSlim _transcriptionLock = new(1, 1);
    private WhisperFactory? _whisperFactory;
    private DateTime _factoryModelLastWriteTimeUtc;
    private bool _modelValidated;
    private DateTime _validatedLastWriteTimeUtc;
    private bool _disposed;

    public SpeechRecognitionService(
        string? modelPath = null,
        AppLogger? logger = null)
    {
        ModelPath = modelPath ?? Path.Combine(
            Environment.GetFolderPath(
                Environment.SpecialFolder.LocalApplicationData),
            "EatsVoiceCompanion",
            "Models",
            ModelFileName);

        _logger = logger;
    }

    public string ModelPath { get; }

    public async Task WarmUpAsync(
        CancellationToken cancellationToken = default)
    {
        ObjectDisposedException.ThrowIf(_disposed, this);

        // Preserve the existing first-use download behavior. If the model has
        // already been installed, load it while the user is setting up the app
        // instead of waiting until PTT is released.
        if (!File.Exists(ModelPath))
        {
            _logger?.Information(
                "SpeechModelWarmUpSkipped",
                "Background speech-model warm-up was skipped because the model has not been downloaded yet.");
            return;
        }

        await _transcriptionLock.WaitAsync(cancellationToken);

        try
        {
            ObjectDisposedException.ThrowIf(_disposed, this);
            Stopwatch stopwatch = Stopwatch.StartNew();
            await EnsureModelAsync(cancellationToken: cancellationToken);
            (_, bool wasAlreadyLoaded) =
                await GetOrCreateFactoryAsync(cancellationToken);

            _logger?.Information(
                "SpeechModelWarmUpCompleted",
                "The local speech model is ready in memory.",
                new
                {
                    DurationMs = stopwatch.Elapsed.TotalMilliseconds,
                    WasAlreadyLoaded = wasAlreadyLoaded
                });
        }
        finally
        {
            _transcriptionLock.Release();
        }
    }

    public async Task EnsureModelAsync(
        IProgress<string>? progress = null,
        CancellationToken cancellationToken = default)
    {
        if (_modelValidated &&
            File.Exists(ModelPath) &&
            new FileInfo(ModelPath).Length == ExpectedModelFileSizeBytes &&
            File.GetLastWriteTimeUtc(ModelPath) ==
            _validatedLastWriteTimeUtc)
        {
            return;
        }

        if (await IsValidModelFileAsync(cancellationToken))
        {
            _modelValidated = true;
            _validatedLastWriteTimeUtc =
                File.GetLastWriteTimeUtc(ModelPath);
            return;
        }

        if (File.Exists(ModelPath))
        {
            progress?.Report(
                "The existing speech model is incomplete or invalid. " +
                "Downloading a clean copy...");

            _logger?.Warning(
                "SpeechModelInvalid",
                "The existing speech model failed validation and will be replaced.");

            File.Delete(ModelPath);
        }

        string? modelDirectory =
            Path.GetDirectoryName(ModelPath);

        if (modelDirectory is null)
        {
            throw new InvalidOperationException(
                "The speech-model directory could not be determined.");
        }

        Directory.CreateDirectory(modelDirectory);

        string temporaryPath = ModelPath + ".download";

        try
        {
            if (File.Exists(temporaryPath))
            {
                File.Delete(temporaryPath);
            }

            progress?.Report(
                "Downloading the English speech model. " +
                "This only happens on first use...");

            using Stream modelStream =
                await WhisperGgmlDownloader.Default
                    .GetGgmlModelAsync(GgmlType.SmallEn);

            // This scope ensures the file is closed before File.Move runs.
            await using (FileStream fileStream = new(
                temporaryPath,
                FileMode.Create,
                FileAccess.Write,
                FileShare.None))
            {
                await modelStream.CopyToAsync(
                    fileStream,
                    cancellationToken);

                await fileStream.FlushAsync(
                    cancellationToken);
            }

            File.Move(
                temporaryPath,
                ModelPath,
                overwrite: true);

            if (!await IsValidModelFileAsync(cancellationToken))
            {
                File.Delete(ModelPath);

                throw new InvalidDataException(
                    "The downloaded speech model failed validation.");
            }

            _modelValidated = true;
            _validatedLastWriteTimeUtc =
                File.GetLastWriteTimeUtc(ModelPath);

            _logger?.Information(
                "SpeechModelReady",
                "The local speech model was downloaded and validated.");
        }
        catch
        {
            if (File.Exists(temporaryPath))
            {
                File.Delete(temporaryPath);
            }

            throw;
        }
    }

    public async Task<string> TranscribeAsync(
        string wavFilePath,
        IProgress<string>? progress = null,
        string? additionalPrompt = null,
        CancellationToken cancellationToken = default)
    {
        if (string.IsNullOrWhiteSpace(wavFilePath))
        {
            throw new ArgumentException(
                "A WAV file path is required.",
                nameof(wavFilePath));
        }

        if (!File.Exists(wavFilePath))
        {
            throw new FileNotFoundException(
                "The recording could not be found.",
                wavFilePath);
        }

        ObjectDisposedException.ThrowIf(_disposed, this);
        await _transcriptionLock.WaitAsync(cancellationToken);

        try
        {
            Stopwatch totalStopwatch = Stopwatch.StartNew();
            Stopwatch stageStopwatch = Stopwatch.StartNew();

            await EnsureModelAsync(
                progress,
                cancellationToken);
            double modelValidationMs =
                stageStopwatch.Elapsed.TotalMilliseconds;

            stageStopwatch.Restart();
            (WhisperFactory whisperFactory, bool wasAlreadyLoaded) =
                await GetOrCreateFactoryAsync(cancellationToken);
            double factoryAcquisitionMs =
                stageStopwatch.Elapsed.TotalMilliseconds;

            progress?.Report("Transcribing recording locally...");

            string completePrompt = RecognitionPrompt;

            if (!string.IsNullOrWhiteSpace(additionalPrompt))
            {
                completePrompt += " " + additionalPrompt;
            }

            stageStopwatch.Restart();
            await using var processor =
                whisperFactory.CreateBuilder()
                    .WithLanguage("en")
                    .WithPrompt(completePrompt)
                    .WithSingleSegment()
                    .WithBeamSearchSamplingStrategy(
                        strategy => strategy.WithBeamSize(5))
                    .Build();
            double processorCreationMs =
                stageStopwatch.Elapsed.TotalMilliseconds;

            await using FileStream audioStream =
                File.OpenRead(wavFilePath);

            StringBuilder transcript = new();
            stageStopwatch.Restart();

            await foreach (
                var segment in processor
                    .ProcessAsync(audioStream, cancellationToken))
            {
                string text = segment.Text.Trim();

                if (text.Length > 0)
                {
                    transcript.Append(text);
                    transcript.Append(' ');
                }
            }

            double decodingMs = stageStopwatch.Elapsed.TotalMilliseconds;
            string result = transcript.ToString().Trim();

            _logger?.Information(
                "SpeechRecognitionTiming",
                "Local speech recognition completed.",
                new
                {
                    TotalMs = totalStopwatch.Elapsed.TotalMilliseconds,
                    ModelValidationMs = modelValidationMs,
                    FactoryAcquisitionMs = factoryAcquisitionMs,
                    ProcessorCreationMs = processorCreationMs,
                    DecodingMs = decodingMs,
                    WasModelAlreadyLoaded = wasAlreadyLoaded,
                    PromptCharacters = completePrompt.Length,
                    HasTranscript = result.Length > 0
                });

            return result;
        }
        finally
        {
            _transcriptionLock.Release();
        }
    }

    public void Dispose()
    {
        _transcriptionLock.Wait();

        try
        {
            _factoryLock.Wait();

            try
            {
                if (_disposed)
                {
                    return;
                }

                _disposed = true;
                _whisperFactory?.Dispose();
                _whisperFactory = null;
            }
            finally
            {
                _factoryLock.Release();
            }
        }
        finally
        {
            _transcriptionLock.Release();
        }
    }

    private async Task<(WhisperFactory Factory, bool WasAlreadyLoaded)>
        GetOrCreateFactoryAsync(CancellationToken cancellationToken)
    {
        await _factoryLock.WaitAsync(cancellationToken);

        try
        {
            ObjectDisposedException.ThrowIf(_disposed, this);
            DateTime modelLastWriteTimeUtc =
                File.GetLastWriteTimeUtc(ModelPath);

            if (_whisperFactory is not null &&
                _factoryModelLastWriteTimeUtc == modelLastWriteTimeUtc)
            {
                return (_whisperFactory, true);
            }

            _whisperFactory?.Dispose();
            _whisperFactory = await Task.Run(
                () => WhisperFactory.FromPath(ModelPath),
                cancellationToken);
            _factoryModelLastWriteTimeUtc = modelLastWriteTimeUtc;

            return (_whisperFactory, false);
        }
        finally
        {
            _factoryLock.Release();
        }
    }

    private async Task<bool> IsValidModelFileAsync(
        CancellationToken cancellationToken)
    {
        return await FileIntegrityValidator.MatchesSha256Async(
            ModelPath,
            ExpectedModelFileSizeBytes,
            ExpectedModelSha256,
            cancellationToken);
    }
}
