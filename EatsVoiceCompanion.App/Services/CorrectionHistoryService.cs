using System.IO;
using System.Text.Json;
using System.Text.Json.Serialization;

namespace EatsVoiceCompanion.App.Services;

public sealed class CorrectionHistoryService
{
    private const int MaximumEntries = 500;

    private static readonly JsonSerializerOptions SerializerOptions =
        new()
        {
            WriteIndented = true,
            Converters =
            {
                new JsonStringEnumConverter()
            }
        };

    public CorrectionHistoryService(string? historyDirectory = null)
    {
        HistoryDirectory = historyDirectory ?? Path.Combine(
            Environment.GetFolderPath(
                Environment.SpecialFolder.LocalApplicationData),
            "EatsVoiceCompanion",
            "CorrectionHistory");
    }

    public string HistoryDirectory { get; }

    public IReadOnlyList<CorrectionHistoryEntry> Load()
    {
        if (!Directory.Exists(HistoryDirectory))
        {
            return [];
        }

        List<CorrectionHistoryEntry> entries = [];

        foreach (string path in Directory.EnumerateFiles(
                     HistoryDirectory,
                     "*.json",
                     SearchOption.TopDirectoryOnly))
        {
            try
            {
                CorrectionHistoryEntry? entry =
                    JsonSerializer.Deserialize<CorrectionHistoryEntry>(
                        File.ReadAllText(path),
                        SerializerOptions);

                if (entry is not null && entry.Id != Guid.Empty)
                {
                    entries.Add(entry);
                }
            }
            catch (JsonException)
            {
                // One damaged entry must not hide the rest of the history.
            }
            catch (IOException)
            {
                // A temporarily unavailable entry is skipped until refresh.
            }
            catch (UnauthorizedAccessException)
            {
                // A temporarily unavailable entry is skipped until refresh.
            }
        }

        return entries
            .OrderByDescending(entry => entry.RecordedAtUtc)
            .Take(MaximumEntries)
            .ToArray();
    }

    public void Save(CorrectionHistoryEntry entry)
    {
        ArgumentNullException.ThrowIfNull(entry);

        if (entry.Id == Guid.Empty)
        {
            throw new ArgumentException(
                "A correction-history entry requires an identifier.",
                nameof(entry));
        }

        Directory.CreateDirectory(HistoryDirectory);

        string path = GetEntryPath(entry.Id);
        string temporaryPath = path + ".tmp";

        try
        {
            File.WriteAllText(
                temporaryPath,
                JsonSerializer.Serialize(entry, SerializerOptions));
            File.Move(temporaryPath, path, overwrite: true);
        }
        finally
        {
            TryDelete(temporaryPath);
        }

        TrimExcessEntries();
    }

    public void ExportRegressionCase(
        CorrectionHistoryEntry entry,
        string destinationPath,
        string appVersion)
    {
        ArgumentNullException.ThrowIfNull(entry);
        ArgumentException.ThrowIfNullOrWhiteSpace(destinationPath);
        ArgumentException.ThrowIfNullOrWhiteSpace(appVersion);

        if (entry.ReviewStatus == CorrectionReviewStatus.Unreviewed ||
            string.IsNullOrWhiteSpace(entry.ExpectedCommand))
        {
            throw new InvalidOperationException(
                "Review the attempt before exporting a regression case.");
        }

        string callsign = entry.ExpectedCommand.Split(
            ' ',
            StringSplitOptions.RemoveEmptyEntries)[0];
        entry.ActiveStars.TryGetValue(callsign, out string? activeStar);
        string[] routeFixes = entry.ActiveRouteFixes.TryGetValue(
            callsign,
            out string[]? fixes)
            ? fixes
            : [];

        CorrectionRegressionCase regressionCase = new(
            SchemaVersion: 1,
            AppVersion: appVersion,
            RecordedAtUtc: entry.RecordedAtUtc,
            OriginalTranscript: entry.OriginalTranscript,
            GeneratedCommand: entry.GeneratedCommand,
            WasBestEffort: entry.WasBestEffort,
            CorrectedTranscript:
                entry.CorrectedTranscript ?? entry.OriginalTranscript,
            ExpectedCommand: entry.ExpectedCommand,
            ControllerPosition: entry.ControllerPosition,
            ActiveStar: activeStar,
            RouteFixes: routeFixes);

        string fullPath = Path.GetFullPath(destinationPath);
        string? directory = Path.GetDirectoryName(fullPath);

        if (directory is null)
        {
            throw new InvalidOperationException(
                "The export directory could not be determined.");
        }

        Directory.CreateDirectory(directory);
        File.WriteAllText(
            fullPath,
            JsonSerializer.Serialize(regressionCase, SerializerOptions));
    }

    private string GetEntryPath(Guid id) =>
        Path.Combine(HistoryDirectory, $"{id:N}.json");

    private void TrimExcessEntries()
    {
        FileInfo[] files = new DirectoryInfo(HistoryDirectory)
            .GetFiles("*.json", SearchOption.TopDirectoryOnly)
            .OrderByDescending(file => file.LastWriteTimeUtc)
            .ToArray();

        foreach (FileInfo file in files.Skip(MaximumEntries))
        {
            TryDelete(file.FullName);
        }
    }

    private static void TryDelete(string path)
    {
        try
        {
            if (File.Exists(path))
            {
                File.Delete(path);
            }
        }
        catch (IOException)
        {
            // Cleanup is best effort.
        }
        catch (UnauthorizedAccessException)
        {
            // Cleanup is best effort.
        }
    }
}
