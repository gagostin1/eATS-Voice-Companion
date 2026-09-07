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

public sealed class SpeechRecognitionService : ISpeechRecognitionService
{
    private const string ModelFileName = "ggml-base.en.bin";
    private const long ExpectedModelFileSizeBytes = 147_964_211;
    private const string ExpectedModelSha256 =
        "A03779C86DF3323075F5E796CB2CE5029F00EC8869EEE3FDFB897AFE36C6D002";
    private const string RecognitionPrompt =
    "Air traffic control phraseology. " +
    "Delta one two three, turn left heading two seven zero. " +
    "United seven one four, climb and maintain flight level two three zero. " +
    "Fly heading. Turn right heading. " +
    "American four five, descend and maintain one zero thousand. " +
    "United seven fourteen. " +
    "American five twenty-one. " +
    "Asiana twenty-five. " +
    "Descend and maintain. Maintain speed. Proceed direct. " +
    "Cross OZZZI at and maintain one two thousand at two five zero knots. " +
    "The Atlanta altimeter two niner niner two. " +
    "Descend via. Descend via except maintain one two thousand.";

    private readonly AppLogger? _logger;
    private bool _modelValidated;
    private DateTime _validatedLastWriteTimeUtc;

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
                    .GetGgmlModelAsync(GgmlType.BaseEn);

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

        await EnsureModelAsync(
            progress,
            cancellationToken);

        progress?.Report("Transcribing recording locally...");

        using var whisperFactory =
            WhisperFactory.FromPath(ModelPath);

        string completePrompt = RecognitionPrompt;

        if (!string.IsNullOrWhiteSpace(additionalPrompt))
        {
            completePrompt += " " + additionalPrompt;
        }

        await using var processor =
            whisperFactory.CreateBuilder()
                .WithLanguage("en")
                .WithPrompt(completePrompt)
                .WithSingleSegment()
                .Build();

        await using FileStream audioStream =
            File.OpenRead(wavFilePath);

        StringBuilder transcript = new();

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

        return transcript.ToString().Trim();
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
