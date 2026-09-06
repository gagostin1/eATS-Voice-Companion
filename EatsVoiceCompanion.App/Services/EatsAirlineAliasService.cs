using System.IO;
using EatsVoiceCompanion.Core.Data;

namespace EatsVoiceCompanion.App.Services;

public sealed class EatsAirlineAliasService
{
    public EatsAirlineAliasService(string? airlineFilePath = null)
    {
        AirlineFilePath = airlineFilePath ?? Path.Combine(
            Environment.GetFolderPath(
                Environment.SpecialFolder.LocalApplicationData),
            "ATSim2020",
            "eATS",
            "Airlines.txt");
    }

    public string AirlineFilePath { get; }

    public IReadOnlyDictionary<string, string> Load()
    {
        if (!File.Exists(AirlineFilePath))
        {
            throw new FileNotFoundException(
                "The eATS airline file was not found.",
                AirlineFilePath);
        }

        IReadOnlyDictionary<string, string> aliases =
            AirlineAliasFileParser.Parse(
                File.ReadLines(AirlineFilePath));

        if (aliases.Count == 0)
        {
            throw new InvalidDataException(
                "The eATS airline file contained no usable entries.");
        }

        return aliases;
    }
}
