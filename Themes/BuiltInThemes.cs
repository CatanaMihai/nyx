namespace Launcher.Themes;

/// <summary>
/// Tasteful, curated theme presets shipped with the launcher. Names describe
/// a visual style only — no third-party branding or copyrighted assets are
/// referenced or reproduced.
/// </summary>
public static class BuiltInThemes
{
    public const string MidnightCyanName = "Midnight Cyan";
    public const string ObsidianName = "Obsidian";
    public const string NordName = "Nord";
    public const string DraculaName = "Dracula";
    public const string LightName = "Light";
    public const string CyberpunkName = "Cyberpunk";
    public const string CustomName = "Custom";

    /// <summary>The launcher's original look — near-black, subtle cyan accent/glow.</summary>
    public static ThemeDefinition MidnightCyan() => new()
    {
        Name = MidnightCyanName,
        BackgroundColor = "#1B1D22",
        BackgroundOpacity = 0.88,
        BorderColor = "#FFFFFF",
        BorderOpacity = 0.20,
        AccentColor = "#41D6C3",
        GlowColor = "#41D6C3",
        GlowOpacity = 0.22,
        GlowRadius = 24,
        GlowEnabled = true,
        SearchTextColor = "#F2F5F5",
        PlaceholderTextColor = "#6E7A79",
        SearchIconColor = "#9AA5A4",
        PrimaryTextColor = "#F2F5F5",
        SecondaryTextColor = "#9AA5A4",
        CategoryTextColor = "#6E7A79",
        SelectedBackgroundColor = "#41D6C3",
        SelectedBackgroundOpacity = 0.15,
        SelectedTextColor = "#F2F5F5",
    };

    /// <summary>Almost black, white/grey UI, essentially no glow — extremely minimal.</summary>
    public static ThemeDefinition Obsidian() => new()
    {
        Name = ObsidianName,
        BackgroundColor = "#121212",
        BackgroundOpacity = 0.92,
        BorderColor = "#FFFFFF",
        BorderOpacity = 0.12,
        AccentColor = "#D8D8D8",
        GlowColor = "#FFFFFF",
        GlowOpacity = 0.06,
        GlowRadius = 14,
        GlowEnabled = false,
        SearchTextColor = "#EDEDED",
        PlaceholderTextColor = "#6B6B6B",
        SearchIconColor = "#8C8C8C",
        PrimaryTextColor = "#EDEDED",
        SecondaryTextColor = "#8C8C8C",
        CategoryTextColor = "#5C5C5C",
        SelectedBackgroundColor = "#D8D8D8",
        SelectedBackgroundOpacity = 0.10,
        SelectedTextColor = "#FFFFFF",
    };

    /// <summary>Dark navy/blue-grey with a pale blue accent and soft muted text.</summary>
    public static ThemeDefinition Nord() => new()
    {
        Name = NordName,
        BackgroundColor = "#2E3440",
        BackgroundOpacity = 0.90,
        BorderColor = "#88C0D0",
        BorderOpacity = 0.22,
        AccentColor = "#88C0D0",
        GlowColor = "#88C0D0",
        GlowOpacity = 0.18,
        GlowRadius = 22,
        GlowEnabled = true,
        SearchTextColor = "#ECEFF4",
        PlaceholderTextColor = "#7B88A1",
        SearchIconColor = "#ABB6C7",
        PrimaryTextColor = "#E5E9F0",
        SecondaryTextColor = "#9AA6B8",
        CategoryTextColor = "#7B88A1",
        SelectedBackgroundColor = "#88C0D0",
        SelectedBackgroundOpacity = 0.16,
        SelectedTextColor = "#ECEFF4",
    };

    /// <summary>Dark purple-grey with a purple/pink accent and a slightly stronger selection color.</summary>
    public static ThemeDefinition Dracula() => new()
    {
        Name = DraculaName,
        BackgroundColor = "#282A36",
        BackgroundOpacity = 0.90,
        BorderColor = "#BD93F9",
        BorderOpacity = 0.24,
        AccentColor = "#BD93F9",
        GlowColor = "#FF79C6",
        GlowOpacity = 0.22,
        GlowRadius = 24,
        GlowEnabled = true,
        SearchTextColor = "#F8F8F2",
        PlaceholderTextColor = "#8C8FA3",
        SearchIconColor = "#BD93F9",
        PrimaryTextColor = "#F8F8F2",
        SecondaryTextColor = "#9EA1B8",
        CategoryTextColor = "#7C7F94",
        SelectedBackgroundColor = "#BD93F9",
        SelectedBackgroundOpacity = 0.22,
        SelectedTextColor = "#FFFFFF",
    };

    /// <summary>Translucent off-white background, dark text, subtle shadow, blue accent, no aggressive glow.</summary>
    public static ThemeDefinition Light() => new()
    {
        Name = LightName,
        BackgroundColor = "#FAFAFA",
        BackgroundOpacity = 0.92,
        BorderColor = "#000000",
        BorderOpacity = 0.08,
        AccentColor = "#2F80ED",
        GlowColor = "#8C8C8C",
        GlowOpacity = 0.12,
        GlowRadius = 16,
        GlowEnabled = true,
        SearchTextColor = "#1A1A1A",
        PlaceholderTextColor = "#8A8A8A",
        SearchIconColor = "#6B6B6B",
        PrimaryTextColor = "#1A1A1A",
        SecondaryTextColor = "#6B6B6B",
        CategoryTextColor = "#9A9A9A",
        SelectedBackgroundColor = "#2F80ED",
        SelectedBackgroundOpacity = 0.12,
        SelectedTextColor = "#0F1720",
    };

    /// <summary>
    /// Deep near-black with a hot cyan/magenta neon clash, a strong glow, and
    /// the ambient "Glitch" pattern enabled by default — irregular signal-jitter
    /// bursts rather than a smooth sweep, which reads as far more "cyberpunk" —
    /// the one deliberately flashy preset, in contrast to the otherwise-restrained
    /// defaults above.
    /// </summary>
    public static ThemeDefinition Cyberpunk() => new()
    {
        Name = CyberpunkName,
        BackgroundColor = "#0A0714",
        BackgroundOpacity = 0.86,
        BorderColor = "#00F0FF",
        BorderOpacity = 0.45,
        BorderThickness = 1.5,
        CornerRadius = 14,
        AccentColor = "#00F0FF",
        GlowColor = "#FF2AD4",
        GlowOpacity = 0.42,
        GlowRadius = 34,
        GlowEnabled = true,
        SearchTextColor = "#E8FFFC",
        PlaceholderTextColor = "#6E5A8C",
        SearchIconColor = "#00F0FF",
        PrimaryTextColor = "#E8FFFC",
        SecondaryTextColor = "#B18CFF",
        CategoryTextColor = "#FF2AD4",
        SelectedBackgroundColor = "#FF2AD4",
        SelectedBackgroundOpacity = 0.26,
        SelectedTextColor = "#FFFFFF",
        AmbientAnimationStyle = "Glitch",
        AmbientAnimationSpeedSeconds = 2.5,
    };

    /// <summary>All built-in (non-custom) presets, in display order.</summary>
    public static IReadOnlyList<ThemeDefinition> All => new[]
    {
        MidnightCyan(),
        Obsidian(),
        Nord(),
        Dracula(),
        Light(),
        Cyberpunk(),
    };

    /// <summary>Resolves a built-in preset by name, or null if <paramref name="name"/> isn't one (e.g. "Custom").</summary>
    public static ThemeDefinition? FindByName(string name) => name switch
    {
        MidnightCyanName => MidnightCyan(),
        ObsidianName => Obsidian(),
        NordName => Nord(),
        DraculaName => Dracula(),
        LightName => Light(),
        CyberpunkName => Cyberpunk(),
        _ => null
    };
}
