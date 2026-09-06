using System.Windows;
using System.Windows.Controls;
using EatsVoiceCompanion.App.Services;
using EatsVoiceCompanion.Core.Commands;
using System.Windows.Input;
using EatsVoiceCompanion.Core.Speech;
using System.Windows.Media;
using EatsVoiceCompanion.Core.Safety;

namespace EatsVoiceCompanion.App;

public partial class MainWindow : Window
{
    private IReadOnlyDictionary<string, string>
        _airlineAliases = FallbackAirlineAliases;  

    private static readonly IReadOnlyDictionary<string, string>
        FallbackAirlineAliases =
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
    private readonly VoiceCommandParser _voiceCommandParser;
    private readonly EatsAirlineAliasService
        _airlineAliasService = new();  
    private readonly EatsSnapshotService
        _snapshotService = new();

    private readonly HashSet<string> _activeCallsigns =
        new(StringComparer.OrdinalIgnoreCase);

    private bool _hasFreshSnapshot;    

    public MainWindow()
    {
        InitializeComponent();

        _voiceCommandParser =
            CreateVoiceCommandParser();

        _audioRecorder.RecordingCompleted +=
            AudioRecorder_RecordingCompleted;

        LoadMicrophones();
        BuildRecognitionContext();
    }

    private void DetectEats_Click(
        object sender,
        RoutedEventArgs e)
    {
        EatsProcessInfo? result =
            _detector.FindRunningInstance();

        if (result is null)
        {
            StatusText.Text =
                "eATS was not detected. Start eATS, " +
                "wait for its main window, and try again.";

            BuildRecognitionContext();
            return;
        }

        StatusText.Text =
            $"eATS detected successfully.\n\n" +
            $"Process ID: {result.ProcessId}\n" +
            $"Window title: {result.WindowTitle}\n" +
            $"Window handle: 0x{result.MainWindowHandle:X}\n" +
            $"Version: {result.ProductVersion ?? "Unavailable"}\n" +
            $"Location: {result.ExecutablePath ?? "Unavailable"}";

        BuildRecognitionContext();
    }

    private void BuildPreview_Click(object sender, RoutedEventArgs e)
    {
        BuildRecognitionContext();
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
                
                "Roger" =>
                    EatsCommandFormatter.Roger(),    

                _ => throw new InvalidOperationException(
                    "The selected instruction is not supported.")
            };
            string transmission =
                EatsCommandFormatter.BuildTransmission(
                    CallsignTextBox.Text,
                    instruction);

            PreviewText.Text = transmission;

            string normalizedCallsign =
                transmission.Split(
                    ' ',
                    StringSplitOptions.RemoveEmptyEntries)[0];

            UpdateCommandSafetyStatus(
                normalizedCallsign);
        }
        catch (Exception exception)
        {
            PreviewText.Text = "Command not generated.";
            PreviewErrorText.Text = exception.Message;
            MarkCommandNotReady(
                "the manual command is invalid.");
            MarkCommandNotReady(
                "speech recognition did not complete.");
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

        string recognitionContext = string.Empty;

        await Dispatcher.InvokeAsync(() =>
        {
            recognitionContext =
                BuildRecognitionContext();
        });

        try
        {
            transcript =
                await _speechRecognitionService.TranscribeAsync(
                    e.FilePath,
                    progress,
                    recognitionContext);
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

        // This is the blank-transcript branch.
        if (string.IsNullOrWhiteSpace(transcript))
        {
            PreviewText.Text =
                "Command not generated.";

            PreviewErrorText.Text =
                "No speech was recognized.";

            SpeechStatusText.Text =
                "Transcription completed without recognizable speech.";

            MarkCommandNotReady(
                "no speech was recognized.");

            return;
        }

        try
        {
            ParsedVoiceCommand parsed =
                _voiceCommandParser.Parse(
                    transcript,
                    ControllerPositionTextBox.Text);

            ApplyParsedCommandToEditor(parsed);

            PreviewText.Text =
                parsed.ToEatsCommand();

            UpdateCommandSafetyStatus(
                parsed.Callsign);

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

            MarkCommandNotReady(
                "the transcript did not produce a valid command.");
        }
    }

    private void ApplyParsedCommandToEditor(
        ParsedVoiceCommand parsed)
    {
        CallsignTextBox.Text = parsed.Callsign;

        string commandTag = parsed.InstructionType switch
        {
            VoiceInstructionType.FlyHeading =>
                "FlyHeading",

            VoiceInstructionType.TurnLeftHeading =>
                "TurnLeft",

            VoiceInstructionType.TurnRightHeading =>
                "TurnRight",

            VoiceInstructionType.ClimbAndMaintain =>
                "Climb",

            VoiceInstructionType.DescendAndMaintain =>
                "Descend",

            VoiceInstructionType.MaintainSpeed =>
                "Speed",

            VoiceInstructionType.ProceedDirect =>
                "Direct",

            VoiceInstructionType.Roger =>
                "Roger",    

            _ => throw new InvalidOperationException(
                "The instruction type is not supported.")
        };

        foreach (object item in CommandTypeBox.Items)
        {
            if (item is ComboBoxItem comboBoxItem &&
                string.Equals(
                    comboBoxItem.Tag?.ToString(),
                    commandTag,
                    StringComparison.Ordinal))
            {
                CommandTypeBox.SelectedItem =
                    comboBoxItem;

                break;
            }
        }

        CommandValueTextBox.Text =
            parsed.InstructionType ==
            VoiceInstructionType.ProceedDirect
                ? parsed.TextValue ?? string.Empty
                : parsed.NumericValue?.ToString() ??
                string.Empty;
    }

    private VoiceCommandParser CreateVoiceCommandParser()
    {
        try
        {
            _airlineAliases =
                _airlineAliasService.Load();

            AirlineDataStatusText.Text =
                $"Loaded {_airlineAliases.Count} airline callsigns from:\n" +
                _airlineAliasService.AirlineFilePath;
        }
        catch (Exception exception)
        {
            _airlineAliases =
                FallbackAirlineAliases;

            AirlineDataStatusText.Text =
                "The installed eATS airline data could not be loaded. " +
                $"Using {_airlineAliases.Count} built-in aliases.\n" +
                exception.Message;
        }

        return new VoiceCommandParser(
            _airlineAliases);
    }

    private string BuildRecognitionContext()
    {
        string controllerPosition =
            ControllerPositionTextBox.Text.Trim();

        string positionContext =
            string.IsNullOrWhiteSpace(controllerPosition)
                ? string.Empty
                : $"Controller position: {controllerPosition}.";
        
        _activeCallsigns.Clear();
        _hasFreshSnapshot = false;

        try
        {
            if (_detector.FindRunningInstance() is null)
            {
                SnapshotStatusText.Text =
                    "eATS is not running. " +
                    "Active-aircraft context was not used.";

                return positionContext;
            }

            EatsSnapshotData snapshot =
                _snapshotService.Load();

            TimeSpan snapshotAge =
                DateTime.UtcNow -
                snapshot.LastWriteTimeUtc;

            if (snapshotAge < TimeSpan.Zero)
            {
                snapshotAge = TimeSpan.Zero;
            }

            if (snapshotAge > TimeSpan.FromMinutes(3))
            {
                SnapshotStatusText.Text =
                    $"The eATS snapshot is stale " +
                    $"({snapshotAge.TotalMinutes:F1} minutes old). " +
                    "Active-aircraft context was not used.";

                return positionContext;
            }

            IReadOnlyList<string> activeCallsigns =
                snapshot.Callsigns;

            foreach (string activeCallsign in activeCallsigns)
            {
                _activeCallsigns.Add(activeCallsign);
            }

            _hasFreshSnapshot = true;    

            string airlineContext =
                ActiveCallsignPromptBuilder.Build(
                    activeCallsigns,
                    _airlineAliases);

            if (activeCallsigns.Count == 0)
            {
                SnapshotStatusText.Text =
                    "The snapshot contained no active aircraft.";

                return positionContext;
            }

            if (string.IsNullOrWhiteSpace(airlineContext))
            {
                SnapshotStatusText.Text =
                    $"Found {activeCallsigns.Count} active aircraft, " +
                    "but none had supported airline callsigns.";

                return positionContext;
            }

            SnapshotStatusText.Text =
                $"Loaded {activeCallsigns.Count} active aircraft " +
                $"from a {snapshotAge.TotalSeconds:F0}-second-old snapshot. " +
                "Dynamic airline speech context is ready.";

            return $"{positionContext} {airlineContext}".Trim();
        }
        catch (Exception exception)
        {
            SnapshotStatusText.Text =
                "Active-aircraft speech context is unavailable.\n" +
                exception.Message;

            return positionContext;
        }
    }
    private void UpdateCommandSafetyStatus(
        string callsign)
    {
        if (!_hasFreshSnapshot)
        {
            CommandSafetyStatusText.Foreground =
                Brushes.DarkGoldenrod;

            CommandSafetyStatusText.Text =
                "Preview only: the callsign could not be " +
                "verified against a fresh eATS snapshot.";

            return;
        }

        bool isActive =
            ActiveCallsignValidator.IsActive(
                callsign,
                _activeCallsigns);

        if (isActive)
        {
            CommandSafetyStatusText.Foreground =
                Brushes.ForestGreen;

            CommandSafetyStatusText.Text =
                $"Verified: {callsign} is active in the " +
                "current eATS snapshot.";

            return;
        }

        CommandSafetyStatusText.Foreground =
            Brushes.Firebrick;

        CommandSafetyStatusText.Text =
            $"Blocked: {callsign} was not found in the " +
            "current eATS snapshot.";
    }

    private void MarkCommandNotReady(string reason)
    {
        CommandSafetyStatusText.Foreground =
            Brushes.Firebrick;

        CommandSafetyStatusText.Text =
            $"Not ready: {reason}";
    }
}