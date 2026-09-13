using EatsVoiceCompanion.App.Services;

namespace EatsVoiceCompanion.Tests;

public sealed class CorrectionHistoryServiceTests : IDisposable
{
    private readonly string _directory = Path.Combine(
        Path.GetTempPath(),
        $"EatsVoiceCompanionHistory-{Guid.NewGuid():N}");

    [Fact]
    public void Load_ReturnsEmptyWhenDirectoryDoesNotExist()
    {
        CorrectionHistoryService service = new(_directory);

        Assert.Empty(service.Load());
    }

    [Fact]
    public void SaveAndLoad_RoundTripsCorrectionCase()
    {
        CorrectionHistoryService service = new(_directory);
        CorrectionHistoryEntry expected = CreateEntry();
        expected.ReviewStatus = CorrectionReviewStatus.Corrected;
        expected.CorrectedTranscript = "Delta 123, fly heading 270.";
        expected.ExpectedCommand = "DAL123 FH270";

        service.Save(expected);
        CorrectionHistoryEntry actual = Assert.Single(service.Load());

        Assert.Equal(expected.Id, actual.Id);
        Assert.Equal(expected.OriginalTranscript, actual.OriginalTranscript);
        Assert.Equal(expected.GeneratedCommand, actual.GeneratedCommand);
        Assert.Equal(expected.ReviewStatus, actual.ReviewStatus);
        Assert.Equal(expected.CorrectedTranscript, actual.CorrectedTranscript);
        Assert.Equal(expected.ExpectedCommand, actual.ExpectedCommand);
        Assert.Equal(expected.ActiveCallsigns, actual.ActiveCallsigns);
        Assert.Equal("BANKR5", actual.ActiveStars["DAL123"]);
        Assert.Equal(
            ["HOMER", "OZZZI"],
            actual.ActiveRouteFixes["DAL123"]);
    }

    [Fact]
    public void Save_UpdatesExistingEntryInsteadOfDuplicatingIt()
    {
        CorrectionHistoryService service = new(_directory);
        CorrectionHistoryEntry entry = CreateEntry();
        service.Save(entry);

        entry.ReviewStatus = CorrectionReviewStatus.Correct;
        entry.ExpectedCommand = entry.GeneratedCommand;
        service.Save(entry);

        CorrectionHistoryEntry actual = Assert.Single(service.Load());
        Assert.Equal(CorrectionReviewStatus.Correct, actual.ReviewStatus);
        Assert.Equal("DAL123 FH270", actual.ExpectedCommand);
    }

    [Fact]
    public void Load_SkipsDamagedEntries()
    {
        CorrectionHistoryService service = new(_directory);
        CorrectionHistoryEntry expected = CreateEntry();
        service.Save(expected);
        File.WriteAllText(
            Path.Combine(_directory, "damaged.json"),
            "not json");

        CorrectionHistoryEntry actual = Assert.Single(service.Load());

        Assert.Equal(expected.Id, actual.Id);
    }

    [Fact]
    public void ExportRegressionCase_RemovesAudioPathPromptAndUnrelatedTraffic()
    {
        CorrectionHistoryService service = new(_directory);
        CorrectionHistoryEntry entry = CreateEntry();
        entry.ActiveCallsigns = ["DAL123", "AAL456"];
        entry.ReviewStatus = CorrectionReviewStatus.Corrected;
        entry.CorrectedTranscript = "Delta 123, fly heading 270.";
        entry.ExpectedCommand = "DAL123 FH270";
        string path = Path.Combine(_directory, "regression.json");

        service.ExportRegressionCase(entry, path, "0.3.0");
        string json = File.ReadAllText(path);

        Assert.Contains("DAL123 FH270", json, StringComparison.Ordinal);
        Assert.Contains("BANKR5", json, StringComparison.Ordinal);
        Assert.Contains("HOMER", json, StringComparison.Ordinal);
        Assert.DoesNotContain(
            entry.RecordingFilePath,
            json,
            StringComparison.Ordinal);
        Assert.DoesNotContain(
            entry.RecognitionPrompt,
            json,
            StringComparison.Ordinal);
        Assert.DoesNotContain("AAL456", json, StringComparison.Ordinal);
    }

    [Fact]
    public void ExportRegressionCase_RejectsUnreviewedAttempt()
    {
        CorrectionHistoryService service = new(_directory);
        CorrectionHistoryEntry entry = CreateEntry();

        InvalidOperationException exception = Assert.Throws<
            InvalidOperationException>(() =>
                service.ExportRegressionCase(
                    entry,
                    Path.Combine(_directory, "regression.json"),
                    "0.3.0"));

        Assert.Contains(
            "Review the attempt",
            exception.Message,
            StringComparison.Ordinal);
    }

    private static CorrectionHistoryEntry CreateEntry() => new()
    {
        RecordingFilePath = @"C:\recording.wav",
        OriginalTranscript = "Delta 123, fly heading 270.",
        GeneratedCommand = "DAL123 FH270",
        ControllerPosition = "Atlanta Center",
        RecognitionPrompt = "Active aircraft callsigns: Delta 123.",
        ActiveCallsigns = ["DAL123"],
        ActiveStars = new Dictionary<string, string>
        {
            ["DAL123"] = "BANKR5"
        },
        ActiveRouteFixes = new Dictionary<string, string[]>
        {
            ["DAL123"] = ["HOMER", "OZZZI"]
        }
    };

    public void Dispose()
    {
        if (Directory.Exists(_directory))
        {
            Directory.Delete(_directory, recursive: true);
        }
    }
}
