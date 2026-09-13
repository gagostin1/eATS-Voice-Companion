using System.Collections.ObjectModel;
using System.IO;
using EatsVoiceCompanion.Core.Data;

namespace EatsVoiceCompanion.App.Services;

public sealed record EatsRouteContextData(
    IReadOnlyDictionary<string, string> ActiveStars,
    IReadOnlyDictionary<string, IReadOnlySet<string>> ActiveRouteFixes,
    DateTime LastWriteTimeUtc);

public interface IEatsRouteContextService
{
    EatsRouteContextData Load(IEnumerable<string> activeCallsigns);
}

public sealed class EatsRouteContextService : IEatsRouteContextService
{
    private IReadOnlyList<DescendViaProcedure>? _cachedProcedures;
    private DateTime _cachedProceduresLastWriteTimeUtc;
    private IReadOnlyList<RouteProcedure>? _cachedRouteProcedures;
    private DateTime _cachedRouteProceduresLastWriteTimeUtc;

    public EatsRouteContextService(
        string logDetailFilePath,
        string airwaysFilePath)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(logDetailFilePath);
        ArgumentException.ThrowIfNullOrWhiteSpace(airwaysFilePath);

        LogDetailFilePath = logDetailFilePath;
        AirwaysFilePath = airwaysFilePath;
    }

    public string LogDetailFilePath { get; }

    public string AirwaysFilePath { get; }

    public EatsRouteContextData Load(IEnumerable<string> activeCallsigns)
    {
        ArgumentNullException.ThrowIfNull(activeCallsigns);

        if (!File.Exists(LogDetailFilePath))
        {
            throw new FileNotFoundException(
                "The eATS detailed log was not found.",
                LogDetailFilePath);
        }

        if (!File.Exists(AirwaysFilePath))
        {
            throw new FileNotFoundException(
                "The eATS airway and procedure database was not found.",
                AirwaysFilePath);
        }

        IReadOnlyDictionary<string, GeneratedAircraftRoute> routes =
            GeneratedRouteLogParser.Parse(ReadSharedLines(LogDetailFilePath));
        IReadOnlyList<DescendViaProcedure> procedures =
            LoadProcedures();
        IReadOnlyList<RouteProcedure> routeProcedures =
            LoadRouteProcedures();
        IReadOnlyDictionary<string, string> activeStars =
            ActiveStarResolver.Resolve(
                activeCallsigns,
                routes,
                procedures);
        IReadOnlyDictionary<string, IReadOnlySet<string>> activeRouteFixes =
            ActiveRouteFixResolver.Resolve(
                activeCallsigns,
                routes,
                routeProcedures);

        return new EatsRouteContextData(
            new ReadOnlyDictionary<string, string>(
                new Dictionary<string, string>(
                    activeStars,
                    StringComparer.OrdinalIgnoreCase)),
            activeRouteFixes,
            File.GetLastWriteTimeUtc(LogDetailFilePath));
    }

    private IReadOnlyList<DescendViaProcedure> LoadProcedures()
    {
        DateTime lastWriteTimeUtc =
            File.GetLastWriteTimeUtc(AirwaysFilePath);

        if (_cachedProcedures is null ||
            lastWriteTimeUtc != _cachedProceduresLastWriteTimeUtc)
        {
            _cachedProcedures = DescendViaProcedureParser.Parse(
                ReadSharedLines(AirwaysFilePath));
            _cachedProceduresLastWriteTimeUtc = lastWriteTimeUtc;
        }

        return _cachedProcedures;
    }

    private IReadOnlyList<RouteProcedure> LoadRouteProcedures()
    {
        DateTime lastWriteTimeUtc =
            File.GetLastWriteTimeUtc(AirwaysFilePath);

        if (_cachedRouteProcedures is null ||
            lastWriteTimeUtc != _cachedRouteProceduresLastWriteTimeUtc)
        {
            _cachedRouteProcedures = RouteProcedureParser.Parse(
                ReadSharedLines(AirwaysFilePath));
            _cachedRouteProceduresLastWriteTimeUtc = lastWriteTimeUtc;
        }

        return _cachedRouteProcedures;
    }

    private static IReadOnlyList<string> ReadSharedLines(string path)
    {
        List<string> lines = new();

        using FileStream stream = new(
            path,
            FileMode.Open,
            FileAccess.Read,
            FileShare.ReadWrite | FileShare.Delete);
        using StreamReader reader = new(stream);

        while (reader.ReadLine() is { } line)
        {
            lines.Add(line);
        }

        return lines;
    }
}
