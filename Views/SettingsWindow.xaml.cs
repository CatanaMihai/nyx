using System.Windows;
using KeyEventArgs = System.Windows.Input.KeyEventArgs;
using MouseButtonEventArgs = System.Windows.Input.MouseButtonEventArgs;
using Button = System.Windows.Controls.Button;
using Color = System.Windows.Media.Color;
using System.Windows.Controls;
using System.Windows.Input;
using System.Windows.Media;
using Launcher.Services;
using Launcher.Themes;
using Launcher.ViewModels;

namespace Launcher.Views;

/// <summary>
/// Settings window: a sidebar-navigated panel (General/Appearance/Search/
/// Animations/About) whose Appearance tab edits the active theme live. Every
/// edit is pushed to <see cref="ThemeService"/> immediately, so both this
/// window's preview panel and the real MainWindow (if visible) update in
/// real time — see <see cref="SettingsViewModel"/> for how.
///
/// Also owns two pieces of "no mouse required, no XAML markup for it" UI:
/// the shortcut recorder boxes (General tab) and the shared color-picker
/// popup (Appearance tab) that every color swatch button opens.
/// </summary>
public partial class SettingsWindow : Window
{
    private readonly SettingsViewModel _viewModel;
    private readonly ThemeService _themeService;
    private ThemeDefinition _lastSavedTheme;

    /// <summary>Which shortcut is currently being re-recorded ("toggle"/"settings"), or null.</summary>
    private string? _recordingTarget;
    private string _recordingOriginalText = "";

    public SettingsWindow(ThemeService themeService, SettingsService settingsService, HotkeyService hotkeyService)
    {
        InitializeComponent();

        _themeService = themeService;
        _lastSavedTheme = themeService.Current.Clone();

        _viewModel = new SettingsViewModel(themeService, settingsService, hotkeyService);
        DataContext = _viewModel;

        PreviewKeyDown += OnPreviewKeyDown;
        Closing += OnClosing;
    }

    private void OnTitleBarMouseDown(object sender, MouseButtonEventArgs e)
    {
        if (e.ChangedButton == MouseButton.Left)
            DragMove();
    }

    private void OnPreviewKeyDown(object sender, KeyEventArgs e)
    {
        // Handled at the Window level (which sees every key press first, since
        // PreviewKeyDown tunnels from the root down) rather than relying on the
        // recorder Border itself holding WPF keyboard focus — this way recording
        // works regardless of exactly which element ends up focused after the
        // click/Tab that started it.
        if (_recordingTarget == "toggle")
        {
            HandleRecorderKey(e, ToggleShortcutBox, ToggleShortcutText, _viewModel.RebindGlobalShortcut);
            return;
        }
        if (_recordingTarget == "settings")
        {
            HandleRecorderKey(e, SettingsShortcutBox, SettingsShortcutText, _viewModel.RebindSettingsShortcut);
            return;
        }

        if (e.Key == Key.Escape)
        {
            Close();
            e.Handled = true;
        }
        else if (e.Key == Key.S && Keyboard.Modifiers == ModifierKeys.Control)
        {
            SaveAndRemember();
            e.Handled = true;
        }
    }

    private void OnCloseClicked(object sender, RoutedEventArgs e) => Close();

    private void OnResetClicked(object sender, RoutedEventArgs e) => _viewModel.ResetToDefault();

    private void OnSaveClicked(object sender, RoutedEventArgs e) => SaveAndRemember();

    private void SaveAndRemember()
    {
        _viewModel.SaveTheme();
        _lastSavedTheme = _themeService.Current.Clone();

        // The footer's Save button applies to every tab's edits (theme, colors,
        // animations — everything in _editing), not just whatever section happens
        // to be visible right now, so confirm that clearly regardless of tab.
        _ = ShowTemporaryMessage(SaveStatusText, "Saved ✓", "");
    }

    /// <summary>
    /// Closing Settings without an explicit Save must never leave a half-edited
    /// theme applied nor corrupt settings.json: any live-preview edits made
    /// since the last Save are discarded and that last-saved theme (or the
    /// theme that was active when Settings opened, if never saved) is restored.
    /// </summary>
    private void OnClosing(object? sender, System.ComponentModel.CancelEventArgs e)
    {
        _viewModel.DiscardLiveEdits(_lastSavedTheme);
    }

    // ----------------------------------------------------------------------
    // Color picker: one shared Popup + ColorPickerPopup control, retargeted
    // to whichever field's swatch button was clicked.
    // ----------------------------------------------------------------------

    private void OnSwatchClick(object sender, RoutedEventArgs e)
    {
        if (sender is not Button button || button.Tag is not string fieldName)
            return;

        _viewModel.BeginEditColor(fieldName);
        ColorPopup.PlacementTarget = button;
        ColorPopup.IsOpen = true;
    }

    // ----------------------------------------------------------------------
    // Shortcut recorder: click a box, press a key combo, it re-binds live.
    // ----------------------------------------------------------------------

    private void OnToggleShortcutBoxClicked(object sender, MouseButtonEventArgs e)
    {
        BeginRecording("toggle", ToggleShortcutBox, ToggleShortcutText);
    }

    private void OnSettingsShortcutBoxClicked(object sender, MouseButtonEventArgs e)
    {
        BeginRecording("settings", SettingsShortcutBox, SettingsShortcutText);
    }

    // Tab-focusing a shortcut box starts recording too, so the whole flow works
    // without a mouse: Tab to the box, then just press the new combination.
    private void OnToggleShortcutBoxFocused(object sender, RoutedEventArgs e)
    {
        if (_recordingTarget is null)
            BeginRecording("toggle", ToggleShortcutBox, ToggleShortcutText);
    }

    private void OnSettingsShortcutBoxFocused(object sender, RoutedEventArgs e)
    {
        if (_recordingTarget is null)
            BeginRecording("settings", SettingsShortcutBox, SettingsShortcutText);
    }

    private void BeginRecording(string target, Border box, TextBlock label)
    {
        _recordingTarget = target;
        _recordingOriginalText = label.Text;
        label.Text = "Press a key combination…";
        box.Background = new SolidColorBrush(Color.FromArgb(0x33, 0x41, 0xD6, 0xC3));
        Keyboard.Focus(box);
    }

    // Border-level PreviewKeyDown handlers are intentionally NOT wired in XAML for
    // the recorder boxes anymore — see the comment in OnPreviewKeyDown above for why
    // this is handled at the Window level instead.

    private void HandleRecorderKey(KeyEventArgs e, Border box, TextBlock label, Func<string, bool> rebind)
    {
        e.Handled = true;
        var key = e.Key == Key.System ? e.SystemKey : e.Key;

        // A bare modifier key press isn't a complete combo yet — keep listening.
        if (key is Key.LeftCtrl or Key.RightCtrl or Key.LeftAlt or Key.RightAlt
            or Key.LeftShift or Key.RightShift or Key.LWin or Key.RWin)
        {
            return;
        }

        if (key == Key.Escape)
        {
            EndRecording();
            label.Text = _recordingOriginalText;
            RestoreBoxBackground(box);
            return;
        }

        var shortcut = HotkeyService.FormatShortcut(Keyboard.Modifiers, key);
        var ok = rebind(shortcut);

        EndRecording();
        RestoreBoxBackground(box);

        if (!ok)
        {
            // Binding already property-changed back via the ViewModel if it failed
            // silently kept the old value; briefly explain why nothing changed.
            label.Text = _recordingOriginalText;
            _ = ShowTemporaryMessage(label, "Already in use", _recordingOriginalText);
        }
        // On success, `label.Text` stays bound to the ViewModel property, which
        // RebindGlobalShortcut/RebindSettingsShortcut already updated.
    }

    private async System.Threading.Tasks.Task ShowTemporaryMessage(TextBlock label, string message, string restoreTo)
    {
        label.Text = message;
        await System.Threading.Tasks.Task.Delay(1200);
        label.Text = restoreTo;
    }

    private void EndRecording() => _recordingTarget = null;

    private static void RestoreBoxBackground(Border box) => box.Background = new SolidColorBrush(Color.FromArgb(0x14, 0xFF, 0xFF, 0xFF));
}
