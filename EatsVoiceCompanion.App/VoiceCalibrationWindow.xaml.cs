using System.Windows;
using System.Windows.Input;
using System.Windows.Media;
using EatsVoiceCompanion.App.Services;

namespace EatsVoiceCompanion.App;

public partial class VoiceCalibrationWindow : Window
{
    private readonly MicrophoneService _microphoneService;
    private readonly AudioRecorder _audioRecorder;
    private readonly ISpeechRecognitionService _speechRecognitionService;
    private readonly CorrectionHistoryService _historyService;
    private readonly CancellationTokenSource _lifetimeCancellation = new();
    private GlobalPushToTalkHotkeyService? _hotkeyService;
    private PushToTalkHotkeyDefinition? _hotkey;
    private Key? _modifierCandidate;
    private bool _isAssigningHotkey;
    private int _phraseIndex;
    private int _matchedCount;
    private int _correctedCount;
    private int _skippedCount;
    private VoiceCalibrationResult? _pendingResult;
    private string _pendingRecordingPath = string.Empty;

    public VoiceCalibrationWindow(
        MicrophoneService microphoneService,
        RecordingStorageService recordingStorage,
        ISpeechRecognitionService speechRecognitionService,
        CorrectionHistoryService historyService,
        string? preferredMicrophoneName,
        string? pushToTalkHotkey)
    {
        _microphoneService = microphoneService;
        _audioRecorder = new AudioRecorder(recordingStorage);
        _speechRecognitionService = speechRecognitionService;
        _historyService = historyService;
        PushToTalkHotkeyDefinition.TryParse(pushToTalkHotkey, out _hotkey);

        InitializeComponent();
        _audioRecorder.RecordingCompleted += RecordingCompleted;
        LoadMicrophones(preferredMicrophoneName);
        UpdateHotkeyUi();
        InitializeHotkeyService();
    }

    public bool Completed { get; private set; }

    public string? SelectedMicrophoneName =>
        (MicrophoneComboBox.SelectedItem as AudioInputDevice)?.Name;

    public string? PushToTalkHotkey => _hotkey?.ToString();

    private void InitializeHotkeyService()
    {
        try
        {
            _hotkeyService = new GlobalPushToTalkHotkeyService
            {
                Hotkey = _hotkey
            };
            _hotkeyService.Pressed += HotkeyPressed;
            _hotkeyService.Released += HotkeyReleased;
        }
        catch (Exception exception)
        {
            HotkeyStatusText.Foreground = Brushes.Firebrick;
            HotkeyStatusText.Text =
                $"The PTT listener could not start: {exception.Message}";
        }
    }

    private void LoadMicrophones(string? preferredName = null)
    {
        try
        {
            IReadOnlyList<AudioInputDevice> devices =
                _microphoneService.GetDevices();
            MicrophoneComboBox.ItemsSource = devices;
            MicrophoneComboBox.SelectedItem = devices.FirstOrDefault(
                device => string.Equals(
                    device.Name,
                    preferredName,
                    StringComparison.OrdinalIgnoreCase)) ??
                devices.FirstOrDefault();
            MicrophoneStatusText.Text = devices.Count == 0
                ? "No recording devices were detected."
                : $"{devices.Count} recording device(s) detected.";
        }
        catch (Exception exception)
        {
            MicrophoneStatusText.Text =
                $"Unable to enumerate microphones: {exception.Message}";
        }
    }

    private void RefreshMicrophones_Click(object sender, RoutedEventArgs e) =>
        LoadMicrophones(SelectedMicrophoneName);

    private void AssignHotkey_Click(object sender, RoutedEventArgs e)
    {
        _isAssigningHotkey = true;
        _modifierCandidate = null;
        if (_hotkeyService is not null)
        {
            _hotkeyService.Hotkey = null;
        }

        AssignHotkeyButton.Content = "Press shortcut...";
        HotkeyStatusText.Foreground = Brushes.DarkGoldenrod;
        HotkeyStatusText.Text =
            "Press a key or combination. Release a modifier to use it alone.";
        Keyboard.Focus(AssignHotkeyButton);
    }

    private void Window_PreviewKeyDown(object sender, KeyEventArgs e)
    {
        if (!_isAssigningHotkey)
        {
            return;
        }

        e.Handled = true;
        Key key = e.Key == Key.System ? e.SystemKey : e.Key;
        if (key == Key.Escape)
        {
            _isAssigningHotkey = false;
            _modifierCandidate = null;
            UpdateHotkeyUi();
            return;
        }

        if (PushToTalkHotkeyDefinition.IsModifierKey(key))
        {
            _modifierCandidate ??= key;
            return;
        }

        try
        {
            _hotkey = PushToTalkHotkeyDefinition.Create(
                key,
                Keyboard.Modifiers);
            _isAssigningHotkey = false;
            _modifierCandidate = null;
            UpdateHotkeyUi();
        }
        catch (ArgumentException exception)
        {
            HotkeyStatusText.Text = exception.Message;
        }
    }

    private void Window_PreviewKeyUp(object sender, KeyEventArgs e)
    {
        if (!_isAssigningHotkey || _modifierCandidate is not { } candidate)
        {
            return;
        }

        Key key = e.Key == Key.System ? e.SystemKey : e.Key;
        if (key != candidate)
        {
            return;
        }

        e.Handled = true;
        _hotkey = PushToTalkHotkeyDefinition.Create(
            candidate,
            ModifierKeys.None);
        _isAssigningHotkey = false;
        _modifierCandidate = null;
        UpdateHotkeyUi();
    }

    private void UpdateHotkeyUi()
    {
        HotkeyTextBox.Text = _hotkey?.ToString() ?? "Not assigned";
        AssignHotkeyButton.Content = "Assign";
        HotkeyStatusText.Foreground = Brushes.DimGray;
        HotkeyStatusText.Text = _hotkey is null
            ? "Assign a PTT key before calibration."
            : $"Hold {_hotkey} during calibration. The key remains available to other apps.";
        if (_hotkeyService is not null)
        {
            _hotkeyService.Hotkey = _hotkey;
        }
    }

    private void BeginCalibration_Click(object sender, RoutedEventArgs e)
    {
        if (MicrophoneComboBox.SelectedItem is not AudioInputDevice)
        {
            MicrophoneStatusText.Foreground = Brushes.Firebrick;
            MicrophoneStatusText.Text = "Select a microphone before continuing.";
            return;
        }

        if (_hotkey is null)
        {
            HotkeyStatusText.Foreground = Brushes.Firebrick;
            HotkeyStatusText.Text = "Assign a push-to-talk key before continuing.";
            return;
        }

        DeviceSetupPanel.Visibility = Visibility.Collapsed;
        CalibrationPanel.Visibility = Visibility.Visible;
        StepText.Text = "CALIBRATION";
        ShowCurrentPhrase();
    }

    private void ShowCurrentPhrase()
    {
        VoiceCalibrationPhrase phrase =
            VoiceCalibrationService.Phrases[_phraseIndex];
        PhraseProgressText.Text =
            $"PHRASE {_phraseIndex + 1} OF {VoiceCalibrationService.Phrases.Count}";
        CalibrationPhraseText.Text = phrase.Instruction;
        CalibrationStatusText.Foreground = Brushes.DimGray;
        CalibrationStatusText.Text =
            $"Hold {_hotkey} or the record button, read the phrase, then release.";
        CalibrationRecordButton.Content = "Hold to record";
        CalibrationRecordButton.IsEnabled = true;
        SkipPhraseButton.IsEnabled = true;
        ResultPanel.Visibility = Visibility.Collapsed;
        RetryButton.IsEnabled = false;
        NextPhraseButton.IsEnabled = false;
        NextPhraseButton.Content = _phraseIndex ==
            VoiceCalibrationService.Phrases.Count - 1
                ? "View summary"
                : "Continue";
        _pendingResult = null;
        _pendingRecordingPath = string.Empty;
    }

    private void CalibrationRecordButton_MouseDown(
        object sender,
        MouseButtonEventArgs e)
    {
        e.Handled = true;
        StartRecording(captureMouse: true);
    }

    private void CalibrationRecordButton_MouseUp(
        object sender,
        MouseButtonEventArgs e)
    {
        e.Handled = true;
        StopRecording();
    }

    private void HotkeyPressed(object? sender, EventArgs e) =>
        _ = Dispatcher.BeginInvoke(() =>
        {
            if (!_isAssigningHotkey &&
                CalibrationPanel.Visibility == Visibility.Visible)
            {
                StartRecording(captureMouse: false);
            }
        });

    private void HotkeyReleased(object? sender, EventArgs e) =>
        _ = Dispatcher.BeginInvoke(StopRecording);

    private void StartRecording(bool captureMouse)
    {
        if (_audioRecorder.IsRecording ||
            !CalibrationRecordButton.IsEnabled ||
            MicrophoneComboBox.SelectedItem is not AudioInputDevice microphone)
        {
            return;
        }

        try
        {
            if (captureMouse)
            {
                CalibrationRecordButton.CaptureMouse();
            }

            _pendingRecordingPath = _audioRecorder.Start(
                microphone.DeviceNumber);
            CalibrationRecordButton.Content = "Recording — release to stop";
            CalibrationStatusText.Foreground = Brushes.Firebrick;
            CalibrationStatusText.Text = $"Listening through {microphone.Name}...";
            ResultPanel.Visibility = Visibility.Collapsed;
            SkipPhraseButton.IsEnabled = false;
            RetryButton.IsEnabled = false;
            NextPhraseButton.IsEnabled = false;
        }
        catch (Exception exception)
        {
            CalibrationStatusText.Foreground = Brushes.Firebrick;
            CalibrationStatusText.Text =
                $"Recording could not start: {exception.Message}";
        }
    }

    private void StopRecording()
    {
        if (CalibrationRecordButton.IsMouseCaptured)
        {
            CalibrationRecordButton.ReleaseMouseCapture();
        }

        if (!_audioRecorder.IsRecording)
        {
            return;
        }

        CalibrationRecordButton.Content = "Processing...";
        CalibrationRecordButton.IsEnabled = false;
        CalibrationStatusText.Foreground = Brushes.DimGray;
        CalibrationStatusText.Text = "Saving and transcribing locally...";
        _audioRecorder.Stop();
    }

    private async void RecordingCompleted(
        object? sender,
        AudioRecordingCompletedEventArgs e)
    {
        if (e.Error is not null)
        {
            await Dispatcher.InvokeAsync(() =>
            {
                CalibrationStatusText.Foreground = Brushes.Firebrick;
                CalibrationStatusText.Text =
                    $"Recording failed: {e.Error.Message}";
                CalibrationRecordButton.Content = "Hold to record";
                CalibrationRecordButton.IsEnabled = true;
                SkipPhraseButton.IsEnabled = true;
            });
            return;
        }

        try
        {
            IProgress<string> progress = new Progress<string>(message =>
                _ = Dispatcher.BeginInvoke(() =>
                    CalibrationStatusText.Text = message));
            string transcript = await _speechRecognitionService.TranscribeAsync(
                e.FilePath,
                progress,
                VoiceCalibrationService.RecognitionPrompt,
                _lifetimeCancellation.Token);
            VoiceCalibrationResult result = VoiceCalibrationService.Evaluate(
                VoiceCalibrationService.Phrases[_phraseIndex],
                transcript);
            await Dispatcher.InvokeAsync(() =>
            {
                _pendingResult = result;
                ShowComparison(result);
            });
        }
        catch (OperationCanceledException)
        {
            return;
        }
        catch (Exception exception)
        {
            await Dispatcher.InvokeAsync(() =>
            {
                CalibrationStatusText.Foreground = Brushes.Firebrick;
                CalibrationStatusText.Text =
                    $"Recognition could not finish: {exception.Message}";
            });
        }
        finally
        {
            if (!_lifetimeCancellation.IsCancellationRequested)
            {
                await Dispatcher.InvokeAsync(() =>
                {
                    CalibrationRecordButton.Content = "Hold to record";
                    CalibrationRecordButton.IsEnabled = true;
                    SkipPhraseButton.IsEnabled = true;
                });
            }
        }
    }

    private void ShowComparison(VoiceCalibrationResult result)
    {
        if (string.IsNullOrWhiteSpace(result.Transcript))
        {
            CalibrationStatusText.Foreground = Brushes.DarkGoldenrod;
            CalibrationStatusText.Text =
                "No speech was recognized. Retry or skip this phrase.";
            RetryButton.IsEnabled = true;
            return;
        }

        ResultPanel.Visibility = Visibility.Visible;
        RecognizedTranscriptText.Text = result.Transcript;
        string actual = result.GeneratedCommand ?? "No command generated";
        string expected =
            VoiceCalibrationService.Phrases[_phraseIndex].ExpectedCommand;
        CommandComparisonText.Text = $"{actual}\n{expected}";
        ComparisonHeadingText.Foreground = result.IsMatch
            ? Brushes.ForestGreen
            : Brushes.DarkGoldenrod;
        ComparisonHeadingText.Text = result.IsMatch
            ? "Matched automatically"
            : "Difference found automatically";
        LearningExplanationText.Text = result.IsMatch
            ? "The recognized command matches the displayed target."
            : "When you continue, the displayed phrase and expected command will be saved as the correction for local learning.";
        CalibrationStatusText.Foreground = Brushes.DimGray;
        CalibrationStatusText.Text = "Review the comparison, retry, or continue.";
        RetryButton.IsEnabled = true;
        NextPhraseButton.IsEnabled = true;
    }

    private void RetryPhrase_Click(object sender, RoutedEventArgs e) =>
        ShowCurrentPhrase();

    private void SkipPhrase_Click(object sender, RoutedEventArgs e)
    {
        if (_audioRecorder.IsRecording || !SkipPhraseButton.IsEnabled)
        {
            return;
        }

        _skippedCount++;
        AdvancePhrase();
    }

    private void NextPhrase_Click(object sender, RoutedEventArgs e)
    {
        if (_pendingResult is null ||
            string.IsNullOrWhiteSpace(_pendingResult.Transcript))
        {
            return;
        }

        try
        {
            _historyService.Save(
                VoiceCalibrationService.CreateHistoryEntry(
                    VoiceCalibrationService.Phrases[_phraseIndex],
                    _pendingResult,
                    _pendingRecordingPath));
        }
        catch (Exception exception)
        {
            CalibrationStatusText.Foreground = Brushes.Firebrick;
            CalibrationStatusText.Text =
                $"The calibration result could not be saved: {exception.Message}";
            return;
        }

        if (_pendingResult.IsMatch)
        {
            _matchedCount++;
        }
        else
        {
            _correctedCount++;
        }

        AdvancePhrase();
    }

    private void AdvancePhrase()
    {
        _phraseIndex++;
        if (_phraseIndex < VoiceCalibrationService.Phrases.Count)
        {
            ShowCurrentPhrase();
            return;
        }

        CalibrationPanel.Visibility = Visibility.Collapsed;
        SummaryPanel.Visibility = Visibility.Visible;
        StepText.Text = "SUMMARY";
        SummaryResultText.Text =
            $"{_matchedCount} matched automatically\n" +
            $"{_correctedCount} correction(s) added to local learning\n" +
            $"{_skippedCount} skipped";
    }

    private void Finish_Click(object sender, RoutedEventArgs e)
    {
        Completed = true;
        DialogResult = true;
    }

    private void Postpone_Click(object sender, RoutedEventArgs e) => Close();

    protected override void OnClosed(EventArgs e)
    {
        _lifetimeCancellation.Cancel();
        _audioRecorder.RecordingCompleted -= RecordingCompleted;
        _audioRecorder.Dispose();
        if (_hotkeyService is not null)
        {
            _hotkeyService.Pressed -= HotkeyPressed;
            _hotkeyService.Released -= HotkeyReleased;
            _hotkeyService.Dispose();
        }
        _lifetimeCancellation.Dispose();
        base.OnClosed(e);
    }
}
