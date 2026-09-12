using System.Windows;
using Launcher.Search;
using Launcher.Services;
using Launcher.Themes;
using Launcher.ViewModels;
using Launcher.Views;

namespace Launcher;

/// <summary>
/// Composition root. Wires up services, the search engine/providers, the global
/// hotkey, the theme service, and a tray icon; owns the single MainWindow (and,
/// lazily, SettingsWindow) instance and toggles visibility rather than
/// creating/destroying windows on every hotkey press.
/// </summary>
public partial class App : System.Windows.Application
{
    private System.Threading.Mutex? _singleInstanceMutex;
    private HotkeyService? _hotkeyService;
    private System.Windows.Forms.NotifyIcon? _trayIcon;
    private MainWindow? _mainWindow;
    private SettingsWindow? _settingsWindow;
    private SettingsService? _settingsService;
    private ThemeService? _themeService;

    protected override async void OnStartup(StartupEventArgs e)
    {
        base.OnStartup(e);

        _singleInstanceMutex = new System.Threading.Mutex(true, "Nyx.SingleInstance.Mutex", out bool createdNew);
        if (!createdNew)
        {
            // Another instance is already running; nothing productive to do here.
            Shutdown();
            return;
        }

        _settingsService = new SettingsService();
        _settingsService.Load();

        _themeService = new ThemeService();
        _themeService.OnThemeAppliedForPersistence = name =>
        {
            _settingsService.Current.CurrentThemeName = name;
            _settingsService.Save();
        };
        _themeService.Initialize(_settingsService.Current.CurrentThemeName);

        var iconService = new IconService();
        var discoveryService = new AppDiscoveryService();
        var launchService = new LaunchService();

        var searchEngine = new SearchEngine();
        searchEngine.RegisterProvider(new AppSearchProvider(discoveryService, iconService));

        var viewModel = new MainViewModel(searchEngine, launchService, _settingsService.Current);
        _mainWindow = new MainWindow(viewModel, _settingsService.Current, _themeService);
        viewModel.RequestClose += () => _mainWindow?.AnimateHide();

        SetupTrayIcon();

        _hotkeyService = new HotkeyService();
        _hotkeyService.Register("toggle", _settingsService.Current.GlobalShortcut, () => Dispatcher.Invoke(ToggleWindow));
        _hotkeyService.Register("settings", _settingsService.Current.SettingsShortcut, () => Dispatcher.Invoke(OpenSettings));

        // Index apps in the background so the very first keystroke is already fast.
        try
        {
            await searchEngine.InitializeAllAsync();
        }
        catch (Exception ex)
        {
            LogService.Error("Initial search index build failed.", ex);
        }
    }

    private void ToggleWindow()
    {
        if (_mainWindow is null) return;

        if (_mainWindow.IsVisible)
            _mainWindow.AnimateHide();
        else
            _mainWindow.AnimateShow();
    }

    private void OpenSettings()
    {
        if (_themeService is null || _settingsService is null) return;

        _mainWindow?.AnimateHide();

        if (_settingsWindow is null || !_settingsWindow.IsLoaded)
        {
            _settingsWindow = new SettingsWindow(_themeService, _settingsService, _hotkeyService!);
            _settingsWindow.Closed += (_, _) => _settingsWindow = null;
        }

        _settingsWindow.Show();
        _settingsWindow.Activate();
    }

    /// <summary>
    /// Loads the app's own icon (embedded into the .exe at build time via
    /// &lt;ApplicationIcon&gt; in Launcher.csproj, from Assets/nyx.ico) for the
    /// tray icon, instead of a generic system icon. Falls back to a system
    /// icon if that ever fails, so a missing/corrupt icon resource can never
    /// prevent the tray icon (and therefore the app) from working.
    /// </summary>
    private static System.Drawing.Icon LoadAppIcon()
    {
        try
        {
            var exePath = System.Diagnostics.Process.GetCurrentProcess().MainModule?.FileName;
            if (!string.IsNullOrEmpty(exePath))
            {
                var icon = System.Drawing.Icon.ExtractAssociatedIcon(exePath);
                if (icon is not null)
                    return icon;
            }
        }
        catch (Exception ex)
        {
            LogService.Error("Failed to load app icon for tray; using system default.", ex);
        }

        return System.Drawing.SystemIcons.Application;
    }

    private void SetupTrayIcon()
    {
        try
        {
            _trayIcon = new System.Windows.Forms.NotifyIcon
            {
                Icon = LoadAppIcon(),
                Visible = true,
                Text = "Nyx — Ctrl+Space to open"
            };

            var menu = new System.Windows.Forms.ContextMenuStrip();
            menu.Items.Add("Open", null, (_, _) => Dispatcher.Invoke(() => _mainWindow?.AnimateShow()));
            menu.Items.Add("Settings", null, (_, _) => Dispatcher.Invoke(OpenSettings));
            menu.Items.Add(new System.Windows.Forms.ToolStripSeparator());
            menu.Items.Add("Exit", null, (_, _) => Dispatcher.Invoke(() => Shutdown()));
            _trayIcon.ContextMenuStrip = menu;

            _trayIcon.DoubleClick += (_, _) => Dispatcher.Invoke(() => _mainWindow?.AnimateShow());
        }
        catch (Exception ex)
        {
            LogService.Error("Failed to create tray icon.", ex);
        }
    }

    protected override void OnExit(ExitEventArgs e)
    {
        _hotkeyService?.Dispose();
        if (_trayIcon is not null)
        {
            _trayIcon.Visible = false;
            _trayIcon.Dispose();
        }
        _singleInstanceMutex?.ReleaseMutex();
        base.OnExit(e);
    }
}
