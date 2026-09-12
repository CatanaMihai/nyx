using System.IO;
using System.Text.Json;
using Launcher.Models;

namespace Launcher.Services;

/// <summary>
/// Loads and saves <see cref="AppSettings"/> from %AppData%\Launcher\settings.json.
/// Falls back to defaults if the file is missing or corrupt.
/// </summary>
public sealed class SettingsService
{
    private static readonly string SettingsPath = Path.Combine(
        Environment.GetFolderPath(Environment.SpecialFolder.ApplicationData),
        "Nyx", "settings.json");

    public AppSettings Current { get; private set; } = new();

    public void Load()
    {
        try
        {
            if (File.Exists(SettingsPath))
            {
                var json = File.ReadAllText(SettingsPath);
                var loaded = JsonSerializer.Deserialize<AppSettings>(json);
                if (loaded is not null)
                {
                    Current = loaded;
                    return;
                }
            }
        }
        catch (Exception ex)
        {
            LogService.Error("Failed to load settings; using defaults.", ex);
        }

        Current = new AppSettings();
    }

    public void Save()
    {
        try
        {
            var dir = Path.GetDirectoryName(SettingsPath)!;
            Directory.CreateDirectory(dir);
            var json = JsonSerializer.Serialize(Current, new JsonSerializerOptions { WriteIndented = true });
            File.WriteAllText(SettingsPath, json);
        }
        catch (Exception ex)
        {
            LogService.Error("Failed to save settings.", ex);
        }
    }
}
