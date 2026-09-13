using EatsVoiceCompanion.App.Services;

namespace EatsVoiceCompanion.Tests;

public sealed class LocalCorrectionMemoryServiceTests
{
    private static readonly IReadOnlyDictionary<string, string> Aliases =
        new Dictionary<string, string>
        {
            ["DELTA"] = "DAL"
        };

    [Fact]
    public void Enrich_AddsReviewedRelevantCorrectionToWhisperPrompt()
    {
        LocalCorrectionMemoryService service = new();
        service.Update(
        [
            Correction(
                "Delta 1288 cross Aussie at and maintain 1-3,000",
                "Delta 1288 cross OZZZI at and maintain 13,000",
                "DAL1288 XOZZZI@130"),
            new CorrectionHistoryEntry
            {
                ReviewStatus = CorrectionReviewStatus.Unreviewed,
                OriginalTranscript = "Delta 1288 cross Fizzy at 13,000",
                CorrectedTranscript =
                    "Delta 1288 cross FIZZY at 13,000",
                ExpectedCommand = "DAL1288 XFIZZY@130"
            }
        ]);

        RecognitionContextResult enriched = service.Enrich(
            Context(["OZZZI", "WINNG"]));

        Assert.Contains("OZZZI may be heard as Aussie", enriched.Prompt);
        Assert.Contains("cross OZZZI", enriched.Prompt);
        Assert.DoesNotContain("FIZZY", enriched.Prompt);
    }

    [Fact]
    public void ApplyLearnedFixAliases_ReplacesAliasForAircraftRoute()
    {
        LocalCorrectionMemoryService service = new();
        service.Update(
        [
            Correction(
                "Delta 1288 cross Aussie at and maintain 1-3,000",
                "Delta 1288 cross OZZZI at and maintain 13,000",
                "DAL1288 XOZZZI@130")
        ]);

        string result = service.ApplyLearnedFixAliases(
            "Delta 825 cross Aussie at and maintain 13,000",
            new HashSet<string>(["DAL825"]),
            new Dictionary<string, IReadOnlySet<string>>
            {
                ["DAL825"] = new HashSet<string>(["OZZZI", "WINNG"])
            },
            Aliases);

        Assert.Equal(
            "Delta 825 cross OZZZI at and maintain 13,000",
            result);
    }

    [Fact]
    public void ApplyLearnedFixAliases_DoesNotUseFixOutsideAircraftRoute()
    {
        LocalCorrectionMemoryService service = new();
        service.Update(
        [
            Correction(
                "Delta 1288 cross Aussie at and maintain 1-3,000",
                "Delta 1288 cross OZZZI at and maintain 13,000",
                "DAL1288 XOZZZI@130")
        ]);
        const string transcript =
            "Delta 825 cross Aussie at and maintain 13,000";

        string result = service.ApplyLearnedFixAliases(
            transcript,
            new HashSet<string>(["DAL825"]),
            new Dictionary<string, IReadOnlySet<string>>
            {
                ["DAL825"] = new HashSet<string>(["BANKR", "HOMER"])
            },
            Aliases);

        Assert.Equal(transcript, result);
    }

    [Fact]
    public void MarkCorrect_LearnsSafeFixAliasWithoutUsingWrongTranscriptAsExample()
    {
        LocalCorrectionMemoryService service = new();
        CorrectionHistoryEntry entry = Correction(
            "Delta 1288 cross Aussie at and maintain 13,000",
            "Delta 1288 cross OZZZI at and maintain 13,000",
            "DAL1288 XOZZZI@130");
        entry.ReviewStatus = CorrectionReviewStatus.Correct;
        entry.CorrectedTranscript = entry.OriginalTranscript;
        service.Update([entry]);

        RecognitionContextResult enriched = service.Enrich(
            Context(["OZZZI"]));
        string adapted = service.ApplyLearnedFixAliases(
            "Delta 825 cross Aussie at and maintain 13,000",
            new HashSet<string>(["DAL825"]),
            new Dictionary<string, IReadOnlySet<string>>
            {
                ["DAL825"] = new HashSet<string>(["OZZZI"])
            },
            Aliases);

        Assert.Contains("OZZZI may be heard as Aussie", enriched.Prompt);
        Assert.DoesNotContain("ATC examples", enriched.Prompt);
        Assert.Contains("cross OZZZI", adapted);
    }

    [Fact]
    public void Update_IgnoresCorrectionExcludedFromLearning()
    {
        LocalCorrectionMemoryService service = new();
        CorrectionHistoryEntry entry = Correction(
            "Delta 1288 cross Aussie at and maintain 13,000",
            "Delta 1288 cross OZZZI at and maintain 13,000",
            "DAL1288 XOZZZI@130");
        entry.UseForLocalLearning = false;
        service.Update([entry]);

        RecognitionContextResult enriched = service.Enrich(
            Context(["OZZZI"]));
        string adapted = service.ApplyLearnedFixAliases(
            "Delta 825 cross Aussie at and maintain 13,000",
            new HashSet<string>(["DAL825"]),
            new Dictionary<string, IReadOnlySet<string>>
            {
                ["DAL825"] = new HashSet<string>(["OZZZI"])
            },
            Aliases);

        Assert.Equal("Base prompt.", enriched.Prompt);
        Assert.DoesNotContain("OZZZI", adapted);
        Assert.Empty(service.GetLearnedFixMappings());
    }

    private static CorrectionHistoryEntry Correction(
        string original,
        string corrected,
        string expected) => new()
        {
            RecordedAtUtc = DateTime.UtcNow,
            ReviewStatus = CorrectionReviewStatus.Corrected,
            OriginalTranscript = original,
            CorrectedTranscript = corrected,
            ExpectedCommand = expected
        };

    private static RecognitionContextResult Context(
        IEnumerable<string> fixes) => new(
            "Base prompt.",
            "Ready",
            true,
            new HashSet<string>(["DAL825"]),
            new Dictionary<string, string>(),
            new Dictionary<string, IReadOnlySet<string>>
            {
                ["DAL825"] = fixes.ToHashSet(
                    StringComparer.OrdinalIgnoreCase)
            });
}
