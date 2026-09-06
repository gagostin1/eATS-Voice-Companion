using System.Text.Json;
using EatsVoiceCompanion.App.Services;

namespace EatsVoiceCompanion.Tests;

public sealed class AppLoggerTests : IDisposable
{
    private readonly string _directory = Path.Combine(
        Path.GetTempPath(),
        $"EatsVoiceCompanionLogs-{Guid.NewGuid():N}");

    [Fact]
    public void Information_WritesStructuredJsonLine()
    {
        AppLogger logger = new(_directory);

        logger.Information(
            "TestEvent",
            "Test message",
            new { Count = 3 });

        string path = Assert.Single(
            Directory.GetFiles(_directory, "app-*.jsonl"));
        using JsonDocument document = JsonDocument.Parse(
            File.ReadAllText(path));
        JsonElement root = document.RootElement;

        Assert.Equal("Information", root.GetProperty("Level").GetString());
        Assert.Equal("TestEvent", root.GetProperty("EventName").GetString());
        Assert.Equal(3, root.GetProperty("Data").GetProperty("Count").GetInt32());
    }

    [Fact]
    public void Error_RecordsExceptionWithoutThrowing()
    {
        AppLogger logger = new(_directory);

        logger.Error(
            "Failure",
            "Something failed",
            new InvalidOperationException("Expected failure"));

        string path = Assert.Single(
            Directory.GetFiles(_directory, "app-*.jsonl"));
        string json = File.ReadAllText(path);

        Assert.Contains("InvalidOperationException", json);
        Assert.Contains("Expected failure", json);
    }

    public void Dispose()
    {
        if (Directory.Exists(_directory))
        {
            Directory.Delete(_directory, recursive: true);
        }
    }
}
