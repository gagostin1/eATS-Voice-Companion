using EatsVoiceCompanion.App.Services;

namespace EatsVoiceCompanion.Tests;

public sealed class CorrectionContributionOutboxServiceTests : IDisposable
{
    private readonly string _directory = Path.Combine(
        Path.GetTempPath(),
        $"EatsVoiceCompanionOutbox-{Guid.NewGuid():N}");

    [Fact]
    public void Enqueue_CreatesSanitizedValidatedCase()
    {
        CorrectionContributionOutboxService service = CreateService();
        CorrectionHistoryEntry entry = CreateCorrectedEntry();

        bool added = service.Enqueue(entry, "0.3.0", Aliases());
        CorrectionContributionOutboxItem item = Assert.Single(service.Load());

        Assert.True(added);
        Assert.Equal("DAL123", item.RegressionCase.Callsign);
        Assert.DoesNotContain(
            entry.RecordingFilePath,
            item.Json,
            StringComparison.Ordinal);
        Assert.DoesNotContain(
            entry.RecognitionPrompt,
            item.Json,
            StringComparison.Ordinal);
        CorrectionRegressionCaseValidator.Validate(item.RegressionCase);
    }

    [Fact]
    public void Enqueue_DeduplicatesIdenticalCorrectionContent()
    {
        CorrectionContributionOutboxService service = CreateService();
        CorrectionHistoryEntry first = CreateCorrectedEntry();
        CorrectionHistoryEntry second = CreateCorrectedEntry();
        second.RecordedAtUtc = first.RecordedAtUtc.AddMinutes(10);

        Assert.True(service.Enqueue(first, "0.3.0", Aliases()));
        Assert.False(service.Enqueue(second, "0.3.0", Aliases()));
        Assert.Single(service.Load());
    }

    [Fact]
    public void Enqueue_IgnoresAttemptMarkedCorrect()
    {
        CorrectionContributionOutboxService service = CreateService();
        CorrectionHistoryEntry entry = CreateCorrectedEntry();
        entry.ReviewStatus = CorrectionReviewStatus.Correct;

        Assert.False(service.Enqueue(entry, "0.3.0", Aliases()));
        Assert.Empty(service.Load());
    }

    [Fact]
    public void ExportAllAndDelete_ManagePendingCases()
    {
        CorrectionContributionOutboxService service = CreateService();
        Assert.True(service.Enqueue(
            CreateCorrectedEntry(),
            "0.3.0",
            Aliases()));
        CorrectionContributionOutboxItem item = Assert.Single(service.Load());
        string exportDirectory = Path.Combine(_directory, "export");

        Assert.Equal(1, service.ExportAll(exportDirectory));
        Assert.True(File.Exists(Path.Combine(
            exportDirectory,
            Path.GetFileName(item.FilePath))));

        service.Delete(item);
        Assert.Empty(service.Load());
    }

    [Fact]
    public void MarkSent_ArchivesReceiptBeforeRemovingPendingCase()
    {
        CorrectionContributionOutboxService service = CreateService();
        Assert.True(service.Enqueue(
            CreateCorrectedEntry(),
            "0.3.0",
            Aliases()));
        CorrectionContributionOutboxItem item = Assert.Single(service.Load());
        string receiptId = new('a', 64);

        service.MarkSent(
            item,
            receiptId,
            "accepted",
            DateTimeOffset.Parse("2026-09-13T21:00:00Z"));

        Assert.Empty(service.Load());
        CorrectionContributionReceipt receipt =
            Assert.Single(service.LoadSent());
        Assert.Equal("accepted", receipt.Status);
        Assert.Equal("DAL123", receipt.Contribution.Callsign);
        string receiptJson = File.ReadAllText(Path.Combine(
            service.SentDirectory,
            $"{receiptId}.json"));
        Assert.Contains("\"Status\": \"accepted\"", receiptJson);
        Assert.Contains("\"Callsign\": \"DAL123\"", receiptJson);
    }

    private CorrectionContributionOutboxService CreateService()
    {
        string historyDirectory = Path.Combine(_directory, "history");
        string outboxDirectory = Path.Combine(_directory, "outbox");
        string sentDirectory = Path.Combine(_directory, "sent");
        return new CorrectionContributionOutboxService(
            new CorrectionHistoryService(historyDirectory),
            outboxDirectory,
            sentDirectory);
    }

    private static Dictionary<string, string> Aliases() => new()
    {
        ["DELTA"] = "DAL"
    };

    private static CorrectionHistoryEntry CreateCorrectedEntry() => new()
    {
        RecordingFilePath = @"C:\Users\pilot\recording.wav",
        OriginalTranscript = "Delta 123, clear direct Aussie.",
        GeneratedCommand = "DAL123 R",
        WasBestEffort = true,
        ControllerPosition = "Atlanta Center",
        RecognitionPrompt = "Sensitive local recognition prompt.",
        ActiveCallsigns = ["DAL123", "AAL456"],
        ActiveRouteFixes = new Dictionary<string, string[]>
        {
            ["DAL123"] = ["OZZZI"]
        },
        CorrectedTranscript = "Delta 123, cleared direct OZZZI.",
        ExpectedCommand = "DAL123 ..OZZZI",
        ReviewStatus = CorrectionReviewStatus.Corrected
    };

    public void Dispose()
    {
        if (Directory.Exists(_directory))
        {
            Directory.Delete(_directory, recursive: true);
        }
    }
}
