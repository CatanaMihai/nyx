namespace Launcher.Themes;

/// <summary>
/// Every visual property the launcher's UI can be styled with. Fully
/// serializable to/from JSON so it doubles as the on-disk theme-file schema
/// (%AppData%\Launcher\Themes\*.json) and the in-memory value ThemeService
/// applies to the WPF resource dictionary. Colors are stored as hex strings
/// ("#RRGGBB" or "#AARRGGBB") so they round-trip cleanly through JSON and are
/// easy for a settings UI (or, later, a hand-edited file) to work with.
///
/// This is intentionally a plain data record — it has no WPF types (Color,
/// Brush, Thickness, ...) so it can be loaded/saved without touching the UI
/// thread and so ThemeService is the single place that turns it into actual
/// WPF resources.
/// </summary>
public sealed class ThemeDefinition
{
    /// <summary>Display name; "Custom" is reserved for the user's own hand-tuned theme.</summary>
    public string Name { get; set; } = "Custom";

    // ----- Window -----
    public string BackgroundColor { get; set; } = "#1B1D22";
    public double BackgroundOpacity { get; set; } = 0.88;
    public string BorderColor { get; set; } = "#FFFFFF";
    public double BorderOpacity { get; set; } = 0.20;
    public double BorderThickness { get; set; } = 1;
    public double CornerRadius { get; set; } = 18;
    public double WindowWidth { get; set; } = 700;
    public double WindowMinHeight { get; set; } = 76;
    public double InnerPadding { get; set; } = 18;

    // ----- Accent / glow -----
    public string AccentColor { get; set; } = "#41D6C3";
    public string GlowColor { get; set; } = "#41D6C3";
    public double GlowOpacity { get; set; } = 0.22;
    public double GlowRadius { get; set; } = 24;
    public bool GlowEnabled { get; set; } = true;

    // ----- Search box -----
    public string SearchTextColor { get; set; } = "#F2F5F5";
    public string PlaceholderTextColor { get; set; } = "#6E7A79";
    public string SearchFontFamily { get; set; } = "Segoe UI";
    public double SearchFontSize { get; set; } = 22;
    public string SearchIconColor { get; set; } = "#9AA5A4";
    public double SearchBoxHeight { get; set; } = 46;

    // ----- Results -----
    public string PrimaryTextColor { get; set; } = "#F2F5F5";
    public string SecondaryTextColor { get; set; } = "#9AA5A4";
    public string CategoryTextColor { get; set; } = "#6E7A79";
    public string SelectedBackgroundColor { get; set; } = "#41D6C3";
    public double SelectedBackgroundOpacity { get; set; } = 0.15;
    public string SelectedTextColor { get; set; } = "#F2F5F5";
    public double ResultCornerRadius { get; set; } = 10;
    public double ResultHeight { get; set; } = 54;
    public double ResultSpacing { get; set; } = 2;
    public double IconSize { get; set; } = 28;

    // ----- Layout -----
    public int MaxVisibleResults { get; set; } = 8;
    public double ResultAreaSpacing { get; set; } = 8;
    public double HorizontalPadding { get; set; } = 18;
    public double VerticalPadding { get; set; } = 14;

    // ----- Fonts (results) -----
    public double ResultTitleFontSize { get; set; } = 15;
    public double ResultSecondaryFontSize { get; set; } = 12;

    // ----- Animation -----
    public int OpenDurationMs { get; set; } = 150;
    public int CloseDurationMs { get; set; } = 120;
    public int ResultSelectionDurationMs { get; set; } = 120;
    public double OpenScaleFrom { get; set; } = 0.97;
    public double OpenYOffset { get; set; } = 6;
    public bool AnimationsEnabled { get; set; } = true;

    /// <summary>Looping ambient animation played inside the bar while it's open: "None", "Pulse", "Shimmer", or "Glitch".</summary>
    public string AmbientAnimationStyle { get; set; } = "None";

    /// <summary>
    /// For "Pulse"/"Shimmer": seconds per animation cycle (lower is faster).
    /// For "Glitch": the *average* number of seconds between bursts — each one
    /// reschedules with a new randomized interval, so it never feels metronomic.
    /// </summary>
    public double AmbientAnimationSpeedSeconds { get; set; } = 3.0;

    /// <summary>Deep value copy (all properties are value types/strings, so a memberwise clone suffices).</summary>
    public ThemeDefinition Clone() => (ThemeDefinition)MemberwiseClone();
}
