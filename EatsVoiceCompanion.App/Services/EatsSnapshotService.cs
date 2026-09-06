using System.IO;
using EatsVoiceCompanion.Core.Data;

namespace EatsVoiceCompanion.App.Services;

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

    public IReadOnlyList<string> LoadCallsigns()
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

        return SnapshotCallsignParser.Parse(lines);
    }
}