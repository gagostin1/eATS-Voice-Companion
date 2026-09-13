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
using Microsoft.Win32;

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
    private readonly CorrectionHistoryService _correctionHistoryService;
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
    private IReadOnlyDictionary<string, IReadOnlySet<string>>
        _activeRouteFixes =
            new Dictionary<string, IReadOnlySet<string>>(
                StringComparer.OrdinalIgnoreCase);
    private bool _hasFreshSnapshot;
    private bool _currentPreviewRequiresStar;
    private string? _currentPreviewSpokenStar;
    private bool _currentPreviewWasRecovered;
    private CommandSafetyState _currentSafetyState =
        CommandSafetyState.NotReady;
    private bool _currentPreviewWasStaged;
    private bool _isStagingCommand;
    private GlobalPushToTalkHotkeyService? _pushToTalkHotkeyService;
    private PushToTalkHotkeyDefinition? _pendingPushToTalkHotkey;
    private bool _isCapturingPushToTalkHotkey;
    private Key? _modifierHotkeyCandidate;
    private bool _recordingStartedByHotkey;
    private IReadOnlyList<CorrectionHistoryEntry> _correctionHistory = [];
    private Guid? _currentVoiceHistoryEntryId;

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
        _correctionHistoryService = new CorrectionHistoryService();

        InitializeComponent();
        FitWindowToWorkArea();
        ApplySettingsToUi();
        Loaded += MainWindow_Loaded;
        InitializePushToTalkHotkey();
        ConfigureEatsDataServices();

        _audioRecorder.RecordingCompleted +=
            AudioRecorder_RecordingCompleted;

        int deletedRecordings = _recordingStorage.Cleanup();

        LoadMicrophones();
        BuildRecognitionContext();
        RefreshCorrectionHistory();

        _logger.Information(
            "ApplicationStarted",
            "The application initialized.",
            new { DeletedRecordings = deletedRecordings });
    }

    private void FitWindowToWorkArea()
    {
        Rect workArea = SystemParameters.WorkArea;

        MaxWidth = workArea.Width;
        MaxHeight = workArea.Height;
        MinHeight = Math.Min(830, workArea.Height * 0.90);
        Width = Math.Min(920, workArea.Width * 0.92);
        Height = Math.Min(830, workArea.Height * 0.96);
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
        AutoStageCommandsCheckBox.IsChecked =
            _settings.AutomaticallyStageVerifiedCommands;
        RecognitionImprovementCheckBox.IsChecked =
            _settings.ParticipateInRecognitionImprovement;
        PushToTalkHotkeyDefinition.TryParse(
            _settings.PushToTalkHotkey,
            out _pendingPushToTalkHotkey);
        UpdatePushToTalkHotkeyUi();

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
            _transcriptionCancellation is not null ||
            _isStagingCommand)
        {
            SettingsStatusText.Foreground = Brushes.DarkGoldenrod;
            SettingsStatusText.Text =
                "Wait for recording, transcription, or command staging " +
                "to finish before changing settings.";
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
                    (MicrophoneComboBox.SelectedItem as AudioInputDevice)?.Name,
                AutomaticallyStageVerifiedCommands =
                    AutoStageCommandsCheckBox.IsChecked == true,
                ParticipateInRecognitionImprovement =
                    RecognitionImprovementCheckBox.IsChecked == true,
                ContributionNoticeShown =
                    _settings.ContributionNoticeShown,
                PushToTalkHotkey = _pendingPushToTalkHotkey?.ToString()
            };

            _settingsService.Save(updated);
            _settings = updated;
            ShowCorrectionHistoryEntry(
                CorrectionHistoryListBox.SelectedItem as
                    CorrectionHistoryEntry);
            if (_pushToTalkHotkeyService is not null)
            {
                _pushToTalkHotkeyService.Hotkey =
                    _pendingPushToTalkHotkey;
            }
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

    private void MainWindow_Loaded(
        object sender,
        RoutedEventArgs e)
    {
        Loaded -= MainWindow_Loaded;

        if (_settings.ContributionNoticeShown)
        {
            return;
        }

        ContributionNoticeWindow notice = new(
            _settings.ParticipateInRecognitionImprovement)
        {
            Owner = this
        };

        _ = notice.ShowDialog();

        _settings.ParticipateInRecognitionImprovement =
            notice.ParticipationEnabled;
        _settings.ContributionNoticeShown = true;
        RecognitionImprovementCheckBox.IsChecked =
            notice.ParticipationEnabled;

        try
        {
            _settingsService.Save(_settings);
        }
        catch (Exception exception)
        {
            SettingsStatusText.Foreground = Brushes.DarkGoldenrod;
            SettingsStatusText.Text =
                "Your contribution preference could not be saved. " +
                "Choose Save all settings to try again.";

            _logger.Error(
                "ContributionPreferenceSaveFailed",
                "The first-run contribution preference could not be saved.",
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
        if (_isStagingCommand)
        {
            CommandStageStatusText.Foreground = Brushes.DarkGoldenrod;
            CommandStageStatusText.Text =
                "Wait for the current command to finish staging in eATS.";
            return;
        }

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

            _ = TryAutoStageCurrentCommandAsync("manual generation");

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
            "ContactFrequency" => VoiceInstructionType.ContactFrequency,
            "RemainFrequency" => VoiceInstructionType.RemainThisFrequency,
            "SayAgain" => VoiceInstructionType.SayAgain,
            "StandBy" => VoiceInstructionType.StandBy,
            "SquawkCode" => VoiceInstructionType.SquawkCode,
            "SquawkIdent" => VoiceInstructionType.SquawkIdent,
            "SquawkAltitude" => VoiceInstructionType.SquawkAltitude,
            "SquawkNormal" => VoiceInstructionType.SquawkNormal,
            "SquawkStandby" => VoiceInstructionType.SquawkStandby,
            "SquawkVfr" => VoiceInstructionType.SquawkVfr,
            "StopAltitudeSquawk" => VoiceInstructionType.StopAltitudeSquawk,
            "PublishedSpeed" =>
                VoiceInstructionType.ComplyWithPublishedSpeeds,
            "Direct" => VoiceInstructionType.ProceedDirect,
            "CrossAltitude" => VoiceInstructionType.CrossAtAltitude,
            "CrossAltitudeSpeed" =>
                VoiceInstructionType.CrossAtAltitudeAndSpeed,
            "CrossDistanceAltitude" =>
                VoiceInstructionType.CrossDistanceAtAltitude,
            "Altimeter" => VoiceInstructionType.Altimeter,
            "Roger" => VoiceInstructionType.Roger,
            _ => throw new InvalidOperationException(
                "The selected instruction is not supported.")
        };
    }

    private async Task TryAutoStageCurrentCommandAsync(string source)
    {
        if (!_settings.AutomaticallyStageVerifiedCommands)
        {
            CommandStageStatusText.Foreground = Brushes.DimGray;
            CommandStageStatusText.Text =
                "Automatic staging is off in Settings. The command was " +
                "generated only.";
            return;
        }

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
            bool forceBestEffortVoiceStage = string.Equals(
                source,
                "voice recognition",
                StringComparison.Ordinal);

            if (_currentSafetyState != CommandSafetyState.Verified &&
                !forceBestEffortVoiceStage)
            {
                CommandStageStatusText.Foreground = Brushes.Firebrick;
                CommandStageStatusText.Text =
                    "The command was not staged because its safety " +
                    "requirements are not freshly verified.";
                return;
            }

            EatsProcessInfo? process = _detector.FindRunningInstance();

            if (process is null)
            {
                throw new InvalidOperationException(
                    "A running eATS window could not be found.");
            }

            _isStagingCommand = true;
            RecordButton.IsEnabled = false;
            InterpretTranscriptButton.IsEnabled = false;
            SaveSettingsButton.IsEnabled = false;
            GenerateCommandButton.IsEnabled = false;
            CommandStageStatusText.Foreground = Brushes.DarkGoldenrod;
            CommandStageStatusText.Text = forceBestEffortVoiceStage &&
                _currentSafetyState != CommandSafetyState.Verified
                ? "Best-effort voice command ready. Staging it in eATS..."
                : "Verified command ready. Staging it in eATS...";

            await _commandStager.StageAsync(
                process,
                transmission,
                _lifetimeCancellation.Token);

            _currentPreviewWasStaged = true;
            CommandStageStatusText.Foreground = Brushes.ForestGreen;
            CommandStageStatusText.Text =
                (_currentPreviewWasRecovered
                    ? "Best-effort command staged. Review in eATS, then "
                    : "Command staged. Review in eATS, then ") +
                "press Enter to transmit.";

            _logger.Information(
                "CommandStaged",
                "A command was staged without transmission.",
                new { process.ProcessId, Source = source });
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
                "A command could not be staged.",
                exception);
        }
        finally
        {
            _isStagingCommand = false;

            if (_transcriptionCancellation is null &&
                !_audioRecorder.IsRecording)
            {
                RecordButton.IsEnabled = true;
                InterpretTranscriptButton.IsEnabled = true;
                SaveSettingsButton.IsEnabled = true;
                GenerateCommandButton.IsEnabled = true;
            }
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

        StartActiveRecording(captureMouse: true);
    }

    private void StartActiveRecording(bool captureMouse)
    {
        if (_audioRecorder.IsRecording ||
            _transcriptionCancellation is not null ||
            !RecordButton.IsEnabled)
        {
            return;
        }

        if (_isStagingCommand)
        {
            RecordingStatusText.Text =
                "Wait for the current command to finish staging in eATS.";
            return;
        }

        if (MicrophoneComboBox.SelectedItem
            is not AudioInputDevice microphone)
        {
            RecordingStatusText.Text = "Select a microphone first.";
            return;
        }

        try
        {
            if (captureMouse)
            {
                RecordButton.CaptureMouse();
            }

            string filePath = _audioRecorder.Start(
                microphone.DeviceNumber);

            _recordingStartedByHotkey = !captureMouse;
            _currentVoiceHistoryEntryId = null;

            SaveSettingsButton.IsEnabled = false;
            InterpretTranscriptButton.IsEnabled = false;
            GenerateCommandButton.IsEnabled = false;
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
            if (RecordButton.IsMouseCaptured)
            {
                RecordButton.ReleaseMouseCapture();
            }

            _recordingStartedByHotkey = false;
            RecordButton.Content = "Hold to record";
            SaveSettingsButton.IsEnabled = true;
            InterpretTranscriptButton.IsEnabled = true;
            GenerateCommandButton.IsEnabled = true;
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
        if (!_recordingStartedByHotkey)
        {
            StopActiveRecording();
        }
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
        _recordingStartedByHotkey = false;
    }

    private void InitializePushToTalkHotkey()
    {
        try
        {
            _pushToTalkHotkeyService =
                new GlobalPushToTalkHotkeyService
                {
                    Hotkey = _pendingPushToTalkHotkey
                };
            _pushToTalkHotkeyService.Pressed +=
                PushToTalkHotkeyService_Pressed;
            _pushToTalkHotkeyService.Released +=
                PushToTalkHotkeyService_Released;
        }
        catch (Exception exception)
        {
            HotkeyStatusText.Foreground = Brushes.Firebrick;
            HotkeyStatusText.Text =
                $"Global hotkeys are unavailable: {exception.Message}";
            AssignHotkeyButton.IsEnabled = false;

            _logger.Error(
                "PushToTalkHotkeyUnavailable",
                "The global push-to-talk keyboard listener could not start.",
                exception);
        }
    }

    private void PushToTalkHotkeyService_Pressed(
        object? sender,
        EventArgs e)
    {
        _ = Dispatcher.BeginInvoke(() =>
        {
            if (!_isCapturingPushToTalkHotkey)
            {
                StartActiveRecording(captureMouse: false);
            }
        });
    }

    private void PushToTalkHotkeyService_Released(
        object? sender,
        EventArgs e)
    {
        _ = Dispatcher.BeginInvoke(StopActiveRecording);
    }

    private void AssignHotkey_Click(
        object sender,
        RoutedEventArgs e)
    {
        _isCapturingPushToTalkHotkey = true;
        _modifierHotkeyCandidate = null;
        if (_pushToTalkHotkeyService is not null)
        {
            _pushToTalkHotkeyService.Hotkey = null;
        }

        AssignHotkeyButton.Content = "Press shortcut...";
        HotkeyStatusText.Foreground = Brushes.DarkGoldenrod;
        HotkeyStatusText.Text =
            "Press the desired key combination. Escape cancels.";
        Keyboard.Focus(AssignHotkeyButton);
    }

    private void ClearHotkey_Click(
        object sender,
        RoutedEventArgs e)
    {
        _isCapturingPushToTalkHotkey = false;
        _modifierHotkeyCandidate = null;
        _pendingPushToTalkHotkey = null;
        UpdatePushToTalkHotkeyUi();
        HotkeyStatusText.Text =
            "Hotkey cleared. Choose Save settings to keep the change.";
    }

    private void MainWindow_PreviewKeyDown(
        object sender,
        KeyEventArgs e)
    {
        if (!_isCapturingPushToTalkHotkey)
        {
            return;
        }

        e.Handled = true;
        Key key = e.Key == Key.System ? e.SystemKey : e.Key;

        if (key == Key.Escape)
        {
            _isCapturingPushToTalkHotkey = false;
            _modifierHotkeyCandidate = null;
            UpdatePushToTalkHotkeyUi();
            HotkeyStatusText.Text = "Hotkey assignment canceled.";
            return;
        }

        if (key is Key.Back or Key.Delete)
        {
            ClearHotkey_Click(this, new RoutedEventArgs());
            return;
        }

        if (PushToTalkHotkeyDefinition.IsModifierKey(key))
        {
            _modifierHotkeyCandidate ??= key;
            HotkeyStatusText.Text =
                "Release the modifier to assign it alone, or keep holding " +
                "it and press another key for a combination.";
            return;
        }

        try
        {
            _pendingPushToTalkHotkey =
                PushToTalkHotkeyDefinition.Create(
                    key,
                    Keyboard.Modifiers);
            _isCapturingPushToTalkHotkey = false;
            _modifierHotkeyCandidate = null;
            UpdatePushToTalkHotkeyUi();
            HotkeyStatusText.Foreground = Brushes.ForestGreen;
            HotkeyStatusText.Text =
                "Shortcut active. Choose Save settings to keep it.";
        }
        catch (ArgumentException exception)
        {
            HotkeyStatusText.Foreground = Brushes.Firebrick;
            HotkeyStatusText.Text = exception.Message;
        }
    }

    private void MainWindow_PreviewKeyUp(
        object sender,
        KeyEventArgs e)
    {
        if (!_isCapturingPushToTalkHotkey ||
            _modifierHotkeyCandidate is not { } candidate)
        {
            return;
        }

        Key key = e.Key == Key.System ? e.SystemKey : e.Key;

        if (key != candidate)
        {
            return;
        }

        e.Handled = true;
        _pendingPushToTalkHotkey =
            PushToTalkHotkeyDefinition.Create(
                candidate,
                ModifierKeys.None);
        _isCapturingPushToTalkHotkey = false;
        _modifierHotkeyCandidate = null;
        UpdatePushToTalkHotkeyUi();
        HotkeyStatusText.Foreground = Brushes.ForestGreen;
        HotkeyStatusText.Text =
            "Modifier shortcut active. Choose Save settings to keep it.";
    }

    private void UpdatePushToTalkHotkeyUi()
    {
        string display = _pendingPushToTalkHotkey?.ToString() ??
            "Not assigned";
        PushToTalkHotkeyTextBox.Text = display;
        if (_pushToTalkHotkeyService is not null)
        {
            _pushToTalkHotkeyService.Hotkey =
                _pendingPushToTalkHotkey;
        }

        VoiceHotkeyHintText.Text = _pendingPushToTalkHotkey is null
            ? "No push-to-talk hotkey assigned."
            : $"Hold {_pendingPushToTalkHotkey} to record from anywhere.";
        AssignHotkeyButton.Content = "Assign";
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
            RecordingStatusText.Text = "Recording saved successfully.";
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
                SaveVoiceAttemptToHistory(
                    e.FilePath,
                    transcript,
                    workflow.ContextAfterTranscription);
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
        RecordButton.IsEnabled = !isBusy && !_isStagingCommand;
        SaveSettingsButton.IsEnabled = !isBusy && !_isStagingCommand;
        InterpretTranscriptButton.IsEnabled = !isBusy && !_isStagingCommand;
        GenerateCommandButton.IsEnabled = !isBusy && !_isStagingCommand;
        ReplayHistoryButton.IsEnabled =
            !isBusy &&
            !_isStagingCommand &&
            CorrectionHistoryListBox.SelectedItem is not null;
        MarkHistoryCorrectButton.IsEnabled =
            !isBusy && CorrectionHistoryListBox.SelectedItem is not null;
        SaveHistoryCorrectionButton.IsEnabled =
            !isBusy && CorrectionHistoryListBox.SelectedItem is not null;
        ExportHistoryButton.IsEnabled =
            !isBusy &&
            _settings.ParticipateInRecognitionImprovement &&
            CorrectionHistoryListBox.SelectedItem is not null;
        CancelTranscriptionButton.IsEnabled = isBusy;
        CancelTranscriptionButton.Content = "Cancel transcription";
    }

    private void InterpretTranscript_Click(
        object sender,
        RoutedEventArgs e)
    {
        if (_isStagingCommand)
        {
            SpeechStatusText.Text =
                "Wait for the current command to finish staging in eATS.";
            return;
        }

        string correctedTranscript = TranscriptText.Text;
        BuildVoicePreview(correctedTranscript);
        SaveCurrentVoiceCorrection(correctedTranscript);
    }

    private void SaveVoiceAttemptToHistory(
        string recordingFilePath,
        string transcript,
        RecognitionContextResult context)
    {
        try
        {
            CorrectionHistoryEntry entry = new()
            {
                RecordingFilePath = recordingFilePath,
                OriginalTranscript = transcript,
                GeneratedCommand = TryGetValidatedPreviewCommand(),
                WasBestEffort = _currentPreviewWasRecovered,
                ControllerPosition = ControllerPositionTextBox.Text.Trim(),
                RecognitionPrompt = context.Prompt,
                ActiveCallsigns = context.ActiveCallsigns
                    .OrderBy(value => value, StringComparer.OrdinalIgnoreCase)
                    .ToArray(),
                ActiveStars = context.ActiveStars.ToDictionary(
                    pair => pair.Key,
                    pair => pair.Value,
                    StringComparer.OrdinalIgnoreCase),
                ActiveRouteFixes = context.ActiveRouteFixes.ToDictionary(
                    pair => pair.Key,
                    pair => pair.Value
                        .OrderBy(
                            value => value,
                            StringComparer.OrdinalIgnoreCase)
                        .ToArray(),
                    StringComparer.OrdinalIgnoreCase)
            };

            _correctionHistoryService.Save(entry);
            _currentVoiceHistoryEntryId = entry.Id;
            RefreshCorrectionHistory(entry.Id);
        }
        catch (Exception exception)
        {
            _logger.Error(
                "CorrectionHistorySaveFailed",
                "The completed voice attempt could not be added to history.",
                exception);
        }
    }

    private void SaveCurrentVoiceCorrection(string correctedTranscript)
    {
        if (_currentVoiceHistoryEntryId is not { } entryId)
        {
            return;
        }

        CorrectionHistoryEntry? entry = _correctionHistory.FirstOrDefault(
            item => item.Id == entryId);

        if (entry is null ||
            string.Equals(
                correctedTranscript.Trim(),
                entry.OriginalTranscript.Trim(),
                StringComparison.Ordinal))
        {
            return;
        }

        entry.CorrectedTranscript = correctedTranscript.Trim();
        string? command = TryGetValidatedPreviewCommand();

        if (command is not null)
        {
            entry.ExpectedCommand = command;
            entry.ReviewStatus = CorrectionReviewStatus.Corrected;
        }

        try
        {
            _correctionHistoryService.Save(entry);
            RefreshCorrectionHistory(entry.Id);
        }
        catch (Exception exception)
        {
            _logger.Error(
                "CorrectionHistoryUpdateFailed",
                "An interpreted transcript correction could not be saved.",
                exception);
        }
    }

    private string? TryGetValidatedPreviewCommand()
    {
        try
        {
            return EatsTransmissionValidator.Validate(PreviewText.Text);
        }
        catch (ArgumentException)
        {
            return null;
        }
    }

    private void RefreshHistory_Click(
        object sender,
        RoutedEventArgs e) => RefreshCorrectionHistory();

    private void RefreshCorrectionHistory(Guid? selectedId = null)
    {
        try
        {
            selectedId ??=
                (CorrectionHistoryListBox.SelectedItem as
                    CorrectionHistoryEntry)?.Id;
            _correctionHistory = _correctionHistoryService.Load();
            CorrectionHistoryListBox.ItemsSource = _correctionHistory;
            HistoryCountText.Foreground = Brushes.DimGray;
            HistoryCountText.Text = _correctionHistory.Count == 0
                ? "No saved attempts yet."
                : $"{_correctionHistory.Count} local attempt(s). Audio " +
                  "availability follows recording-retention settings.";

            CorrectionHistoryEntry? selected = selectedId is null
                ? _correctionHistory.FirstOrDefault()
                : _correctionHistory.FirstOrDefault(
                    entry => entry.Id == selectedId);
            CorrectionHistoryListBox.SelectedItem = selected;
            ShowCorrectionHistoryEntry(selected);
        }
        catch (Exception exception)
        {
            HistoryCountText.Foreground = Brushes.Firebrick;
            HistoryCountText.Text =
                $"Correction history could not be loaded: {exception.Message}";
            _logger.Error(
                "CorrectionHistoryLoadFailed",
                "Local correction history could not be loaded.",
                exception);
        }
    }

    private void CorrectionHistoryList_SelectionChanged(
        object sender,
        SelectionChangedEventArgs e)
    {
        ShowCorrectionHistoryEntry(
            CorrectionHistoryListBox.SelectedItem as
                CorrectionHistoryEntry);
    }

    private void ShowCorrectionHistoryEntry(CorrectionHistoryEntry? entry)
    {
        bool hasEntry = entry is not null;
        MarkHistoryCorrectButton.IsEnabled = hasEntry;
        SaveHistoryCorrectionButton.IsEnabled = hasEntry;
        ExportHistoryButton.IsEnabled =
            hasEntry && _settings.ParticipateInRecognitionImprovement;
        ReplayHistoryButton.IsEnabled =
            hasEntry &&
            _transcriptionCancellation is null &&
            !_audioRecorder.IsRecording &&
            !_isStagingCommand;

        if (entry is null)
        {
            HistoryDetailStatusText.Text =
                "Select an attempt from the history.";
            HistoryOriginalTranscriptTextBox.Text = string.Empty;
            HistoryOriginalCommandTextBox.Text = string.Empty;
            HistoryCorrectedTranscriptTextBox.Text = string.Empty;
            HistoryExpectedCommandTextBox.Text = string.Empty;
            HistoryReplayResultText.Text =
                "This attempt has not been replayed.";
            HistoryReplayResultText.Foreground = Brushes.DimGray;
            return;
        }

        bool hasAudio = File.Exists(entry.RecordingFilePath);
        HistoryDetailStatusText.Foreground = Brushes.DimGray;
        HistoryDetailStatusText.Text =
            $"{entry.RecordedAtUtc.ToLocalTime():F} · " +
            $"{entry.ReviewStatus} · " +
            (hasAudio ? "Audio available" : "Audio expired or missing");
        HistoryOriginalTranscriptTextBox.Text = entry.OriginalTranscript;
        HistoryOriginalCommandTextBox.Text =
            entry.GeneratedCommand ?? "No valid command was generated.";
        HistoryCorrectedTranscriptTextBox.Text =
            entry.CorrectedTranscript ?? entry.OriginalTranscript;
        HistoryExpectedCommandTextBox.Text =
            entry.ExpectedCommand ?? entry.GeneratedCommand ?? string.Empty;

        if (entry.LastReplayedAtUtc is null)
        {
            HistoryReplayResultText.Text =
                "This attempt has not been replayed.";
            HistoryReplayResultText.Foreground = Brushes.DimGray;
            return;
        }

        string replayResult = entry.LatestReplayError is not null
            ? $"Replay failed: {entry.LatestReplayError}"
            : $"Transcript: {entry.LatestReplayTranscript}\n" +
              $"Command: {entry.LatestReplayCommand ?? "No command"}";
        string? expected = entry.ExpectedCommand ?? entry.GeneratedCommand;

        if (entry.LatestReplayError is null && expected is not null)
        {
            bool matches = string.Equals(
                expected,
                entry.LatestReplayCommand,
                StringComparison.Ordinal);
            replayResult += matches
                ? "\nPASS: replay matches the expected command."
                : $"\nDIFFERS: expected {expected}.";
            HistoryReplayResultText.Foreground = matches
                ? Brushes.ForestGreen
                : Brushes.DarkGoldenrod;
        }
        else
        {
            HistoryReplayResultText.Foreground =
                entry.LatestReplayError is null
                    ? Brushes.DimGray
                    : Brushes.Firebrick;
        }

        HistoryReplayResultText.Text = replayResult;
    }

    private void MarkHistoryCorrect_Click(
        object sender,
        RoutedEventArgs e)
    {
        if (CorrectionHistoryListBox.SelectedItem is not
            CorrectionHistoryEntry entry)
        {
            return;
        }

        if (entry.GeneratedCommand is null)
        {
            HistoryDetailStatusText.Foreground = Brushes.Firebrick;
            HistoryDetailStatusText.Text =
                "This attempt did not generate a command. Enter the " +
                "expected command and save it as a correction instead.";
            return;
        }

        entry.CorrectedTranscript = entry.OriginalTranscript;
        entry.ExpectedCommand = entry.GeneratedCommand;
        entry.ReviewStatus = CorrectionReviewStatus.Correct;
        SaveReviewedHistoryEntry(entry, "Marked correct.");
    }

    private void SaveHistoryCorrection_Click(
        object sender,
        RoutedEventArgs e)
    {
        if (CorrectionHistoryListBox.SelectedItem is not
            CorrectionHistoryEntry entry)
        {
            return;
        }

        try
        {
            string transcript =
                HistoryCorrectedTranscriptTextBox.Text.Trim();
            string command = EatsTransmissionValidator.Validate(
                HistoryExpectedCommandTextBox.Text);

            if (string.IsNullOrWhiteSpace(transcript))
            {
                throw new ArgumentException(
                    "Enter the corrected transcript.");
            }

            entry.CorrectedTranscript = transcript;
            entry.ExpectedCommand = command;
            entry.ReviewStatus = CorrectionReviewStatus.Corrected;
            SaveReviewedHistoryEntry(entry, "Correction saved locally.");
        }
        catch (Exception exception)
        {
            HistoryDetailStatusText.Foreground = Brushes.Firebrick;
            HistoryDetailStatusText.Text = exception.Message;
        }
    }

    private void SaveReviewedHistoryEntry(
        CorrectionHistoryEntry entry,
        string confirmation)
    {
        try
        {
            _correctionHistoryService.Save(entry);
            RefreshCorrectionHistory(entry.Id);
            HistoryDetailStatusText.Foreground = Brushes.ForestGreen;
            HistoryDetailStatusText.Text = confirmation;
        }
        catch (Exception exception)
        {
            HistoryDetailStatusText.Foreground = Brushes.Firebrick;
            HistoryDetailStatusText.Text =
                $"The review could not be saved: {exception.Message}";
            _logger.Error(
                "CorrectionReviewSaveFailed",
                "A correction-history review could not be saved.",
                exception);
        }
    }

    private async void ReplayHistory_Click(
        object sender,
        RoutedEventArgs e)
    {
        if (CorrectionHistoryListBox.SelectedItem is not
                CorrectionHistoryEntry entry ||
            _transcriptionCancellation is not null ||
            _audioRecorder.IsRecording ||
            _isStagingCommand)
        {
            return;
        }

        if (!File.Exists(entry.RecordingFilePath))
        {
            HistoryReplayResultText.Foreground = Brushes.Firebrick;
            HistoryReplayResultText.Text =
                "The retained WAV file has expired or is missing. The " +
                "saved text case is still available for regression tests.";
            return;
        }

        using CancellationTokenSource cancellation =
            CancellationTokenSource.CreateLinkedTokenSource(
                _lifetimeCancellation.Token);
        _transcriptionCancellation = cancellation;
        SetTranscriptionControls(isBusy: true);
        HistoryReplayResultText.Foreground = Brushes.DarkGoldenrod;
        HistoryReplayResultText.Text =
            "Replaying the saved WAV through the current recognizer...";

        IProgress<string> progress = new Progress<string>(message =>
        {
            if (!cancellation.IsCancellationRequested)
            {
                HistoryReplayResultText.Text = message;
            }
        });

        try
        {
            RefreshHistoryContextForReplay(entry);

            string transcript =
                await _speechRecognitionService.TranscribeAsync(
                    entry.RecordingFilePath,
                    progress,
                    entry.RecognitionPrompt,
                    cancellation.Token);

            IReadOnlySet<string> callsigns = new HashSet<string>(
                entry.ActiveCallsigns,
                StringComparer.OrdinalIgnoreCase);
            IReadOnlyDictionary<string, IReadOnlySet<string>> routeFixes =
                entry.ActiveRouteFixes.ToDictionary(
                    pair => pair.Key,
                    pair => (IReadOnlySet<string>)new HashSet<string>(
                        pair.Value,
                        StringComparer.OrdinalIgnoreCase),
                    StringComparer.OrdinalIgnoreCase);
            VoiceCommandInterpretation interpretation =
                _voiceCommandInterpreter.Interpret(
                    transcript,
                    entry.ControllerPosition,
                    callsigns,
                    entry.ActiveStars,
                    routeFixes);

            entry.LastReplayedAtUtc = DateTime.UtcNow;
            entry.LatestReplayTranscript = transcript;
            entry.LatestReplayCommand = EatsTransmissionValidator.Validate(
                interpretation.Command.ToEatsCommand());
            entry.LatestReplayError = null;
        }
        catch (OperationCanceledException)
            when (cancellation.IsCancellationRequested)
        {
            entry.LastReplayedAtUtc = DateTime.UtcNow;
            entry.LatestReplayError = "Replay was canceled.";
        }
        catch (Exception exception)
        {
            entry.LastReplayedAtUtc = DateTime.UtcNow;
            entry.LatestReplayError = exception.Message;
            _logger.Error(
                "CorrectionHistoryReplayFailed",
                "A saved voice attempt could not be replayed.",
                exception);
        }
        finally
        {
            if (ReferenceEquals(_transcriptionCancellation, cancellation))
            {
                _transcriptionCancellation = null;
            }

            try
            {
                _correctionHistoryService.Save(entry);
            }
            catch (Exception exception)
            {
                _logger.Error(
                    "CorrectionHistoryReplaySaveFailed",
                    "The replay result could not be saved.",
                    exception);
            }

            SetTranscriptionControls(isBusy: false);
            RefreshCorrectionHistory(entry.Id);
        }
    }

    private void RefreshHistoryContextForReplay(
        CorrectionHistoryEntry entry)
    {
        string? expected = entry.ExpectedCommand ?? entry.GeneratedCommand;
        string? callsign = expected?
            .Split(' ', StringSplitOptions.RemoveEmptyEntries)
            .FirstOrDefault();

        if (callsign is null)
        {
            return;
        }

        RecognitionContextResult current =
            _recognitionContextService.Build(entry.ControllerPosition);

        if (!current.ActiveCallsigns.Contains(callsign))
        {
            return;
        }

        entry.RecognitionPrompt = current.Prompt;
        entry.ActiveCallsigns = current.ActiveCallsigns
            .OrderBy(value => value, StringComparer.OrdinalIgnoreCase)
            .ToArray();
        entry.ActiveStars = current.ActiveStars.ToDictionary(
            pair => pair.Key,
            pair => pair.Value,
            StringComparer.OrdinalIgnoreCase);
        entry.ActiveRouteFixes = current.ActiveRouteFixes.ToDictionary(
            pair => pair.Key,
            pair => pair.Value
                .OrderBy(value => value, StringComparer.OrdinalIgnoreCase)
                .ToArray(),
            StringComparer.OrdinalIgnoreCase);
    }

    private void ExportHistory_Click(
        object sender,
        RoutedEventArgs e)
    {
        if (CorrectionHistoryListBox.SelectedItem is not
            CorrectionHistoryEntry entry)
        {
            return;
        }

        if (!_settings.ParticipateInRecognitionImprovement)
        {
            HistoryDetailStatusText.Foreground = Brushes.DarkGoldenrod;
            HistoryDetailStatusText.Text =
                "Enable recognition-improvement participation in Settings " +
                "before exporting a contribution-ready case.";
            return;
        }

        try
        {
            SaveFileDialog dialog = new()
            {
                Title = "Export regression case",
                Filter = "JSON files (*.json)|*.json",
                DefaultExt = ".json",
                AddExtension = true,
                FileName = $"eats-recognition-{entry.Id:N}.json"
            };

            if (dialog.ShowDialog(this) != true)
            {
                return;
            }

            string appVersion =
                typeof(MainWindow).Assembly.GetName().Version?
                    .ToString(3) ?? "unknown";
            _correctionHistoryService.ExportRegressionCase(
                entry,
                dialog.FileName,
                appVersion);
            HistoryDetailStatusText.Foreground = Brushes.ForestGreen;
            HistoryDetailStatusText.Text =
                $"Sanitized regression case exported to {dialog.FileName}";
        }
        catch (Exception exception)
        {
            HistoryDetailStatusText.Foreground = Brushes.Firebrick;
            HistoryDetailStatusText.Text = exception.Message;
        }
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
                    _activeStars,
                    _activeRouteFixes);
            ParsedVoiceCommand parsed = interpretation.Command;

            string transmission = EatsTransmissionValidator.Validate(
                parsed.ToEatsCommand());

            ApplyParsedCommandToEditor(parsed);
            PreviewText.Text = transmission;
            _currentPreviewWasRecovered =
                interpretation.Kind != VoiceInterpretationKind.Strict;
            ConfigureRouteRequirement(
                transmission,
                GetSpokenStar(parsed));
            PrepareNewPreviewForStaging();
            UpdateCommandSafetyStatus(parsed.Callsign);
            _ = TryAutoStageCurrentCommandAsync("voice recognition");
            if (interpretation.Kind == VoiceInterpretationKind.Strict)
            {
                SpeechStatusText.Text =
                    "Transcription completed and command preview generated.";
            }
            else
            {
                SpeechStatusText.Text = interpretation.Kind ==
                    VoiceInterpretationKind.BestHypothesis
                    ? $"Best command hypothesis generated " +
                      $"({interpretation.ConfidenceScore:P0}). Review it carefully."
                    : "Best-effort interpretation generated. Review the " +
                      "transcript and command carefully.";

                _logger.Warning(
                    "VoiceTranscriptRecovered",
                    "A constrained best-effort voice interpretation was used.",
                    new
                    {
                        interpretation.RecoveredInstructionPhrase,
                        interpretation.StarWasCorrected,
                        interpretation.RouteFixWasCorrected,
                        interpretation.Kind,
                        interpretation.ConfidenceScore,
                        interpretation.ValidHypothesisCount
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
            VoiceInstructionType.ContactFrequency => "ContactFrequency",
            VoiceInstructionType.RemainThisFrequency => "RemainFrequency",
            VoiceInstructionType.SayAgain => "SayAgain",
            VoiceInstructionType.StandBy => "StandBy",
            VoiceInstructionType.SquawkCode => "SquawkCode",
            VoiceInstructionType.SquawkIdent => "SquawkIdent",
            VoiceInstructionType.SquawkAltitude => "SquawkAltitude",
            VoiceInstructionType.SquawkNormal => "SquawkNormal",
            VoiceInstructionType.SquawkStandby => "SquawkStandby",
            VoiceInstructionType.SquawkVfr => "SquawkVfr",
            VoiceInstructionType.StopAltitudeSquawk =>
                "StopAltitudeSquawk",
            VoiceInstructionType.ComplyWithPublishedSpeeds => "PublishedSpeed",
            VoiceInstructionType.ProceedDirect => "Direct",
            VoiceInstructionType.CrossAtAltitude => "CrossAltitude",
            VoiceInstructionType.CrossAtAltitudeAndSpeed =>
                "CrossAltitudeSpeed",
            VoiceInstructionType.CrossDistanceAtAltitude =>
                "CrossDistanceAltitude",
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
            VoiceInstructionType.CrossDistanceAtAltitude =>
                $"{instruction.SecondaryNumericValue} " +
                $"{instruction.SecondaryTextValue} " +
                $"{instruction.TextValue} {instruction.NumericValue}",
            VoiceInstructionType.ComplyWithPublishedSpeeds =>
                instruction.TextValue ?? string.Empty,
            VoiceInstructionType.ExpectApproach =>
                instruction.TextValue ?? string.Empty,
            VoiceInstructionType.ContactFrequency =>
                instruction.TextValue ?? string.Empty,
            VoiceInstructionType.SquawkCode =>
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
        _activeRouteFixes = context.ActiveRouteFixes;
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
            _settings.AutomaticallyStageVerifiedCommands
                ? "A freshly verified command will be staged automatically. " +
                  "The final Enter key is always left to you."
                : "Automatic staging is off in Settings.";
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
    }

    protected override void OnClosed(EventArgs e)
    {
        _lifetimeCancellation.Cancel();
        _transcriptionCancellation?.Cancel();

        _audioRecorder.RecordingCompleted -=
            AudioRecorder_RecordingCompleted;
        if (_pushToTalkHotkeyService is not null)
        {
            _pushToTalkHotkeyService.Pressed -=
                PushToTalkHotkeyService_Pressed;
            _pushToTalkHotkeyService.Released -=
                PushToTalkHotkeyService_Released;
            _pushToTalkHotkeyService.Dispose();
        }

        _audioRecorder.Dispose();

        _logger.Information(
            "ApplicationClosed",
            "The application closed.");

        base.OnClosed(e);
    }
}
