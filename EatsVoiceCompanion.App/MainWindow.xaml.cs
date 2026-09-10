using System.IO;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Input;
using System.Windows.Media;
using EatsVoiceCompanion.App.Configuration;
using EatsVoiceCompanion.App.Presentation;
using EatsVoiceCompanion.App.Services;
using EatsVoiceCompanion.Core.Commands;
using EatsVoiceCompanion.Core.Safety;
using EatsVoiceCompanion.Core.Speech;

namespace EatsVoiceCompanion.App;

public partial class MainWindow : Window
{
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

    private readonly AppLogger _logger;
    private readonly CompanionSettingsService _settingsService;
    private readonly EatsProcessDetector _detector;
    private readonly MicrophoneService _microphoneService;
    private readonly RecordingStorageService _recordingStorage;
    private readonly AudioRecorder _audioRecorder;
    private readonly SpeechRecognitionService _speechRecognitionService;
    private readonly EatsCommandStager _commandStager;
    private readonly CancellationTokenSource _lifetimeCancellation = new();

    private CompanionSettings _settings;
    private string? _startupSettingsWarning;
    private IReadOnlyDictionary<string, string> _airlineAliases =
        FallbackAirlineAliases;
    private EatsAirlineAliasService _airlineAliasService = null!;
    private EatsSnapshotService _snapshotService = null!;
    private RecognitionContextService _recognitionContextService = null!;
    private SpeechWorkflowService _speechWorkflowService = null!;
    private VoiceCommandInterpreter _voiceCommandInterpreter = null!;
    private CancellationTokenSource? _transcriptionCancellation;
    private IReadOnlySet<string> _activeCallsigns =
        new HashSet<string>(StringComparer.OrdinalIgnoreCase);
    private IReadOnlyDictionary<string, string> _activeStars =
        new Dictionary<string, string>(StringComparer.OrdinalIgnoreCase);
    private bool _hasFreshSnapshot;
    private bool _currentPreviewRequiresStar;
    private string? _currentPreviewSpokenStar;
    private bool _currentPreviewWasRecovered;
    private CommandSafetyState _currentSafetyState =
        CommandSafetyState.NotReady;
    private bool _currentPreviewWasStaged;
    private bool _isStagingCommand;

    public MainWindow()
    {
        _logger = new AppLogger();
        _settingsService = new CompanionSettingsService();
        _settings = LoadSettingsOrDefaults();
        _detector = new EatsProcessDetector();
        _microphoneService = new MicrophoneService();
        _recordingStorage = new RecordingStorageService(
            _settings.RecordingRetentionDays,
            _settings.MaximumSavedRecordings);
        _audioRecorder = new AudioRecorder(_recordingStorage);
        _speechRecognitionService = new SpeechRecognitionService(
            logger: _logger);
        _commandStager = new EatsCommandStager();

        InitializeComponent();
        ApplySettingsToUi();
        ConfigureEatsDataServices();

        _audioRecorder.RecordingCompleted +=
            AudioRecorder_RecordingCompleted;

        int deletedRecordings = _recordingStorage.Cleanup();

        LoadMicrophones();
        BuildRecognitionContext();

        _logger.Information(
            "ApplicationStarted",
            "The application initialized.",
            new { DeletedRecordings = deletedRecordings });
    }

    private CompanionSettings LoadSettingsOrDefaults()
    {
        try
        {
            return _settingsService.Load();
        }
        catch (Exception exception)
        {
            _startupSettingsWarning =
                "Saved settings could not be loaded; defaults are in use. " +
                exception.Message;

            _logger.Error(
                "SettingsLoadFailed",
                "Saved settings could not be loaded.",
                exception);

            return new CompanionSettings();
        }
    }

    private void ApplySettingsToUi()
    {
        ControllerPositionTextBox.Text = _settings.ControllerPosition;
        EatsDataDirectoryTextBox.Text = _settings.EatsDataDirectory;
        SnapshotFreshnessTextBox.Text =
            _settings.SnapshotFreshnessMinutes.ToString();
        RecordingRetentionTextBox.Text =
            _settings.RecordingRetentionDays.ToString();
        MaximumRecordingsTextBox.Text =
            _settings.MaximumSavedRecordings.ToString();

        SettingsStatusText.Text = _startupSettingsWarning ??
            $"Settings file: {_settingsService.SettingsFilePath}";

        SettingsStatusText.Foreground = _startupSettingsWarning is null
            ? Brushes.DimGray
            : Brushes.DarkGoldenrod;
    }

    private void SaveSettings_Click(
        object sender,
        RoutedEventArgs e)
    {
        if (_audioRecorder.IsRecording ||
            _transcriptionCancellation is not null)
        {
            SettingsStatusText.Foreground = Brushes.DarkGoldenrod;
            SettingsStatusText.Text =
                "Wait for recording or transcription to finish before " +
                "changing settings.";
            return;
        }

        try
        {
            CompanionSettings updated = new()
            {
                ControllerPosition = ControllerPositionTextBox.Text,
                EatsDataDirectory = EatsDataDirectoryTextBox.Text,
                SnapshotFreshnessMinutes = ParseSetting(
                    SnapshotFreshnessTextBox.Text,
                    "snapshot freshness"),
                RecordingRetentionDays = ParseSetting(
                    RecordingRetentionTextBox.Text,
                    "recording retention"),
                MaximumSavedRecordings = ParseSetting(
                    MaximumRecordingsTextBox.Text,
                    "maximum recordings"),
                PreferredMicrophoneName =
                    (MicrophoneComboBox.SelectedItem as AudioInputDevice)?.Name
            };

            _settingsService.Save(updated);
            _settings = updated;
            _recordingStorage.UpdatePolicy(
                updated.RecordingRetentionDays,
                updated.MaximumSavedRecordings);
            int deletedRecordings = _recordingStorage.Cleanup();

            ConfigureEatsDataServices();
            BuildRecognitionContext();
            RefreshExistingPreviewSafety();

            SettingsStatusText.Foreground = Brushes.ForestGreen;
            SettingsStatusText.Text =
                $"Settings saved to {_settingsService.SettingsFilePath}";

            _logger.Information(
                "SettingsSaved",
                "Application settings were saved.",
                new { DeletedRecordings = deletedRecordings });
        }
        catch (Exception exception)
        {
            SettingsStatusText.Foreground = Brushes.Firebrick;
            SettingsStatusText.Text =
                $"Settings were not saved: {exception.Message}";

            _logger.Error(
                "SettingsSaveFailed",
                "Application settings could not be saved.",
                exception);
        }
    }

    private static int ParseSetting(
        string value,
        string settingName)
    {
        if (!int.TryParse(value, out int result))
        {
            throw new ArgumentException(
                $"Enter a valid numeric {settingName}.");
        }

        return result;
    }

    private void ConfigureEatsDataServices()
    {
        string airlinePath = Path.Combine(
            _settings.EatsDataDirectory,
            "Airlines.txt");
        string snapshotPath = Path.Combine(
            _settings.EatsDataDirectory,
            "SnapshotAuto.txt");
        string logDetailPath = Path.Combine(
            _settings.EatsDataDirectory,
            "LogDetail.txt");
        string airwaysPath = Path.Combine(
            _settings.EatsDataDirectory,
            "Airways.txt");

        _airlineAliasService = new EatsAirlineAliasService(airlinePath);
        _snapshotService = new EatsSnapshotService(snapshotPath);

        try
        {
            _airlineAliases = _airlineAliasService.Load();

            AirlineDataStatusText.Text =
                $"Loaded {_airlineAliases.Count} airline callsigns from:\n" +
                _airlineAliasService.AirlineFilePath;

            _logger.Information(
                "AirlineAliasesLoaded",
                "Installed eATS airline aliases were loaded.",
                new { AliasCount = _airlineAliases.Count });
        }
        catch (Exception exception)
        {
            _airlineAliases = FallbackAirlineAliases;

            AirlineDataStatusText.Text =
                "The installed eATS airline data could not be loaded. " +
                $"Using {_airlineAliases.Count} built-in aliases.\n" +
                exception.Message;

            _logger.Error(
                "AirlineAliasesFallback",
                "Installed airline aliases could not be loaded.",
                exception);
        }

        _voiceCommandInterpreter = new VoiceCommandInterpreter(
            _airlineAliases);
        EatsRouteContextService routeContextService = new(
            logDetailPath,
            airwaysPath);
        _recognitionContextService = new RecognitionContextService(
            _detector,
            _snapshotService,
            _airlineAliases,
            TimeSpan.FromMinutes(_settings.SnapshotFreshnessMinutes),
            logger: _logger,
            routeContextService: routeContextService);
        _speechWorkflowService = new SpeechWorkflowService(
            _speechRecognitionService,
            _recognitionContextService);
    }

    private void DetectEats_Click(
        object sender,
        RoutedEventArgs e)
    {
        EatsProcessInfo? result = _detector.FindRunningInstance();

        if (result is null)
        {
            StatusText.Text =
                "eATS was not detected. Start eATS, " +
                "wait for its main window, and try again.";

            _logger.Warning(
                "EatsNotDetected",
                "No running eATS window was detected.");

            BuildRecognitionContext();
            RefreshExistingPreviewSafety();
            return;
        }

        StatusText.Text =
            $"eATS detected successfully.\n\n" +
            $"Process ID: {result.ProcessId}\n" +
            $"Window title: {result.WindowTitle}\n" +
            $"Window handle: 0x{result.MainWindowHandle:X}\n" +
            $"Version: {result.ProductVersion ?? "Unavailable"}\n" +
            $"Location: {result.ExecutablePath ?? "Unavailable"}";

        _logger.Information(
            "EatsDetected",
            "A running eATS window was detected.",
            new
            {
                result.ProcessId,
                result.ProductVersion
            });

        BuildRecognitionContext();
        RefreshExistingPreviewSafety();
    }

    private void BuildPreview_Click(
        object sender,
        RoutedEventArgs e)
    {
        BuildRecognitionContext();
        PreviewErrorText.Text = string.Empty;

        try
        {
            string commandType = GetSelectedCommandTypeTag();

            string transmission = commandType == "Combined"
                ? CommandPreviewBuilder.BuildCombined(
                    CallsignTextBox.Text,
                    CommandValueTextBox.Text)
                : CommandPreviewBuilder.Build(
                    CallsignTextBox.Text,
                    GetSelectedInstructionType(commandType),
                    CommandValueTextBox.Text);

            PreviewText.Text = transmission;
            _currentPreviewWasRecovered = false;
            ConfigureRouteRequirement(transmission, spokenStar: null);
            PrepareNewPreviewForStaging();
            string callsign = transmission.Split(' ')[0];
            UpdateCommandSafetyStatus(callsign);

            _logger.Information(
                "ManualPreviewBuilt",
                "A manual command preview was built.",
                new { InstructionType = commandType });
        }
        catch (Exception exception)
        {
            ShowCommandError(
                exception.Message,
                "the manual command is invalid.");

            _logger.Error(
                "ManualPreviewRejected",
                "A manual command preview was rejected.",
                exception);
        }
    }

    private string GetSelectedCommandTypeTag()
    {
        if (CommandTypeBox.SelectedItem is not ComboBoxItem selectedItem ||
            selectedItem.Tag is not string commandType)
        {
            throw new InvalidOperationException(
                "Select an instruction type.");
        }

        return commandType;
    }

    private static VoiceInstructionType GetSelectedInstructionType(
        string commandType)
    {
        return commandType switch
        {
            "FlyHeading" => VoiceInstructionType.FlyHeading,
            "TurnLeft" => VoiceInstructionType.TurnLeftHeading,
            "TurnRight" => VoiceInstructionType.TurnRightHeading,
            "PresentHeading" => VoiceInstructionType.FlyPresentHeading,
            "Climb" => VoiceInstructionType.ClimbAndMaintain,
            "Descend" => VoiceInstructionType.DescendAndMaintain,
            "DescendVia" => VoiceInstructionType.DescendVia,
            "DescendViaExceptMaintain" =>
                VoiceInstructionType.DescendViaExceptMaintain,
            "PilotsDiscretion" =>
                VoiceInstructionType.DescendAtPilotsDiscretion,
            "Expedite" => VoiceInstructionType.Expedite,
            "ExpediteAltitude" =>
                VoiceInstructionType.ExpediteThroughAltitude,
            "ReportLeaving" =>
                VoiceInstructionType.ReportLeavingAltitude,
            "ReportReaching" =>
                VoiceInstructionType.ReportReachingAltitude,
            "SayAltitude" => VoiceInstructionType.SayAltitude,
            "Speed" => VoiceInstructionType.MaintainSpeed,
            "SpeedGreater" => VoiceInstructionType.MaintainSpeedOrGreater,
            "SpeedLess" => VoiceInstructionType.MaintainSpeedOrLess,
            "Mach" => VoiceInstructionType.MaintainMach,
            "MachGreater" => VoiceInstructionType.MaintainMachOrGreater,
            "MachLess" => VoiceInstructionType.MaintainMachOrLess,
            "ResumeNormalSpeed" => VoiceInstructionType.ResumeNormalSpeed,
            "SayIndicatedSpeed" => VoiceInstructionType.SayIndicatedSpeed,
            "SayMach" => VoiceInstructionType.SayMach,
            "SayNormalSpeed" => VoiceInstructionType.SayNormalSpeed,
            "ExpectApproach" => VoiceInstructionType.ExpectApproach,
            "InterceptFinal" =>
                VoiceInstructionType.InterceptFinalApproachCourse,
            "ClearedApproach" => VoiceInstructionType.ClearedApproach,
            "SayApproachRequest" => VoiceInstructionType.SayApproachRequest,
            "ApproachSpeed" => VoiceInstructionType.ReduceToFinalApproachSpeed,
            "PublishedSpeed" =>
                VoiceInstructionType.ComplyWithPublishedSpeeds,
            "Direct" => VoiceInstructionType.ProceedDirect,
            "CrossAltitude" => VoiceInstructionType.CrossAtAltitude,
            "CrossAltitudeSpeed" =>
                VoiceInstructionType.CrossAtAltitudeAndSpeed,
            "Altimeter" => VoiceInstructionType.Altimeter,
            "Roger" => VoiceInstructionType.Roger,
            _ => throw new InvalidOperationException(
                "The selected instruction is not supported.")
        };
    }

    private async void StageCommand_Click(
        object sender,
        RoutedEventArgs e)
    {
        if (_isStagingCommand || _currentPreviewWasStaged)
        {
            return;
        }

        string transmission;

        try
        {
            BuildRecognitionContext();
            transmission = EatsTransmissionValidator.Validate(
                PreviewText.Text);
            string callsign = transmission.Split(' ')[0];
            UpdateCommandSafetyStatus(callsign);

            if (_currentSafetyState != CommandSafetyState.Verified)
            {
                CommandStageStatusText.Foreground = Brushes.Firebrick;
                CommandStageStatusText.Text =
                    "The command was not staged because its safety " +
                    "requirements are not freshly verified.";
                return;
            }

            string recoveryWarning = _currentPreviewWasRecovered
                ? "\n\nWARNING: Speech recognition required a " +
                  "best-effort interpretation. Compare the original " +
                  "transcript and every preview field before approving."
                : string.Empty;

            MessageBoxResult confirmation = MessageBox.Show(
                this,
                "Stage this verified command in eATS?\n\n" +
                transmission +
                recoveryWarning +
                "\n\nThe application will not press the final Enter key.",
                "Stage verified command",
                MessageBoxButton.YesNo,
                MessageBoxImage.Warning,
                MessageBoxResult.No);

            if (confirmation != MessageBoxResult.Yes)
            {
                CommandStageStatusText.Foreground = Brushes.DimGray;
                CommandStageStatusText.Text = "Command staging was canceled.";
                return;
            }

            BuildRecognitionContext();
            UpdateCommandSafetyStatus(callsign);

            if (_currentSafetyState != CommandSafetyState.Verified)
            {
                CommandStageStatusText.Foreground = Brushes.Firebrick;
                CommandStageStatusText.Text =
                    "The command was not staged because verification " +
                    "changed while confirmation was open.";
                return;
            }

            EatsProcessInfo? process = _detector.FindRunningInstance();

            if (process is null)
            {
                throw new InvalidOperationException(
                    "A running eATS window could not be found.");
            }

            _isStagingCommand = true;
            UpdateStageButtonState();
            CommandStageStatusText.Foreground = Brushes.DarkGoldenrod;
            CommandStageStatusText.Text = "Staging the command in eATS...";

            await _commandStager.StageAsync(
                process,
                transmission,
                _lifetimeCancellation.Token);

            _currentPreviewWasStaged = true;
            CommandStageStatusText.Foreground = Brushes.ForestGreen;
            CommandStageStatusText.Text =
                "Command staged in eATS. Review it there, then press " +
                "Enter yourself to transmit.";

            _logger.Information(
                "CommandStaged",
                "A verified command was staged without transmission.",
                new { process.ProcessId });
        }
        catch (OperationCanceledException)
            when (_lifetimeCancellation.IsCancellationRequested)
        {
            // Application shutdown intentionally cancels staging.
        }
        catch (Exception exception)
        {
            _currentPreviewWasStaged = true;
            CommandStageStatusText.Foreground = Brushes.Firebrick;
            CommandStageStatusText.Text =
                "Command staging did not complete: " +
                exception.Message +
                " Check and clear the eATS radio-command box before " +
                "trying again.";

            _logger.Error(
                "CommandStagingFailed",
                "A verified command could not be staged.",
                exception);
        }
        finally
        {
            _isStagingCommand = false;
            UpdateStageButtonState();
        }
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

            AudioInputDevice? preferred = devices.FirstOrDefault(
                device => string.Equals(
                    device.Name,
                    _settings.PreferredMicrophoneName,
                    StringComparison.OrdinalIgnoreCase));

            MicrophoneComboBox.SelectedItem = preferred ?? devices[0];
            MicrophoneStatusText.Text =
                $"{devices.Count} recording device(s) detected.";
        }
        catch (Exception exception)
        {
            MicrophoneStatusText.Text =
                $"Unable to enumerate microphones: {exception.Message}";

            _logger.Error(
                "MicrophoneEnumerationFailed",
                "Recording devices could not be enumerated.",
                exception);
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
            RecordingStatusText.Text = "Select a microphone first.";
            return;
        }

        try
        {
            RecordButton.CaptureMouse();

            string filePath = _audioRecorder.Start(
                microphone.DeviceNumber);

            SaveSettingsButton.IsEnabled = false;
            RecordButton.Content = "Recording — release to stop";
            RecordingStatusText.Text =
                $"Recording from {microphone.Name}\n{filePath}";

            _logger.Information(
                "RecordingStarted",
                "Audio recording started.",
                new { microphone.DeviceNumber });
        }
        catch (Exception exception)
        {
            RecordButton.ReleaseMouseCapture();
            RecordButton.Content = "Hold to record";
            SaveSettingsButton.IsEnabled = true;
            RecordingStatusText.Text =
                $"Recording could not start: {exception.Message}";

            _logger.Error(
                "RecordingStartFailed",
                "Audio recording could not start.",
                exception);
        }
    }

    private void RecordButton_MouseUp(
        object sender,
        MouseButtonEventArgs e)
    {
        e.Handled = true;
        StopActiveRecording();
    }

    private void MainWindow_Deactivated(
        object? sender,
        EventArgs e)
    {
        StopActiveRecording();
    }

    private void StopActiveRecording()
    {
        if (RecordButton.IsMouseCaptured)
        {
            RecordButton.ReleaseMouseCapture();
        }

        if (!_audioRecorder.IsRecording)
        {
            return;
        }

        RecordButton.Content = "Finishing recording...";
        RecordButton.IsEnabled = false;
        RecordingStatusText.Text = "Saving WAV file...";

        _audioRecorder.Stop();
    }

    private void CancelTranscription_Click(
        object sender,
        RoutedEventArgs e)
    {
        if (_transcriptionCancellation is null)
        {
            return;
        }

        CancelTranscriptionButton.IsEnabled = false;
        CancelTranscriptionButton.Content = "Canceling...";
        _transcriptionCancellation.Cancel();

        _logger.Information(
            "TranscriptionCancellationRequested",
            "The user requested transcription cancellation.");
    }

    private async void AudioRecorder_RecordingCompleted(
        object? sender,
        AudioRecordingCompletedEventArgs e)
    {
        if (_lifetimeCancellation.IsCancellationRequested)
        {
            return;
        }

        if (e.Error is not null)
        {
            await Dispatcher.InvokeAsync(() =>
            {
                SetTranscriptionControls(isBusy: false);
                RecordingStatusText.Text =
                    $"Recording failed: {e.Error.Message}";
                SpeechStatusText.Text =
                    "Speech recognition was not started.";
                ShowCommandError(
                    "The recording did not complete.",
                    "the recording failed.");
            });

            _logger.Error(
                "RecordingFailed",
                "Audio recording failed.",
                e.Error);
            return;
        }

        using CancellationTokenSource cancellation =
            CancellationTokenSource.CreateLinkedTokenSource(
                _lifetimeCancellation.Token);

        _transcriptionCancellation = cancellation;

        string controllerPosition = string.Empty;

        await Dispatcher.InvokeAsync(() =>
        {
            SetTranscriptionControls(isBusy: true);
            RecordingStatusText.Text =
                $"Recording saved successfully:\n{e.FilePath}";
            TranscriptText.Text = "Working...";
            SpeechStatusText.Text =
                "Preparing speech recognition...";
            controllerPosition = ControllerPositionTextBox.Text;
        });

        IProgress<string> progress = new Progress<string>(message =>
        {
            if (!cancellation.IsCancellationRequested &&
                !Dispatcher.HasShutdownStarted)
            {
                Dispatcher.BeginInvoke(
                    new Action(() => SpeechStatusText.Text = message));
            }
        });

        IProgress<RecognitionContextResult> contextProgress =
            new Progress<RecognitionContextResult>(context =>
            {
                if (!cancellation.IsCancellationRequested &&
                    !Dispatcher.HasShutdownStarted)
                {
                    Dispatcher.BeginInvoke(
                        new Action(() =>
                            ApplyRecognitionContext(context)));
                }
            });

        _logger.Information(
            "TranscriptionStarted",
            "Local speech transcription started.");

        try
        {
            SpeechWorkflowResult workflow =
                await _speechWorkflowService.RunAsync(
                    e.FilePath,
                    controllerPosition,
                    progress,
                    contextProgress,
                    cancellation.Token);

            string transcript = workflow.Transcript;

            if (_lifetimeCancellation.IsCancellationRequested)
            {
                return;
            }

            await Dispatcher.InvokeAsync(() =>
            {
                ApplyRecognitionContext(
                    workflow.ContextAfterTranscription);

                TranscriptText.Text =
                    string.IsNullOrWhiteSpace(transcript)
                        ? "No speech was recognized."
                        : transcript;

                BuildVoicePreview(
                    transcript,
                    refreshRecognitionContext: false);
            });

            _logger.Information(
                "TranscriptionCompleted",
                "Local speech transcription completed.",
                new { HasTranscript = !string.IsNullOrWhiteSpace(transcript) });
        }
        catch (OperationCanceledException)
            when (cancellation.IsCancellationRequested)
        {
            if (!_lifetimeCancellation.IsCancellationRequested)
            {
                await Dispatcher.InvokeAsync(() =>
                {
                    TranscriptText.Text = "Transcription canceled.";
                    SpeechStatusText.Text =
                        "Transcription was canceled by the user.";
                    ShowCommandError(
                        "Speech recognition was canceled.",
                        "transcription was canceled.");
                });

                _logger.Information(
                    "TranscriptionCanceled",
                    "Local speech transcription was canceled.");
            }
        }
        catch (Exception exception)
        {
            if (!_lifetimeCancellation.IsCancellationRequested)
            {
                await Dispatcher.InvokeAsync(() =>
                {
                    TranscriptText.Text = "Transcription failed.";
                    SpeechStatusText.Text = exception.Message;
                    ShowCommandError(
                        "Speech recognition did not complete.",
                        "speech recognition did not complete.");
                });
            }

            _logger.Error(
                "TranscriptionFailed",
                "Local speech transcription failed.",
                exception);
        }
        finally
        {
            if (ReferenceEquals(_transcriptionCancellation, cancellation))
            {
                _transcriptionCancellation = null;
            }

            if (!_lifetimeCancellation.IsCancellationRequested &&
                !Dispatcher.HasShutdownStarted)
            {
                await Dispatcher.InvokeAsync(() =>
                    SetTranscriptionControls(isBusy: false));
            }
        }
    }

    private void SetTranscriptionControls(bool isBusy)
    {
        RecordButton.Content = isBusy
            ? "Transcribing..."
            : "Hold to record";
        RecordButton.IsEnabled = !isBusy;
        SaveSettingsButton.IsEnabled = !isBusy;
        InterpretTranscriptButton.IsEnabled = !isBusy;
        CancelTranscriptionButton.IsEnabled = isBusy;
        CancelTranscriptionButton.Content = "Cancel transcription";
    }

    private void InterpretTranscript_Click(
        object sender,
        RoutedEventArgs e)
    {
        BuildVoicePreview(TranscriptText.Text);
    }

    private void BuildVoicePreview(
        string transcript,
        bool refreshRecognitionContext = true)
    {
        PreviewErrorText.Text = string.Empty;

        if (string.IsNullOrWhiteSpace(transcript))
        {
            SpeechStatusText.Text =
                "Transcription completed without recognizable speech.";
            ShowCommandError(
                "No speech was recognized.",
                "no speech was recognized.");
            return;
        }

        if (refreshRecognitionContext)
        {
            BuildRecognitionContext();
        }

        try
        {
            VoiceCommandInterpretation interpretation =
                _voiceCommandInterpreter.Interpret(
                    transcript,
                    ControllerPositionTextBox.Text,
                    _activeCallsigns,
                    _activeStars);
            ParsedVoiceCommand parsed = interpretation.Command;

            string transmission = EatsTransmissionValidator.Validate(
                parsed.ToEatsCommand());

            ApplyParsedCommandToEditor(parsed);
            PreviewText.Text = transmission;
            _currentPreviewWasRecovered =
                interpretation.Kind == VoiceInterpretationKind.Recovered;
            ConfigureRouteRequirement(
                transmission,
                GetSpokenStar(parsed));
            PrepareNewPreviewForStaging();
            UpdateCommandSafetyStatus(parsed.Callsign);
            if (interpretation.Kind == VoiceInterpretationKind.Strict)
            {
                SpeechStatusText.Text =
                    "Transcription completed and command preview generated.";
            }
            else
            {
                SpeechStatusText.Text =
                    "Best-effort interpretation generated. Review every " +
                    "field carefully before staging:\n" +
                    interpretation.InterpretedTranscript;

                _logger.Warning(
                    "VoiceTranscriptRecovered",
                    "A constrained best-effort voice interpretation was used.",
                    new
                    {
                        interpretation.RecoveredInstructionPhrase,
                        interpretation.StarWasCorrected
                    });
            }
        }
        catch (Exception exception)
        {
            SpeechStatusText.Text =
                "Transcription completed, but the command " +
                "could not be interpreted.";
            ShowCommandError(
                exception.Message,
                "the transcript did not produce a valid command.");

            _logger.Error(
                "VoicePreviewRejected",
                "The transcript did not produce a valid command preview.",
                exception);
        }
    }

    private void ApplyParsedCommandToEditor(
        ParsedVoiceCommand parsed)
    {
        CallsignTextBox.Text = parsed.Callsign;

        if (parsed.Instructions.Count > 1)
        {
            SelectCommandType("Combined");
            CommandValueTextBox.Text = string.Join(
                ' ',
                parsed.ToEatsInstructionTokens());
            return;
        }

        ParsedVoiceInstruction instruction = parsed.Instructions[0];

        string commandTag = instruction.InstructionType switch
        {
            VoiceInstructionType.FlyHeading => "FlyHeading",
            VoiceInstructionType.TurnLeftHeading => "TurnLeft",
            VoiceInstructionType.TurnRightHeading => "TurnRight",
            VoiceInstructionType.FlyPresentHeading => "PresentHeading",
            VoiceInstructionType.ClimbAndMaintain => "Climb",
            VoiceInstructionType.DescendAndMaintain => "Descend",
            VoiceInstructionType.DescendVia => "DescendVia",
            VoiceInstructionType.DescendViaExceptMaintain =>
                "DescendViaExceptMaintain",
            VoiceInstructionType.DescendAtPilotsDiscretion =>
                "PilotsDiscretion",
            VoiceInstructionType.Expedite => "Expedite",
            VoiceInstructionType.ExpediteThroughAltitude =>
                "ExpediteAltitude",
            VoiceInstructionType.ReportLeavingAltitude =>
                "ReportLeaving",
            VoiceInstructionType.ReportReachingAltitude =>
                "ReportReaching",
            VoiceInstructionType.SayAltitude => "SayAltitude",
            VoiceInstructionType.MaintainSpeed => "Speed",
            VoiceInstructionType.MaintainSpeedOrGreater => "SpeedGreater",
            VoiceInstructionType.MaintainSpeedOrLess => "SpeedLess",
            VoiceInstructionType.MaintainMach => "Mach",
            VoiceInstructionType.MaintainMachOrGreater => "MachGreater",
            VoiceInstructionType.MaintainMachOrLess => "MachLess",
            VoiceInstructionType.ResumeNormalSpeed => "ResumeNormalSpeed",
            VoiceInstructionType.SayIndicatedSpeed => "SayIndicatedSpeed",
            VoiceInstructionType.SayMach => "SayMach",
            VoiceInstructionType.SayNormalSpeed => "SayNormalSpeed",
            VoiceInstructionType.ExpectApproach => "ExpectApproach",
            VoiceInstructionType.InterceptFinalApproachCourse =>
                "InterceptFinal",
            VoiceInstructionType.ClearedApproach => "ClearedApproach",
            VoiceInstructionType.SayApproachRequest =>
                "SayApproachRequest",
            VoiceInstructionType.ReduceToFinalApproachSpeed =>
                "ApproachSpeed",
            VoiceInstructionType.ComplyWithPublishedSpeeds => "PublishedSpeed",
            VoiceInstructionType.ProceedDirect => "Direct",
            VoiceInstructionType.CrossAtAltitude => "CrossAltitude",
            VoiceInstructionType.CrossAtAltitudeAndSpeed =>
                "CrossAltitudeSpeed",
            VoiceInstructionType.Altimeter => "Altimeter",
            VoiceInstructionType.Roger => "Roger",
            _ => throw new InvalidOperationException(
                "The instruction type is not supported.")
        };

        SelectCommandType(commandTag);

        CommandValueTextBox.Text = instruction.InstructionType switch
        {
            VoiceInstructionType.ProceedDirect =>
                instruction.TextValue ?? string.Empty,
            VoiceInstructionType.CrossAtAltitude =>
                $"{instruction.TextValue} {instruction.NumericValue}",
            VoiceInstructionType.CrossAtAltitudeAndSpeed =>
                $"{instruction.TextValue} {instruction.NumericValue} " +
                $"{instruction.SecondaryNumericValue}",
            VoiceInstructionType.ComplyWithPublishedSpeeds =>
                instruction.TextValue ?? string.Empty,
            VoiceInstructionType.ExpectApproach =>
                instruction.TextValue ?? string.Empty,
            _ => instruction.NumericValue?.ToString() ?? string.Empty
        };
    }

    private void SelectCommandType(string commandTag)
    {
        foreach (object item in CommandTypeBox.Items)
        {
            if (item is ComboBoxItem comboBoxItem &&
                string.Equals(
                    comboBoxItem.Tag?.ToString(),
                    commandTag,
                    StringComparison.Ordinal))
            {
                CommandTypeBox.SelectedItem = comboBoxItem;
                break;
            }
        }
    }

    private string BuildRecognitionContext()
    {
        RecognitionContextResult context =
            _recognitionContextService.Build(
                ControllerPositionTextBox.Text);

        ApplyRecognitionContext(context);
        return context.Prompt;
    }

    private void ApplyRecognitionContext(
        RecognitionContextResult context)
    {
        _activeCallsigns = context.ActiveCallsigns;
        _activeStars = context.ActiveStars;
        _hasFreshSnapshot = context.HasFreshSnapshot;
        SnapshotStatusText.Text = context.StatusMessage;
    }

    private void ShowCommandError(
        string error,
        string notReadyReason)
    {
        PreviewText.Text = "Command not generated.";
        PreviewErrorText.Text = error;
        _currentPreviewWasRecovered = false;
        _currentPreviewRequiresStar = false;
        _currentPreviewSpokenStar = null;
        _currentPreviewWasStaged = false;
        CommandStageStatusText.Foreground = Brushes.DimGray;
        CommandStageStatusText.Text =
            "Only a freshly verified command can be staged.";
        ApplySafetyStatus(
            CommandSafetyEvaluator.NotReady(notReadyReason));
    }

    private void PrepareNewPreviewForStaging()
    {
        _currentPreviewWasStaged = false;
        CommandStageStatusText.Foreground = Brushes.DimGray;
        CommandStageStatusText.Text =
            "Verify the command, then choose Stage in eATS. " +
            "The final Enter key is always left to you.";
    }

    private void UpdateCommandSafetyStatus(string callsign)
    {
        CommandSafetyResult result = CommandSafetyEvaluator.Evaluate(
            callsign,
            _hasFreshSnapshot,
            _activeCallsigns);

        if (result.State == CommandSafetyState.Verified &&
            _currentPreviewRequiresStar)
        {
            result = NamedStarSafetyEvaluator.Evaluate(
                callsign,
                _currentPreviewSpokenStar,
                _activeStars);
        }

        ApplySafetyStatus(result);
    }

    private void ConfigureRouteRequirement(
        string transmission,
        string? spokenStar)
    {
        string[] tokens = transmission.Split(
            ' ',
            StringSplitOptions.RemoveEmptyEntries);

        _currentPreviewRequiresStar = tokens
            .Skip(1)
            .Any(token =>
                token == "DV" ||
                token.StartsWith("DVXM", StringComparison.Ordinal));
        _currentPreviewSpokenStar = _currentPreviewRequiresStar
            ? spokenStar
            : null;
    }

    private static string? GetSpokenStar(ParsedVoiceCommand parsed)
    {
        string[] stars = parsed.Instructions
            .Where(instruction =>
                instruction.InstructionType is
                    VoiceInstructionType.DescendVia or
                    VoiceInstructionType.DescendViaExceptMaintain)
            .Select(instruction => instruction.TextValue)
            .Where(value => !string.IsNullOrWhiteSpace(value))
            .Select(value => value!)
            .Distinct(StringComparer.OrdinalIgnoreCase)
            .ToArray();

        return stars.Length switch
        {
            0 => null,
            1 => stars[0],
            _ => throw new InvalidOperationException(
                "A command cannot reference more than one STAR.")
        };
    }

    private void RefreshExistingPreviewSafety()
    {
        try
        {
            string transmission = EatsTransmissionValidator.Validate(
                PreviewText.Text);
            string callsign = transmission.Split(' ')[0];

            UpdateCommandSafetyStatus(callsign);
        }
        catch (ArgumentException)
        {
            ApplySafetyStatus(
                CommandSafetyEvaluator.NotReady(
                    "build a command preview."));
        }
    }

    private void ApplySafetyStatus(CommandSafetyResult result)
    {
        _currentSafetyState = result.State;

        CommandSafetyPresentation presentation =
            CommandSafetyPresenter.Create(result);

        CommandSafetyStatusText.Foreground = presentation.Foreground;
        CommandSafetyStatusText.Text = presentation.Message;
        UpdateStageButtonState();
    }

    private void UpdateStageButtonState()
    {
        StageCommandButton.IsEnabled =
            !_isStagingCommand &&
            !_currentPreviewWasStaged &&
            _currentSafetyState == CommandSafetyState.Verified;
    }

    protected override void OnClosed(EventArgs e)
    {
        _lifetimeCancellation.Cancel();
        _transcriptionCancellation?.Cancel();

        _audioRecorder.RecordingCompleted -=
            AudioRecorder_RecordingCompleted;
        _audioRecorder.Dispose();

        _logger.Information(
            "ApplicationClosed",
            "The application closed.");

        base.OnClosed(e);
    }
}
