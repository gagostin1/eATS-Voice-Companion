using System.IO;
using System.Text.Json;

namespace EatsVoiceCompanion.App.Services;

public sealed class AppLogger
{
    private readonly object _syncRoot = new();
    private readonly string _logDirectory;

    public AppLogger(string? logDirectory = null)
    {
        _logDirectory = logDirectory ?? Path.Combine(
            Environment.GetFolderPath(
                Environment.SpecialFolder.LocalApplicationData),
            "EatsVoiceCompanion",
            "Logs");

        CleanupOldLogs();
    }

    public void Information(
        string eventName,
        string message,
        object? data = null)
    {
        Write("Information", eventName, message, data, null);
    }

    public void Warning(
        string eventName,
        string message,
        object? data = null)
    {
        Write("Warning", eventName, message, data, null);
    }

    public void Error(
        string eventName,
        string message,
        Exception exception,
        object? data = null)
    {
        ArgumentNullException.ThrowIfNull(exception);
        Write("Error", eventName, message, data, exception);
    }

    private void Write(
        string level,
        string eventName,
        string message,
        object? data,
        Exception? exception)
    {
        try
        {
            var entry = new
            {
                TimestampUtc = DateTime.UtcNow,
                Level = level,
                EventName = eventName,
                Message = message,
                Data = data,
                ExceptionType = exception?.GetType().FullName,
                ExceptionMessage = exception?.Message
            };

            string json = JsonSerializer.Serialize(entry);
            string filePath = Path.Combine(
                _logDirectory,
                $"app-{DateTime.UtcNow:yyyyMMdd}.jsonl");

            lock (_syncRoot)
            {
                Directory.CreateDirectory(_logDirectory);
                File.AppendAllText(filePath, json + Environment.NewLine);
            }
        }
        catch
        {
            // Diagnostics must never interrupt the controller workflow.
        }
    }

    private void CleanupOldLogs()
    {
        try
        {
            if (!Directory.Exists(_logDirectory))
            {
                return;
            }

            DateTime cutoff = DateTime.UtcNow.AddDays(-14);

            foreach (string path in Directory.EnumerateFiles(
                         _logDirectory,
                         "app-*.jsonl",
                         SearchOption.TopDirectoryOnly))
            {
                if (File.GetLastWriteTimeUtc(path) < cutoff)
                {
                    File.Delete(path);
                }
            }
        }
        catch
        {
            // Log retention is best-effort.
        }
    }
}
