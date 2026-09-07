using EatsVoiceCompanion.Core.Speech;

namespace EatsVoiceCompanion.App.Services;

public sealed record RecognitionContextResult(
    string Prompt,
    string StatusMessage,
    bool HasFreshSnapshot,
    IReadOnlySet<string> ActiveCallsigns,
    IReadOnlyDictionary<string, string> ActiveStars);

public sealed class RecognitionContextService
{
    private readonly IEatsProcessDetector _processDetector;
    private readonly IEatsSnapshotService _snapshotService;
    private readonly IReadOnlyDictionary<string, string> _airlineAliases;
    private readonly IEatsRouteContextService? _routeContextService;
    private readonly TimeSpan _maximumSnapshotAge;
    private readonly Func<DateTime> _utcNow;
    private readonly AppLogger? _logger;

    public RecognitionContextService(
        IEatsProcessDetector processDetector,
        IEatsSnapshotService snapshotService,
        IReadOnlyDictionary<string, string> airlineAliases,
        TimeSpan maximumSnapshotAge,
        Func<DateTime>? utcNow = null,
        AppLogger? logger = null,
        IEatsRouteContextService? routeContextService = null)
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
        _routeContextService = routeContextService;
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
        IReadOnlyDictionary<string, string> activeStars =
            EmptyActiveStars();

        try
        {
            if (_processDetector.FindRunningInstance() is null)
            {
                return Result(
                    positionContext,
                    "eATS is not running. " +
                    "Active-aircraft context was not used.",
                    hasFreshSnapshot: false,
                    activeCallsigns,
                    activeStars);
            }

            EatsSnapshotData snapshot = _snapshotService.Load();
            TimeSpan snapshotAge = _utcNow() - snapshot.LastWriteTimeUtc;

            if (snapshotAge < TimeSpan.Zero)
            {
                snapshotAge = TimeSpan.Zero;
            }

            foreach (string callsign in snapshot.Callsigns)
            {
                activeCallsigns.Add(callsign);
            }

            if (snapshotAge > _maximumSnapshotAge)
            {
                if (activeCallsigns.Count > 0 &&
                    _routeContextService is not null)
                {
                    (activeStars, _) = LoadRouteContext(activeCallsigns);
                }

                string staleAirlineContext =
                    ActiveCallsignPromptBuilder.Build(
                        activeCallsigns,
                        _airlineAliases);
                string staleStarPrompt = BuildStarPrompt(
                    activeStars.Values);

                return Result(
                    ($"{positionContext} {staleAirlineContext} " +
                     staleStarPrompt).Trim(),
                    $"The eATS snapshot is stale " +
                    $"({snapshotAge.TotalMinutes:F1} minutes old). " +
                    "Its aircraft context may be used only for " +
                    "best-effort speech interpretation; staging " +
                    "remains disabled.",
                    hasFreshSnapshot: false,
                    activeCallsigns,
                    activeStars);
            }

            if (activeCallsigns.Count == 0)
            {
                return Result(
                    positionContext,
                    "The snapshot contained no active aircraft.",
                    hasFreshSnapshot: true,
                    activeCallsigns,
                    activeStars);
            }

            string routeStatus = string.Empty;

            if (_routeContextService is not null)
            {
                (activeStars, routeStatus) = LoadRouteContext(
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
                    activeCallsigns,
                    activeStars);
            }

            string starPrompt = BuildStarPrompt(activeStars.Values);

            return Result(
                $"{positionContext} {airlineContext} {starPrompt}".Trim(),
                $"Loaded {activeCallsigns.Count} active aircraft " +
                $"from a {snapshotAge.TotalSeconds:F0}-second-old snapshot. " +
                "Dynamic airline speech context is ready." +
                routeStatus,
                hasFreshSnapshot: true,
                activeCallsigns,
                activeStars);
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
                activeCallsigns,
                activeStars);
        }
    }

    private (
        IReadOnlyDictionary<string, string> ActiveStars,
        string Status) LoadRouteContext(HashSet<string> activeCallsigns)
    {
        try
        {
            EatsRouteContextData routes =
                _routeContextService!.Load(activeCallsigns);
            TimeSpan routeAge = _utcNow() - routes.LastWriteTimeUtc;

            if (routeAge < TimeSpan.Zero)
            {
                routeAge = TimeSpan.Zero;
            }

            if (routeAge > _maximumSnapshotAge)
            {
                return (
                    EmptyActiveStars(),
                    $" Named STAR context is stale " +
                    $"({routeAge.TotalMinutes:F1} minutes old).");
            }

            return (
                routes.ActiveStars,
                $" Verified route context for " +
                $"{routes.ActiveStars.Count} descend-via aircraft.");
        }
        catch (Exception exception)
        {
            _logger?.Warning(
                "RouteContextUnavailable",
                "Named STAR route context could not be built.",
                new { ExceptionType = exception.GetType().Name });

            return (
                EmptyActiveStars(),
                " Named STAR context is unavailable.");
        }
    }

    private static string BuildStarPrompt(IEnumerable<string> starNames)
    {
        string[] phrases = starNames
            .Distinct(StringComparer.OrdinalIgnoreCase)
            .OrderBy(name => name, StringComparer.OrdinalIgnoreCase)
            .Select(StarNameNormalizer.ToPromptPhrase)
            .ToArray();

        return phrases.Length == 0
            ? string.Empty
            : "Active arrival procedures: " +
              string.Join(". ", phrases) + ".";
    }

    private static IReadOnlyDictionary<string, string> EmptyActiveStars()
    {
        return new Dictionary<string, string>(
            StringComparer.OrdinalIgnoreCase);
    }

    private static RecognitionContextResult Result(
        string prompt,
        string status,
        bool hasFreshSnapshot,
        HashSet<string> activeCallsigns,
        IReadOnlyDictionary<string, string> activeStars)
    {
        return new RecognitionContextResult(
            prompt,
            status,
            hasFreshSnapshot,
            activeCallsigns,
            activeStars);
    }
}
