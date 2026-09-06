using System.IO;
using EatsVoiceCompanion.Core.Data;

namespace EatsVoiceCompanion.App.Services;

public sealed record EatsSnapshotData(
    IReadOnlyList<string> Callsigns,
    DateTime LastWriteTimeUtc);

public sealed class EatsSnapshotService
{
    public EatsSnapshotService()
    {
        string localAppData =
            Environment.GetFolderPath(
                Environment.SpecialFolder.LocalApplicationData);

        SnapshotFilePath = Path.Combine(
            localAppData,
            "ATSim2020",
            "eATS",
            "SnapshotAuto.txt");
    }

    public string SnapshotFilePath { get; }

    public EatsSnapshotData Load()
    {
        if (!File.Exists(SnapshotFilePath))
        {
            throw new FileNotFoundException(
                "The eATS automatic snapshot was not found.",
                SnapshotFilePath);
        }

        List<string> lines = new();

        using FileStream stream = new(
            SnapshotFilePath,
            FileMode.Open,
            FileAccess.Read,
            FileShare.ReadWrite | FileShare.Delete);

        using StreamReader reader = new(stream);

        while (reader.ReadLine() is { } line)
        {
            lines.Add(line);
        }

        IReadOnlyList<string> callsigns =
            SnapshotCallsignParser.Parse(lines);

        DateTime lastWriteTimeUtc =
            File.GetLastWriteTimeUtc(SnapshotFilePath);

        return new EatsSnapshotData(
            callsigns,
            lastWriteTimeUtc);
    }
}