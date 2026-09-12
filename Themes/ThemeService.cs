using System.IO;
using System.Text.Json;
using System.Windows;
using Color = System.Windows.Media.Color;
using ColorConverter = System.Windows.Media.ColorConverter;
using SolidColorBrush = System.Windows.Media.SolidColorBrush;
using FontFamily = System.Windows.Media.FontFamily;
using Launcher.Services;

namespace Launcher.Themes;

/// <summary>
/// Owns the live theme: loads it at startup, applies it to WPF's resource
/// dictionary (so every window using DynamicResource updates instantly), and
/// persists user edits. Applying a theme only ever touches
/// <see cref="Application.Resources"/> — it never rebuilds the search index,
/// restarts services, or recreates windows.
/// </summary>
public sealed class ThemeService
{
    private static readonly string ThemesDirectory = Path.Combine(
        Environment.GetFolderPath(Environment.SpecialFolder.ApplicationData),
        "Nyx", "Themes");

    /// <summary>The theme currently applied to the running app.</summary>
    public ThemeDefinition Current { get; private set; } = BuiltInThemes.MidnightCyan();

    /// <summary>Raised after a theme has been applied to resources, so open windows (e.g. Settings) can refresh any non-resource-bound UI.</summary>
    public event Action<ThemeDefinition>? ThemeChanged;

    /// <summary>
    /// Loads the theme named by <paramref name="themeName"/> (a built-in preset,
    /// or a file under the Themes folder) and applies it. Falls back to
    /// Midnight Cyan — and never throws — if anything about the requested
    /// theme is missing or malformed, so a corrupt theme file can never
    /// prevent the launcher from starting.
    /// </summary>
    public void Initialize(string? themeName)
    {
        EnsureThemesDirectoryHasBuiltIns();

        var theme = TryLoadByName(themeName) ?? BuiltInThemes.MidnightCyan();
        Apply(theme, persistName: false);
    }

    /// <summary>Returns every theme available to pick from: built-ins plus anything in the Themes folder (custom or edited-built-in files).</summary>
    public List<ThemeDefinition> GetAvailableThemes()
    {
        var byName = new Dictionary<string, ThemeDefinition>(StringComparer.OrdinalIgnoreCase);

        foreach (var builtIn in BuiltInThemes.All)
            byName[builtIn.Name] = builtIn;

        try
        {
            if (Directory.Exists(ThemesDirectory))
            {
                foreach (var file in Directory.EnumerateFiles(ThemesDirectory, "*.json"))
                {
                    var loaded = TryLoadFile(file);
                    if (loaded is not null)
                        byName[loaded.Name] = loaded;
                }
            }
        }
        catch (Exception ex)
        {
            LogService.Error("Failed to enumerate custom themes.", ex);
        }

        // Always offer a "Custom" slot, even before the user has saved one,
        // seeded from whatever theme is currently active.
        if (!byName.ContainsKey(BuiltInThemes.CustomName))
            byName[BuiltInThemes.CustomName] = Current.Clone() is { } c ? WithName(c, BuiltInThemes.CustomName) : BuiltInThemes.MidnightCyan();

        return byName.Values.ToList();
    }

    /// <summary>Applies <paramref name="theme"/> immediately and (by default) records it as the active theme in settings.</summary>
    public void Apply(ThemeDefinition theme, bool persistName = true)
    {
        try
        {
            PushToResources(theme);
            Current = theme;
        }
        catch (Exception ex)
        {
            LogService.Error($"Failed to apply theme '{theme.Name}'; falling back to Midnight Cyan.", ex);
            var fallback = BuiltInThemes.MidnightCyan();
            PushToResources(fallback);
            Current = fallback;
        }

        if (persistName)
            OnThemeAppliedForPersistence?.Invoke(Current.Name);

        ThemeChanged?.Invoke(Current);
    }

    /// <summary>Set by App.xaml.cs so applying a theme also updates+saves AppSettings.CurrentThemeName.</summary>
    public Action<string>? OnThemeAppliedForPersistence { get; set; }

    /// <summary>Saves <paramref name="theme"/> as the user's "Custom" theme and applies it.</summary>
    public void SaveAsCustom(ThemeDefinition theme)
    {
        var custom = WithName(theme.Clone(), BuiltInThemes.CustomName);
        SaveToFile(custom);
        Apply(custom);
    }

    /// <summary>Resets to a built-in preset by name (defaults to Midnight Cyan) and applies it.</summary>
    public void ResetToBuiltIn(string name = BuiltInThemes.MidnightCyanName)
    {
        var theme = BuiltInThemes.FindByName(name) ?? BuiltInThemes.MidnightCyan();
        Apply(theme);
    }

    private ThemeDefinition? TryLoadByName(string? name)
    {
        if (string.IsNullOrWhiteSpace(name))
            return null;

        var builtIn = BuiltInThemes.FindByName(name);

        // A file on disk (including a user-edited copy of a built-in name)
        // takes precedence over the compiled-in preset.
        var path = PathForTheme(name);
        var fromFile = File.Exists(path) ? TryLoadFile(path) : null;

        return fromFile ?? builtIn;
    }

    private static ThemeDefinition? TryLoadFile(string path)
    {
        try
        {
            var json = File.ReadAllText(path);
            var theme = JsonSerializer.Deserialize<ThemeDefinition>(json);
            if (theme is null || string.IsNullOrWhiteSpace(theme.Name))
                return null;
            return theme;
        }
        catch (Exception ex)
        {
            LogService.Error($"Failed to load theme file '{path}'; ignoring it.", ex);
            return null;
        }
    }

    private static void SaveToFile(ThemeDefinition theme)
    {
        try
        {
            Directory.CreateDirectory(ThemesDirectory);
            var json = JsonSerializer.Serialize(theme, new JsonSerializerOptions { WriteIndented = true });
            File.WriteAllText(PathForTheme(theme.Name), json);
        }
        catch (Exception ex)
        {
            LogService.Error($"Failed to save theme '{theme.Name}'.", ex);
        }
    }

    private static void EnsureThemesDirectoryHasBuiltIns()
    {
        try
        {
            Directory.CreateDirectory(ThemesDirectory);
            foreach (var theme in BuiltInThemes.All)
            {
                var path = PathForTheme(theme.Name);
                if (!File.Exists(path))
                    SaveToFile(theme);
            }
        }
        catch (Exception ex)
        {
            LogService.Error("Failed to seed built-in theme files.", ex);
        }
    }

    private static string PathForTheme(string name)
    {
        var safeName = string.Join("_", name.Split(Path.GetInvalidFileNameChars()));
        return Path.Combine(ThemesDirectory, safeName + ".json");
    }

    private static ThemeDefinition WithName(ThemeDefinition theme, string name)
    {
        theme.Name = name;
        return theme;
    }

    /// <summary>
    /// Pushes every visual property of <paramref name="t"/> into
    /// Application.Current.Resources under stable, semantic keys. MainWindow
    /// (and any future window) consumes these via DynamicResource, so this is
    /// the ONLY place that needs to change to add a new themeable property.
    /// </summary>
    private static void PushToResources(ThemeDefinition t)
    {
        var res = System.Windows.Application.Current.Resources;

        SetBrush(res, "ThemeWindowBackgroundBrush", t.BackgroundColor, t.BackgroundOpacity);
        SetBrush(res, "ThemeWindowBorderBrush", t.BorderColor, t.BorderOpacity);
        res["ThemeWindowBorderThickness"] = new Thickness(t.BorderThickness);
        res["ThemeWindowCornerRadius"] = new CornerRadius(t.CornerRadius);
        res["ThemeWindowWidth"] = t.WindowWidth;
        res["ThemeWindowMinHeight"] = t.WindowMinHeight;
        res["ThemeSearchRowMargin"] = new Thickness(t.HorizontalPadding, t.VerticalPadding, t.HorizontalPadding, t.VerticalPadding * 0.7);

        SetColor(res, "ThemeGlowColor", t.GlowColor);
        res["ThemeGlowOpacity"] = t.GlowEnabled ? t.GlowOpacity : 0.0;
        res["ThemeGlowRadius"] = t.GlowRadius;
        res["ThemeGlowMargin"] = new Thickness(ComputeGlowMargin(t));

        SetBrush(res, "ThemeAccentBrush", t.AccentColor, 1.0);
        SetBrush(res, "ThemeSearchTextBrush", t.SearchTextColor, 1.0);
        SetBrush(res, "ThemePlaceholderTextBrush", t.PlaceholderTextColor, 1.0);
        SetBrush(res, "ThemeSearchIconBrush", t.SearchIconColor, 1.0);
        res["ThemeSearchFontFamily"] = new FontFamily(string.IsNullOrWhiteSpace(t.SearchFontFamily) ? "Segoe UI" : t.SearchFontFamily);
        res["ThemeSearchFontSize"] = t.SearchFontSize;
        res["ThemeSearchBoxHeight"] = t.SearchBoxHeight;

        SetBrush(res, "ThemePrimaryTextBrush", t.PrimaryTextColor, 1.0);
        SetBrush(res, "ThemeSecondaryTextBrush", t.SecondaryTextColor, 1.0);
        SetBrush(res, "ThemeCategoryTextBrush", t.CategoryTextColor, 1.0);
        SetBrush(res, "ThemeSelectedBackgroundBrush", t.SelectedBackgroundColor, t.SelectedBackgroundOpacity);
        SetBrush(res, "ThemeSelectedTextBrush", t.SelectedTextColor, 1.0);
        SetBrush(res, "ThemeResultHoverBrush", t.AccentColor, 0.08);
        SetBrush(res, "ThemeDividerBrush", t.BorderColor, 0.10);
        SetBrush(res, "ThemeCategoryChipBrush", t.PrimaryTextColor, 0.08);

        res["ThemeResultCornerRadius"] = new CornerRadius(t.ResultCornerRadius);
        res["ThemeResultHeight"] = t.ResultHeight;
        res["ThemeResultMargin"] = new Thickness(6, t.ResultSpacing / 2.0, 6, t.ResultSpacing / 2.0);
        res["ThemeIconSize"] = t.IconSize;
        res["ThemeResultTitleFontSize"] = t.ResultTitleFontSize;
        res["ThemeResultSecondaryFontSize"] = t.ResultSecondaryFontSize;

        res["ThemeMaxVisibleResults"] = t.MaxVisibleResults;
        res["ThemeResultsMaxHeight"] = t.MaxVisibleResults * (t.ResultHeight + t.ResultSpacing);
        res["ThemeResultAreaSpacing"] = new Thickness(0, t.ResultAreaSpacing, 0, t.VerticalPadding * 0.7);
    }

    private static void SetBrush(ResourceDictionary res, string key, string hex, double opacity)
    {
        var color = ParseColorSafe(hex);
        var brush = new SolidColorBrush(color) { Opacity = Math.Clamp(opacity, 0.0, 1.0) };
        brush.Freeze();
        res[key] = brush;
    }

    private static void SetColor(ResourceDictionary res, string key, string hex) =>
        res[key] = ParseColorSafe(hex);

    /// <summary>
    /// Transparent padding (DIPs) reserved around the visible rounded panel so the
    /// glow's blur has room to render without being clipped at the window edge.
    /// Shared by the resource push (for the Border's Margin) and MainWindow's
    /// code-behind (which must add 2x this to the Window's total Width).
    /// </summary>
    public static double ComputeGlowMargin(ThemeDefinition t) => Math.Max(20, t.GlowEnabled ? t.GlowRadius : 20);

    /// <summary>Parses a "#RRGGBB"/"#AARRGGBB" hex color, falling back to a visible neutral gray on any invalid input rather than throwing.</summary>
    public static Color ParseColorSafe(string? hex)
    {
        if (!string.IsNullOrWhiteSpace(hex))
        {
            try
            {
                var converted = ColorConverter.ConvertFromString(hex);
                if (converted is Color color)
                    return color;
            }
            catch (Exception ex)
            {
                LogService.Warn($"Invalid theme color '{hex}'; using fallback gray. ({ex.Message})");
            }
        }

        return Color.FromRgb(0x80, 0x80, 0x80);
    }
}
