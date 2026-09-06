using System.Windows;
using System.Windows.Controls;
using EatsVoiceCompanion.App.Services;
using EatsVoiceCompanion.Core.Commands;
using System.Windows.Input;
using EatsVoiceCompanion.Core.Speech;

namespace EatsVoiceCompanion.App;

public partial class MainWindow : Window
{
    private static readonly IReadOnlyDictionary<string, string>
        AirlineAliases =
            new Dictionary<string, string>(
                StringComparer.OrdinalIgnoreCase)
            {
                ["Delta"] = "DAL",
                ["United"] = "UAL",
                ["American"] = "AAL",
                ["Air Canada"] = "ACA"
            };
    private readonly EatsProcessDetector _detector = new();
    private readonly MicrophoneService _microphoneService = new();
    private readonly AudioRecorder _audioRecorder = new();
    private readonly SpeechRecognitionService
    _speechRecognitionService = new();
    private readonly VoiceCommandParser _voiceCommandParser =
        new(AirlineAliases);

    public MainWindow()
    {
        InitializeComponent();

        _audioRecorder.RecordingCompleted +=
            AudioRecorder_RecordingCompleted;

        LoadMicrophones();
    }

    private void DetectEats_Click(object sender, RoutedEventArgs e)
    {
        EatsProcessInfo? result = _detector.FindRunningInstance();

        if (result is null)
        {
            StatusText.Text =
                "eATS was not detected. Start eATS, wait for its main window, and try again.";
            return;
        }

        StatusText.Text =
            $"eATS detected successfully.\n\n" +
            $"Process ID: {result.ProcessId}\n" +
            $"Window title: {result.WindowTitle}\n" +
            $"Window handle: 0x{result.MainWindowHandle:X}\n" +
            $"Version: {result.ProductVersion ?? "Unavailable"}\n" +
            $"Location: {result.ExecutablePath ?? "Unavailable"}";
    }

    private void BuildPreview_Click(object sender, RoutedEventArgs e)
    {
        PreviewErrorText.Text = string.Empty;

        try
        {
            if (CommandTypeBox.SelectedItem is not ComboBoxItem selectedItem ||
                selectedItem.Tag is not string commandType)
            {
                throw new InvalidOperationException(
                    "Select an instruction type.");
            }

            string instruction = commandType switch
            {
                "FlyHeading" =>
                    EatsCommandFormatter.FlyHeading(
                        ParseNumber("heading")),

                "TurnLeft" =>
                    EatsCommandFormatter.TurnLeftHeading(
                        ParseNumber("heading")),

                "TurnRight" =>
                    EatsCommandFormatter.TurnRightHeading(
                        ParseNumber("heading")),

                "Climb" =>
                    EatsCommandFormatter.ClimbAndMaintain(
                        ParseNumber("altitude")),

                "Descend" =>
                    EatsCommandFormatter.DescendAndMaintain(
                        ParseNumber("altitude")),

                "Speed" =>
                    EatsCommandFormatter.MaintainSpeed(
                        ParseNumber("speed")),

                "Direct" =>
                    EatsCommandFormatter.ProceedDirect(
                        CommandValueTextBox.Text),

                _ => throw new InvalidOperationException(
                    "The selected instruction is not supported.")
            };

            PreviewText.Text =
                EatsCommandFormatter.BuildTransmission(
                    CallsignTextBox.Text,
                    instruction);
        }
        catch (Exception exception)
        {
            PreviewText.Text = "Command not generated.";
            PreviewErrorText.Text = exception.Message;
        }
    }

    private int ParseNumber(string valueName)
    {
        if (!int.TryParse(CommandValueTextBox.Text, out int value))
        {
            throw new ArgumentException(
                $"Enter a valid numeric {valueName}.");
        }

        return value;
    }

    private void RefreshMicrophones_Click(
        object sender,
        RoutedEventArgs e)
    {
        LoadMicrophones();
    }

    private void LoadMicrophones()
    {
        try
        {
            IReadOnlyList<AudioInputDevice> devices =
                _microphoneService.GetDevices();

            MicrophoneComboBox.ItemsSource = devices;

            if (devices.Count == 0)
            {
                MicrophoneStatusText.Text =
                    "No recording devices were detected.";

                return;
            }

            MicrophoneComboBox.SelectedIndex = 0;

            MicrophoneStatusText.Text =
                $"{devices.Count} recording device(s) detected.";
        }
        catch (Exception exception)
        {
            MicrophoneStatusText.Text =
                $"Unable to enumerate microphones: {exception.Message}";
        }
    }

    private void RecordButton_MouseDown(
    object sender,
    MouseButtonEventArgs e)
{
    e.Handled = true;

    if (MicrophoneComboBox.SelectedItem
        is not AudioInputDevice microphone)
    {
        RecordingStatusText.Text =
            "Select a microphone first.";

        return;
    }

    try
    {
        RecordButton.CaptureMouse();

        string filePath = _audioRecorder.Start(
            microphone.DeviceNumber);

        RecordButton.Content = "Recording — release to stop";
        RecordingStatusText.Text =
            $"Recording from {microphone.Name}\n{filePath}";
    }
    catch (Exception exception)
    {
        RecordButton.ReleaseMouseCapture();
        RecordButton.Content = "Hold to record";
        RecordingStatusText.Text =
            $"Recording could not start: {exception.Message}";
    }
}

    private void RecordButton_MouseUp(
        object sender,
        MouseButtonEventArgs e)
    {
        e.Handled = true;

        RecordButton.ReleaseMouseCapture();

        if (!_audioRecorder.IsRecording)
        {
            return;
        }

        RecordButton.Content = "Finishing recording...";
        RecordButton.IsEnabled = false;
        RecordingStatusText.Text = "Saving WAV file...";

        _audioRecorder.Stop();
    }

    private async void AudioRecorder_RecordingCompleted(
        object? sender,
        AudioRecordingCompletedEventArgs e)
    {
        await Dispatcher.InvokeAsync(() =>
        {
            if (e.Error is not null)
            {
                RecordButton.Content = "Hold to record";
                RecordButton.IsEnabled = true;

                RecordingStatusText.Text =
                    $"Recording failed: {e.Error.Message}";

                SpeechStatusText.Text =
                    "Speech recognition was not started.";

                return;
            }

            RecordingStatusText.Text =
                $"Recording saved successfully:\n{e.FilePath}";

            RecordButton.Content = "Transcribing...";
            TranscriptText.Text = "Working...";
            SpeechStatusText.Text =
                "Preparing speech recognition...";
        });

        if (e.Error is not null)
        {
            return;
        }

        IProgress<string> progress =
            new Progress<string>(message =>
            {
                Dispatcher.BeginInvoke(
                    new Action(() =>
                    {
                        SpeechStatusText.Text = message;
                    }));
            });

        string transcript;

        try
        {
            transcript =
                await _speechRecognitionService.TranscribeAsync(
                    e.FilePath,
                    progress);
        }
        catch (Exception exception)
        {
            await Dispatcher.InvokeAsync(() =>
            {
                TranscriptText.Text =
                    "Transcription failed.";

                SpeechStatusText.Text =
                    exception.Message;

                PreviewText.Text =
                    "Command not generated.";

                PreviewErrorText.Text =
                    "Speech recognition did not complete.";

                RecordButton.Content = "Hold to record";
                RecordButton.IsEnabled = true;
            });

            return;
        }

        await Dispatcher.InvokeAsync(() =>
        {
            TranscriptText.Text =
                string.IsNullOrWhiteSpace(transcript)
                    ? "No speech was recognized."
                    : transcript;

            BuildVoicePreview(transcript);

            RecordButton.Content = "Hold to record";
            RecordButton.IsEnabled = true;
        });
    }

    private void BuildVoicePreview(string transcript)
    {
        PreviewErrorText.Text = string.Empty;

        if (string.IsNullOrWhiteSpace(transcript))
        {
            PreviewText.Text =
                "Command not generated.";

            PreviewErrorText.Text =
                "No speech was recognized.";

            SpeechStatusText.Text =
                "Transcription completed without recognizable speech.";

            return;
        }

        try
        {
            ParsedVoiceCommand parsed =
                _voiceCommandParser.Parse(transcript);

            PreviewText.Text =
                parsed.ToEatsCommand();

            SpeechStatusText.Text =
                "Transcription completed and command preview generated.";
        }
        catch (Exception exception)
        {
            PreviewText.Text =
                "Command not generated.";

            PreviewErrorText.Text =
                exception.Message;

            SpeechStatusText.Text =
                "Transcription completed, but the command " +
                "could not be interpreted.";
        }
    }

}