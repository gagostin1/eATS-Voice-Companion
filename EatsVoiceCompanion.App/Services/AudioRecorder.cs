using System.IO;
using NAudio.Wave;

namespace EatsVoiceCompanion.App.Services;

public sealed class AudioRecordingCompletedEventArgs : EventArgs
{
    public AudioRecordingCompletedEventArgs(
        string filePath,
        Exception? error)
    {
        FilePath = filePath;
        Error = error;
    }

    public string FilePath { get; }

    public Exception? Error { get; }
}

public sealed class AudioRecorder : IDisposable
{
    private WaveInEvent? _waveIn;
    private WaveFileWriter? _writer;

    public event EventHandler<AudioRecordingCompletedEventArgs>?
        RecordingCompleted;

    public bool IsRecording { get; private set; }

    public string? CurrentFilePath { get; private set; }

    public string Start(int deviceNumber)
    {
        if (IsRecording)
        {
            throw new InvalidOperationException(
                "A recording is already in progress.");
        }

        string directory = Path.Combine(
            Path.GetTempPath(),
            "EatsVoiceCompanion");

        Directory.CreateDirectory(directory);

        CurrentFilePath = Path.Combine(
            directory,
            $"recording-{DateTime.Now:yyyyMMdd-HHmmss}.wav");

        _waveIn = new WaveInEvent
        {
            DeviceNumber = deviceNumber,
            WaveFormat = new WaveFormat(
                rate: 16000,
                bits: 16,
                channels: 1),
            BufferMilliseconds = 50
        };

        _writer = new WaveFileWriter(
            CurrentFilePath,
            _waveIn.WaveFormat);

        _waveIn.DataAvailable += HandleDataAvailable;
        _waveIn.RecordingStopped += HandleRecordingStopped;

        try
        {
            _waveIn.StartRecording();
            IsRecording = true;

            return CurrentFilePath;
        }
        catch
        {
            Cleanup();
            throw;
        }
    }

    public void Stop()
    {
        if (!IsRecording || _waveIn is null)
        {
            return;
        }

        _waveIn.StopRecording();
    }

    private void HandleDataAvailable(
        object? sender,
        WaveInEventArgs eventArgs)
    {
        _writer?.Write(
            eventArgs.Buffer,
            0,
            eventArgs.BytesRecorded);
    }

    private void HandleRecordingStopped(
        object? sender,
        StoppedEventArgs eventArgs)
    {
        string filePath = CurrentFilePath ?? string.Empty;

        Cleanup();

        RecordingCompleted?.Invoke(
            this,
            new AudioRecordingCompletedEventArgs(
                filePath,
                eventArgs.Exception));
    }

    private void Cleanup()
    {
        IsRecording = false;

        _writer?.Dispose();
        _writer = null;

        if (_waveIn is not null)
        {
            _waveIn.DataAvailable -= HandleDataAvailable;
            _waveIn.RecordingStopped -= HandleRecordingStopped;
            _waveIn.Dispose();
            _waveIn = null;
        }
    }

    public void Dispose()
    {
        if (IsRecording)
        {
            Stop();
            return;
        }

        Cleanup();
    }
}