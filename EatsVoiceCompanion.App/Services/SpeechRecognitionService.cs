using System.IO;
using System.Text;
using Whisper.net;
using Whisper.net.Ggml;

namespace EatsVoiceCompanion.App.Services;

public sealed class SpeechRecognitionService
{
    private const string ModelFileName = "ggml-base.en.bin";

    public SpeechRecognitionService()
    {
        string localAppData =
            Environment.GetFolderPath(
                Environment.SpecialFolder.LocalApplicationData);

        ModelPath = Path.Combine(
            localAppData,
            "EatsVoiceCompanion",
            "Models",
            ModelFileName);
    }

    public string ModelPath { get; }

    public async Task EnsureModelAsync(
        IProgress<string>? progress = null,
        CancellationToken cancellationToken = default)
    {
        if (File.Exists(ModelPath))
        {
            return;
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
                "The WAV recording could not be found.",
                wavFilePath);
        }

        await EnsureModelAsync(progress, cancellationToken);

        progress?.Report("Converting speech to text...");

        using WhisperFactory factory =
            WhisperFactory.FromPath(ModelPath);

        using WhisperProcessor processor =
            factory.CreateBuilder()
                .WithLanguage("en")
                .Build();

        await using FileStream audioStream =
            File.OpenRead(wavFilePath);

        StringBuilder transcript = new();

        await foreach (
            SegmentData segment in processor
                .ProcessAsync(audioStream)
                .WithCancellation(cancellationToken))
        {
            transcript.Append(segment.Text);
        }

        return transcript.ToString().Trim();
    }
}