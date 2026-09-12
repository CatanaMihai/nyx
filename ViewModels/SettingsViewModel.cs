using Brush = System.Windows.Media.Brush;
using SolidColorBrush = System.Windows.Media.SolidColorBrush;
using Fonts = System.Windows.Media.Fonts;
using ColorConverter = System.Windows.Media.ColorConverter;
using Launcher.Models;
using Launcher.Services;
using Launcher.Themes;

namespace Launcher.ViewModels;

/// <summary>One row shown in the Settings window's live-preview mini result list.</summary>
public sealed record PreviewResultRow(string Title, bool IsSelected);

/// <summary>
/// Drives the Settings window. Holds a working copy of the active theme
/// (<see cref="_editing"/>); every property setter here pushes the change to
/// <see cref="ThemeService"/> immediately (persistName: false) so the real
/// launcher window — and this window's own live preview, which uses the
/// exact same DynamicResource keys — update in real time as the user types
/// or drags a slider. Nothing here triggers app re-indexing or service
/// restarts; only the WPF resource dictionary changes.
/// </summary>
public sealed class SettingsViewModel : ObservableObject
{
    private readonly ThemeService _themeService;
    private readonly SettingsService _settingsService;
    private readonly HotkeyService _hotkeyService;
    private ThemeDefinition _editing;

    public SettingsViewModel(ThemeService themeService, SettingsService settingsService, HotkeyService hotkeyService)
    {
        _themeService = themeService;
        _settingsService = settingsService;
        _hotkeyService = hotkeyService;
        _editing = themeService.Current.Clone();

        AvailableThemes = new List<string>(BuiltInThemes.All.Select(t => t.Name)) { BuiltInThemes.CustomName };
        _selectedThemeName = _editing.Name;

        _globalShortcut = HotkeyService.DisplayName(hotkeyService.GetShortcut("toggle") ?? settingsService.Current.GlobalShortcut);
        _settingsShortcutValue = hotkeyService.GetShortcut("settings") ?? settingsService.Current.SettingsShortcut;
        MaxResultCount = settingsService.Current.MaxResultCount;
    }

    // ----- Sidebar navigation -----

    private int _selectedSectionIndex;
    public int SelectedSectionIndex
    {
        get => _selectedSectionIndex;
        set
        {
            if (!SetField(ref _selectedSectionIndex, value)) return;
            OnPropertyChanged(nameof(IsGeneralVisible));
            OnPropertyChanged(nameof(IsAppearanceVisible));
            OnPropertyChanged(nameof(IsSearchVisible));
            OnPropertyChanged(nameof(IsAnimationsVisible));
            OnPropertyChanged(nameof(IsAboutVisible));
        }
    }

    public System.Windows.Visibility IsGeneralVisible => Vis(0);
    public System.Windows.Visibility IsAppearanceVisible => Vis(1);
    public System.Windows.Visibility IsSearchVisible => Vis(2);
    public System.Windows.Visibility IsAnimationsVisible => Vis(3);
    public System.Windows.Visibility IsAboutVisible => Vis(4);

    private System.Windows.Visibility Vis(int index) =>
        _selectedSectionIndex == index ? System.Windows.Visibility.Visible : System.Windows.Visibility.Collapsed;

    // ----- Live preview rows (3 representative sample results) -----

    public List<PreviewResultRow> PreviewResults { get; } = new()
    {
        new PreviewResultRow("Visual Studio Code", true),
        new PreviewResultRow("Spotify", false),
        new PreviewResultRow("Settings", false),
    };

    // ----- Theme selection -----

    public List<string> AvailableThemes { get; }

    private string _selectedThemeName;
    public string SelectedThemeName
    {
        get => _selectedThemeName;
        set
        {
            if (!SetField(ref _selectedThemeName, value)) return;

            var preset = BuiltInThemes.FindByName(value);
            if (preset is not null)
            {
                _editing = preset.Clone();
                ApplyLive();
                RaiseAllThemePropertiesChanged();
            }
            // "Custom" keeps whatever is currently being edited.
        }
    }

    // ----- Colors (hex, validated) -----

    public string AccentColor { get => _editing.AccentColor; set => SetColor(v => _editing.AccentColor = v, value, nameof(AccentColor), nameof(AccentColorSwatch)); }
    public Brush AccentColorSwatch => SwatchFor(_editing.AccentColor);

    public string BackgroundColor { get => _editing.BackgroundColor; set => SetColor(v => _editing.BackgroundColor = v, value, nameof(BackgroundColor), nameof(BackgroundColorSwatch)); }
    public Brush BackgroundColorSwatch => SwatchFor(_editing.BackgroundColor);

    public string PrimaryTextColor { get => _editing.PrimaryTextColor; set => SetColor(v => _editing.PrimaryTextColor = v, value, nameof(PrimaryTextColor), nameof(PrimaryTextColorSwatch)); }
    public Brush PrimaryTextColorSwatch => SwatchFor(_editing.PrimaryTextColor);

    public string SelectedBackgroundColor { get => _editing.SelectedBackgroundColor; set => SetColor(v => _editing.SelectedBackgroundColor = v, value, nameof(SelectedBackgroundColor), nameof(SelectedBackgroundColorSwatch)); }
    public Brush SelectedBackgroundColorSwatch => SwatchFor(_editing.SelectedBackgroundColor);

    public string GlowColor { get => _editing.GlowColor; set => SetColor(v => _editing.GlowColor = v, value, nameof(GlowColor), nameof(GlowColorSwatch)); }
    public Brush GlowColorSwatch => SwatchFor(_editing.GlowColor);

    public string BorderColor { get => _editing.BorderColor; set => SetColor(v => _editing.BorderColor = v, value, nameof(BorderColor), nameof(BorderColorSwatch)); }
    public Brush BorderColorSwatch => SwatchFor(_editing.BorderColor);

    /// <summary>
    /// Proxy used by the single shared color-picker popup: whichever field name is
    /// passed to <see cref="BeginEditColor"/> is what this reads/writes, so one
    /// popup instance can edit any of the six color fields above.
    /// </summary>
    private string _activeColorField = nameof(AccentColor);
    public string ActiveColorHex
    {
        get => _activeColorField switch
        {
            nameof(AccentColor) => AccentColor,
            nameof(BackgroundColor) => BackgroundColor,
            nameof(PrimaryTextColor) => PrimaryTextColor,
            nameof(SelectedBackgroundColor) => SelectedBackgroundColor,
            nameof(GlowColor) => GlowColor,
            nameof(BorderColor) => BorderColor,
            _ => AccentColor
        };
        set
        {
            switch (_activeColorField)
            {
                case nameof(AccentColor): AccentColor = value; break;
                case nameof(BackgroundColor): BackgroundColor = value; break;
                case nameof(PrimaryTextColor): PrimaryTextColor = value; break;
                case nameof(SelectedBackgroundColor): SelectedBackgroundColor = value; break;
                case nameof(GlowColor): GlowColor = value; break;
                case nameof(BorderColor): BorderColor = value; break;
            }
            OnPropertyChanged();
        }
    }

    /// <summary>Points the shared color-picker popup at a different field (e.g. "AccentColor").</summary>
    public void BeginEditColor(string fieldName)
    {
        _activeColorField = fieldName;
        OnPropertyChanged(nameof(ActiveColorHex));
    }

    // ----- Window -----

    public double BackgroundOpacityPercent
    {
        get => _editing.BackgroundOpacity * 100;
        set { _editing.BackgroundOpacity = Clamp01(value / 100.0); ApplyLive(); OnPropertyChanged(); }
    }

    public double CornerRadius
    {
        get => _editing.CornerRadius;
        set { _editing.CornerRadius = Clamp(value, 0, 40); ApplyLive(); OnPropertyChanged(); }
    }

    public double WindowWidth
    {
        get => _editing.WindowWidth;
        set { _editing.WindowWidth = Clamp(value, 480, 900); ApplyLive(); OnPropertyChanged(); }
    }

    public double BorderThickness
    {
        get => _editing.BorderThickness;
        set { _editing.BorderThickness = Clamp(value, 0, 4); ApplyLive(); OnPropertyChanged(); }
    }

    // ----- Glow -----

    public bool GlowEnabled
    {
        get => _editing.GlowEnabled;
        set { _editing.GlowEnabled = value; ApplyLive(); OnPropertyChanged(); }
    }

    public double GlowIntensityPercent
    {
        get => _editing.GlowOpacity * 100;
        set { _editing.GlowOpacity = Clamp01(value / 100.0); ApplyLive(); OnPropertyChanged(); }
    }

    public double GlowRadius
    {
        get => _editing.GlowRadius;
        set { _editing.GlowRadius = Clamp(value, 0, 60); ApplyLive(); OnPropertyChanged(); }
    }

    // ----- Results -----

    public double ResultHeight
    {
        get => _editing.ResultHeight;
        set { _editing.ResultHeight = Clamp(value, 36, 90); ApplyLive(); OnPropertyChanged(); }
    }

    public double IconSize
    {
        get => _editing.IconSize;
        set { _editing.IconSize = Clamp(value, 16, 48); ApplyLive(); OnPropertyChanged(); }
    }

    public double ResultSpacing
    {
        get => _editing.ResultSpacing;
        set { _editing.ResultSpacing = Clamp(value, 0, 16); ApplyLive(); OnPropertyChanged(); }
    }

    // ----- Search / fonts -----

    public List<string> AvailableFontFamilies { get; } =
        Fonts.SystemFontFamilies.Select(f => f.Source).OrderBy(n => n).Distinct().ToList();

    public string SearchFontFamily
    {
        get => _editing.SearchFontFamily;
        set { _editing.SearchFontFamily = string.IsNullOrWhiteSpace(value) ? "Segoe UI" : value; ApplyLive(); OnPropertyChanged(); }
    }

    public double SearchFontSize
    {
        get => _editing.SearchFontSize;
        set { _editing.SearchFontSize = Clamp(value, 14, 32); ApplyLive(); OnPropertyChanged(); }
    }

    public double ResultTitleFontSize
    {
        get => _editing.ResultTitleFontSize;
        set { _editing.ResultTitleFontSize = Clamp(value, 11, 22); ApplyLive(); OnPropertyChanged(); }
    }

    public double ResultSecondaryFontSize
    {
        get => _editing.ResultSecondaryFontSize;
        set { _editing.ResultSecondaryFontSize = Clamp(value, 9, 18); ApplyLive(); OnPropertyChanged(); }
    }

    // ----- Animation -----

    public bool AnimationsEnabled
    {
        get => _editing.AnimationsEnabled;
        set { _editing.AnimationsEnabled = value; ApplyLive(); OnPropertyChanged(); }
    }

    public double OpenDurationMs
    {
        get => _editing.OpenDurationMs;
        set { _editing.OpenDurationMs = (int)Clamp(value, 60, 400); ApplyLive(); OnPropertyChanged(); }
    }

    /// <summary>Ambient (looping, while-open) animation styles the bar itself can play.</summary>
    public List<string> AvailableAmbientStyles { get; } = new() { "None", "Pulse", "Shimmer", "Glitch" };

    public string AmbientAnimationStyle
    {
        get => _editing.AmbientAnimationStyle;
        set { _editing.AmbientAnimationStyle = value; ApplyLive(); OnPropertyChanged(); }
    }

    public double AmbientAnimationSpeedSeconds
    {
        get => _editing.AmbientAnimationSpeedSeconds;
        set { _editing.AmbientAnimationSpeedSeconds = Clamp(value, 1, 8); ApplyLive(); OnPropertyChanged(); }
    }

    // ----- General / Search (functional AppSettings, not theme) -----

    private string _globalShortcut;

    /// <summary>Display-friendly form of the toggle shortcut (e.g. "Ctrl+Space").</summary>
    public string GlobalShortcut
    {
        get => _globalShortcut;
        private set => SetField(ref _globalShortcut, value);
    }

    private string _settingsShortcutValue;
    public string SettingsShortcutDisplay
    {
        get => HotkeyService.DisplayName(_settingsShortcutValue);
        private set { }
    }

    /// <summary>
    /// Re-binds the "toggle launcher" global hotkey to <paramref name="shortcut"/>
    /// immediately (no restart) and remembers it for the next Save. Returns false
    /// (and leaves the previous binding active) if the combo can't be registered.
    /// </summary>
    public bool RebindGlobalShortcut(string shortcut)
    {
        if (!_hotkeyService.Rebind("toggle", shortcut))
            return false;

        GlobalShortcut = HotkeyService.DisplayName(shortcut);
        _settingsService.Current.GlobalShortcut = shortcut;
        _settingsService.Save();
        return true;
    }

    /// <summary>Same as <see cref="RebindGlobalShortcut"/> but for the "open Settings" hotkey.</summary>
    public bool RebindSettingsShortcut(string shortcut)
    {
        if (!_hotkeyService.Rebind("settings", shortcut))
            return false;

        _settingsShortcutValue = shortcut;
        OnPropertyChanged(nameof(SettingsShortcutDisplay));
        _settingsService.Current.SettingsShortcut = shortcut;
        _settingsService.Save();
        return true;
    }

    private int _maxResultCount;
    public int MaxResultCount
    {
        get => _maxResultCount;
        set => SetField(ref _maxResultCount, (int)Clamp(value, 3, 20));
    }

    // ----- Commands -----

    public void SaveTheme()
    {
        _editing.Name = BuiltInThemes.CustomName;
        _themeService.SaveAsCustom(_editing);
        _selectedThemeName = BuiltInThemes.CustomName;
        OnPropertyChanged(nameof(SelectedThemeName));

        _settingsService.Current.MaxResultCount = MaxResultCount;
        _settingsService.Current.CurrentThemeName = BuiltInThemes.CustomName;
        _settingsService.Save();
    }

    public void ResetToDefault()
    {
        _editing = BuiltInThemes.MidnightCyan();
        _selectedThemeName = _editing.Name;
        ApplyLive();
        RaiseAllThemePropertiesChanged();
        OnPropertyChanged(nameof(SelectedThemeName));
    }

    /// <summary>Re-applies whatever theme is currently persisted, discarding any unsaved live edits. Used when Settings closes without Save.</summary>
    public void DiscardLiveEdits(ThemeDefinition originallyActive)
    {
        _themeService.Apply(originallyActive, persistName: false);
    }

    private void ApplyLive() => _themeService.Apply(_editing.Clone(), persistName: false);

    private void SetColor(Action<string> setter, string value, string colorPropName, string swatchPropName)
    {
        // Validate before committing — an invalid hex must never corrupt the
        // working theme or crash the app; just ignore the edit until it's valid.
        try
        {
            _ = System.Windows.Media.ColorConverter.ConvertFromString(value);
        }
        catch (Exception ex)
        {
            LogService.Warn($"Ignored invalid color '{value}' entered for {colorPropName}: {ex.Message}");
            OnPropertyChanged(colorPropName); // revert the TextBox display to the last-good value
            return;
        }

        setter(value);
        ApplyLive();
        OnPropertyChanged(colorPropName);
        OnPropertyChanged(swatchPropName);
    }

    private static Brush SwatchFor(string hex)
    {
        var brush = new SolidColorBrush(ThemeService.ParseColorSafe(hex));
        brush.Freeze();
        return brush;
    }

    private void RaiseAllThemePropertiesChanged()
    {
        foreach (var name in new[]
        {
            nameof(AccentColor), nameof(AccentColorSwatch), nameof(BackgroundColor), nameof(BackgroundColorSwatch),
            nameof(PrimaryTextColor), nameof(PrimaryTextColorSwatch), nameof(SelectedBackgroundColor), nameof(SelectedBackgroundColorSwatch),
            nameof(GlowColor), nameof(GlowColorSwatch), nameof(BorderColor), nameof(BorderColorSwatch),
            nameof(BackgroundOpacityPercent), nameof(CornerRadius), nameof(WindowWidth), nameof(BorderThickness),
            nameof(GlowEnabled), nameof(GlowIntensityPercent), nameof(GlowRadius),
            nameof(ResultHeight), nameof(IconSize), nameof(ResultSpacing),
            nameof(SearchFontFamily), nameof(SearchFontSize), nameof(ResultTitleFontSize), nameof(ResultSecondaryFontSize),
            nameof(AnimationsEnabled), nameof(OpenDurationMs),
            nameof(AmbientAnimationStyle), nameof(AmbientAnimationSpeedSeconds), nameof(ActiveColorHex),
        })
        {
            OnPropertyChanged(name);
        }
    }

    private static double Clamp(double value, double min, double max) => Math.Clamp(value, min, max);
    private static double Clamp01(double value) => Math.Clamp(value, 0.0, 1.0);
}
