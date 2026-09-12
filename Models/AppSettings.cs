namespace Launcher.Models;

/// <summary>
/// Functional (non-visual) configuration for the launcher. Persisted as JSON
/// under %AppData%\Nyx\settings.json. Visual/appearance configuration
/// lives separately in <see cref="Launcher.Themes.ThemeDefinition"/> — see
/// <see cref="Launcher.Themes.ThemeService"/> — and is referenced here only
/// by name via <see cref="CurrentThemeName"/>.
/// </summary>
public sealed class AppSettings
{
    /// <summary>Modifier + key combo that toggles the launcher, e.g. "Ctrl+Space".</summary>
    public string GlobalShortcut { get; set; } = "Ctrl+Space";

    /// <summary>Modifier + key combo that opens Settings, e.g. "Ctrl+,".</summary>
    public string SettingsShortcut { get; set; } = "Ctrl+OemComma";

    /// <summary>Maximum number of results the search engine returns for a query.</summary>
    public int MaxResultCount { get; set; } = 8;

    /// <summary>Name of the active theme — a built-in preset name, or "Custom".</summary>
    public string CurrentThemeName { get; set; } = "Midnight Cyan";
}
