using System.IO;
using System.Text;
using Whisper.net;
using Whisper.net.Ggml;

namespace EatsVoiceCompanion.App.Services;

public sealed class SpeechRecognitionService
{
    private const string ModelFileName = "ggml-base.en.bin";
    private const string RecognitionPrompt =
    "Air traffic control phraseology. " +
    "Delta one two three, turn left heading two seven zero. " +
    "United seven one four, climb and maintain flight level two three zero. " +
    "Fly heading. Turn right heading. " +
    "American four five, descend and maintain one zero thousand. " +
    "United seven fourteen. " +
    "American five twenty-one. " +
    "Asiana twenty-five. " +
    "Descend and maintain. Maintain speed. Proceed direct.";

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

        using var processor =
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
                .ProcessAsync(audioStream)
                .WithCancellation(cancellationToken))
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
}