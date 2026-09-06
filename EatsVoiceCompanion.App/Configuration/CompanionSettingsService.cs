using System.IO;
using System.Text.Json;

namespace EatsVoiceCompanion.App.Configuration;

public sealed class CompanionSettingsService
{
    private static readonly JsonSerializerOptions SerializerOptions =
        new()
        {
            WriteIndented = true
        };

    public CompanionSettingsService(string? settingsFilePath = null)
    {
        SettingsFilePath = settingsFilePath ?? Path.Combine(
            Environment.GetFolderPath(
                Environment.SpecialFolder.LocalApplicationData),
            "EatsVoiceCompanion",
            "settings.json");
    }

    public string SettingsFilePath { get; }

    public CompanionSettings Load()
    {
        if (!File.Exists(SettingsFilePath))
        {
            return new CompanionSettings();
        }

        string json = File.ReadAllText(SettingsFilePath);

        CompanionSettings settings =
            JsonSerializer.Deserialize<CompanionSettings>(
                json,
                SerializerOptions) ??
            throw new InvalidDataException(
                "The settings file did not contain valid settings.");

        settings.Validate();
        return settings;
    }

    public void Save(CompanionSettings settings)
    {
        ArgumentNullException.ThrowIfNull(settings);
        settings.Validate();

        string? directory = Path.GetDirectoryName(SettingsFilePath);

        if (directory is null)
        {
            throw new InvalidOperationException(
                "The settings directory could not be determined.");
        }

        Directory.CreateDirectory(directory);

        string temporaryPath = SettingsFilePath + ".tmp";

        try
        {
            File.WriteAllText(
                temporaryPath,
                JsonSerializer.Serialize(settings, SerializerOptions));

            File.Move(
                temporaryPath,
                SettingsFilePath,
                overwrite: true);
        }
        finally
        {
            try
            {
                if (File.Exists(temporaryPath))
                {
                    File.Delete(temporaryPath);
                }
            }
            catch (IOException)
            {
                // Preserve the original save error if cleanup also fails.
            }
            catch (UnauthorizedAccessException)
            {
                // Preserve the original save error if cleanup also fails.
            }
        }
    }
}
