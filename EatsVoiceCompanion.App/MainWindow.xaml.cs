using System.Windows;
using System.Windows.Controls;
using EatsVoiceCompanion.App.Services;
using EatsVoiceCompanion.Core.Commands;
using System.Windows.Input;

namespace EatsVoiceCompanion.App;

public partial class MainWindow : Window
{
    private readonly EatsProcessDetector _detector = new();
    private readonly MicrophoneService _microphoneService = new();
    private readonly AudioRecorder _audioRecorder = new();

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

    private void AudioRecorder_RecordingCompleted(
        object? sender,
        AudioRecordingCompletedEventArgs e)
    {
        Dispatcher.Invoke(() =>
        {
            RecordButton.Content = "Hold to record";
            RecordButton.IsEnabled = true;

            if (e.Error is not null)
            {
                RecordingStatusText.Text =
                    $"Recording failed: {e.Error.Message}";

                return;
            }

            RecordingStatusText.Text =
                $"Recording saved successfully:\n{e.FilePath}";
        });
    }

    protected override void OnClosed(EventArgs e)
    {
        _audioRecorder.Dispose();
        base.OnClosed(e);
    }
}