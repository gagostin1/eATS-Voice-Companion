using EatsVoiceCompanion.Core.Speech;

namespace EatsVoiceCompanion.App.Services;

public sealed record RecognitionContextResult(
    string Prompt,
    string StatusMessage,
    bool HasFreshSnapshot,
    IReadOnlySet<string> ActiveCallsigns);

public sealed class RecognitionContextService
{
    private readonly IEatsProcessDetector _processDetector;
    private readonly IEatsSnapshotService _snapshotService;
    private readonly IReadOnlyDictionary<string, string> _airlineAliases;
    private readonly TimeSpan _maximumSnapshotAge;
    private readonly Func<DateTime> _utcNow;
    private readonly AppLogger? _logger;

    public RecognitionContextService(
        IEatsProcessDetector processDetector,
        IEatsSnapshotService snapshotService,
        IReadOnlyDictionary<string, string> airlineAliases,
        TimeSpan maximumSnapshotAge,
        Func<DateTime>? utcNow = null,
        AppLogger? logger = null)
    {
        ArgumentNullException.ThrowIfNull(processDetector);
        ArgumentNullException.ThrowIfNull(snapshotService);
        ArgumentNullException.ThrowIfNull(airlineAliases);

        if (maximumSnapshotAge <= TimeSpan.Zero)
        {
            throw new ArgumentOutOfRangeException(
                nameof(maximumSnapshotAge));
        }

        _processDetector = processDetector;
        _snapshotService = snapshotService;
        _airlineAliases = airlineAliases;
        _maximumSnapshotAge = maximumSnapshotAge;
        _utcNow = utcNow ?? (() => DateTime.UtcNow);
        _logger = logger;
    }

    public RecognitionContextResult Build(string? controllerPosition)
    {
        string positionContext =
            string.IsNullOrWhiteSpace(controllerPosition)
                ? string.Empty
                : $"Controller position: {controllerPosition.Trim()}.";

        HashSet<string> activeCallsigns =
            new(StringComparer.OrdinalIgnoreCase);

        try
        {
            if (_processDetector.FindRunningInstance() is null)
            {
                return Result(
                    positionContext,
                    "eATS is not running. " +
                    "Active-aircraft context was not used.",
                    hasFreshSnapshot: false,
                    activeCallsigns);
            }

            EatsSnapshotData snapshot = _snapshotService.Load();
            TimeSpan snapshotAge = _utcNow() - snapshot.LastWriteTimeUtc;

            if (snapshotAge < TimeSpan.Zero)
            {
                snapshotAge = TimeSpan.Zero;
            }

            if (snapshotAge > _maximumSnapshotAge)
            {
                return Result(
                    positionContext,
                    $"The eATS snapshot is stale " +
                    $"({snapshotAge.TotalMinutes:F1} minutes old). " +
                    "Active-aircraft context was not used.",
                    hasFreshSnapshot: false,
                    activeCallsigns);
            }

            foreach (string callsign in snapshot.Callsigns)
            {
                activeCallsigns.Add(callsign);
            }

            if (activeCallsigns.Count == 0)
            {
                return Result(
                    positionContext,
                    "The snapshot contained no active aircraft.",
                    hasFreshSnapshot: true,
                    activeCallsigns);
            }

            string airlineContext = ActiveCallsignPromptBuilder.Build(
                activeCallsigns,
                _airlineAliases);

            if (string.IsNullOrWhiteSpace(airlineContext))
            {
                return Result(
                    positionContext,
                    $"Found {activeCallsigns.Count} active aircraft, " +
                    "but none had supported airline callsigns.",
                    hasFreshSnapshot: true,
                    activeCallsigns);
            }

            return Result(
                $"{positionContext} {airlineContext}".Trim(),
                $"Loaded {activeCallsigns.Count} active aircraft " +
                $"from a {snapshotAge.TotalSeconds:F0}-second-old snapshot. " +
                "Dynamic airline speech context is ready.",
                hasFreshSnapshot: true,
                activeCallsigns);
        }
        catch (Exception exception)
        {
            _logger?.Error(
                "RecognitionContextUnavailable",
                "Active-aircraft context could not be built.",
                exception);

            return Result(
                positionContext,
                "Active-aircraft speech context is unavailable.\n" +
                exception.Message,
                hasFreshSnapshot: false,
                activeCallsigns);
        }
    }

    private static RecognitionContextResult Result(
        string prompt,
        string status,
        bool hasFreshSnapshot,
        HashSet<string> activeCallsigns)
    {
        return new RecognitionContextResult(
            prompt,
            status,
            hasFreshSnapshot,
            activeCallsigns);
    }
}
