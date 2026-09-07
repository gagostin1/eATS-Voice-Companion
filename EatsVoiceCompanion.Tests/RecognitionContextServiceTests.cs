using EatsVoiceCompanion.App.Services;

namespace EatsVoiceCompanion.Tests;

public sealed class RecognitionContextServiceTests
{
    private static readonly DateTime Now =
        new(2026, 9, 6, 12, 0, 0, DateTimeKind.Utc);

    private static readonly IReadOnlyDictionary<string, string> Aliases =
        new Dictionary<string, string>
        {
            ["AMERICAN"] = "AAL"
        };

    [Fact]
    public void Build_ReturnsUnavailableWhenEatsIsNotRunning()
    {
        RecognitionContextService service = CreateService(
            process: null,
            snapshot: Snapshot(Now));

        RecognitionContextResult result = service.Build("Atlanta Center");

        Assert.False(result.HasFreshSnapshot);
        Assert.Empty(result.ActiveCallsigns);
        Assert.Contains("not running", result.StatusMessage);
        Assert.Equal(
            "Controller position: Atlanta Center.",
            result.Prompt);
    }

    [Fact]
    public void Build_RejectsStaleSnapshot()
    {
        RecognitionContextService service = CreateService(
            Process(),
            Snapshot(Now.AddMinutes(-4)));

        RecognitionContextResult result = service.Build("Atlanta Center");

        Assert.False(result.HasFreshSnapshot);
        Assert.Contains("stale", result.StatusMessage);
    }

    [Fact]
    public void Build_RetainsStaleAircraftOnlyForSpeechRecovery()
    {
        RecognitionContextService service = CreateService(
            Process(),
            Snapshot(Now.AddMinutes(-4), "AAL123"));

        RecognitionContextResult result = service.Build("Atlanta Center");

        Assert.False(result.HasFreshSnapshot);
        Assert.Contains("AAL123", result.ActiveCallsigns);
        Assert.Contains("American 123", result.Prompt);
        Assert.Contains("staging remains disabled", result.StatusMessage);
    }

    [Fact]
    public void Build_ReturnsFreshAircraftAndPrompt()
    {
        RecognitionContextService service = CreateService(
            Process(),
            Snapshot(Now.AddSeconds(-30), "AAL123"));

        RecognitionContextResult result = service.Build("Atlanta Center");

        Assert.True(result.HasFreshSnapshot);
        Assert.Contains("AAL123", result.ActiveCallsigns);
        Assert.Contains("American 123", result.Prompt);
        Assert.Contains("30-second-old", result.StatusMessage);
    }

    [Fact]
    public void Build_AddsFreshNamedStarContextToPrompt()
    {
        RecognitionContextService service = CreateService(
            Process(),
            Snapshot(Now.AddSeconds(-30), "AAL123"),
            new FakeRouteContextService(
                new EatsRouteContextData(
                    new Dictionary<string, string>
                    {
                        ["AAL123"] = "BANKR5"
                    },
                    Now.AddSeconds(-20))));

        RecognitionContextResult result = service.Build("Atlanta Center");

        Assert.Equal("BANKR5", result.ActiveStars["AAL123"]);
        Assert.Contains("BANKR five", result.Prompt);
        Assert.Contains("1 descend-via aircraft", result.StatusMessage);
    }

    [Fact]
    public void Build_DropsStaleNamedStarContextButKeepsFreshSnapshot()
    {
        RecognitionContextService service = CreateService(
            Process(),
            Snapshot(Now.AddSeconds(-30), "AAL123"),
            new FakeRouteContextService(
                new EatsRouteContextData(
                    new Dictionary<string, string>
                    {
                        ["AAL123"] = "BANKR5"
                    },
                    Now.AddMinutes(-4))));

        RecognitionContextResult result = service.Build("Atlanta Center");

        Assert.True(result.HasFreshSnapshot);
        Assert.Empty(result.ActiveStars);
        Assert.Contains("STAR context is stale", result.StatusMessage);
    }

    private static RecognitionContextService CreateService(
        EatsProcessInfo? process,
        EatsSnapshotData snapshot,
        IEatsRouteContextService? routeContextService = null)
    {
        return new RecognitionContextService(
            new FakeProcessDetector(process),
            new FakeSnapshotService(snapshot),
            Aliases,
            TimeSpan.FromMinutes(3),
            () => Now,
            routeContextService: routeContextService);
    }

    private static EatsProcessInfo Process()
    {
        return new EatsProcessInfo(
            1,
            "eATS",
            (nint)1,
            null,
            null);
    }

    private static EatsSnapshotData Snapshot(
        DateTime timestamp,
        params string[] callsigns)
    {
        return new EatsSnapshotData(callsigns, timestamp);
    }

    private sealed class FakeProcessDetector(EatsProcessInfo? result)
        : IEatsProcessDetector
    {
        public EatsProcessInfo? FindRunningInstance() => result;
    }

    private sealed class FakeSnapshotService(EatsSnapshotData result)
        : IEatsSnapshotService
    {
        public EatsSnapshotData Load() => result;
    }

    private sealed class FakeRouteContextService(EatsRouteContextData result)
        : IEatsRouteContextService
    {
        public EatsRouteContextData Load(IEnumerable<string> activeCallsigns)
        {
            return result;
        }
    }
}
