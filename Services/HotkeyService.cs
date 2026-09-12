using System.Runtime.InteropServices;
using System.Windows.Interop;

namespace Launcher.Services;

/// <summary>
/// Registers one or more global hotkeys via the Win32 RegisterHotKey API and
/// invokes each one's callback whenever it fires. Hosted off a single hidden
/// message-only window (WPF's HwndSource) so it works even while the launcher
/// itself is hidden. Each hotkey is registered under a caller-chosen name
/// (e.g. "toggle", "settings") so it can be re-bound to a different key
/// combination later without restarting the app.
/// </summary>
public sealed class HotkeyService : IDisposable
{
    [DllImport("user32.dll")]
    private static extern bool RegisterHotKey(IntPtr hWnd, int id, uint fsModifiers, uint vk);

    [DllImport("user32.dll")]
    private static extern bool UnregisterHotKey(IntPtr hWnd, int id);

    private const int WM_HOTKEY = 0x0312;

    // MOD_* flags
    private const uint MOD_ALT = 0x0001;
    private const uint MOD_CONTROL = 0x0002;
    private const uint MOD_SHIFT = 0x0004;
    private const uint MOD_WIN = 0x0008;
    private const uint MOD_NOREPEAT = 0x4000;

    private sealed class Entry
    {
        public int Id;
        public string Shortcut = "";
        public Action Callback = () => { };
    }

    private HwndSource? _source;
    private int _nextId = 0xB00F;
    private readonly Dictionary<string, Entry> _entries = new(StringComparer.OrdinalIgnoreCase);

    /// <summary>
    /// Registers the hotkey described by <paramref name="shortcut"/> (e.g. "Ctrl+Space",
    /// "Ctrl+OemComma") under <paramref name="name"/>, invoking <paramref name="onPressed"/>
    /// when pressed. Returns false if the combo is already owned by another application.
    /// </summary>
    public bool Register(string name, string shortcut, Action onPressed)
    {
        try
        {
            EnsureMessageWindow();

            var (modifiers, vk) = ParseShortcut(shortcut);
            var id = _nextId++;

            var ok = RegisterHotKey(_source!.Handle, id, modifiers | MOD_NOREPEAT, vk);
            if (!ok)
            {
                LogService.Warn($"Failed to register global hotkey '{shortcut}' ('{name}'). It may be in use by another app.");
                return false;
            }

            _entries[name] = new Entry { Id = id, Shortcut = shortcut, Callback = onPressed };
            return true;
        }
        catch (Exception ex)
        {
            LogService.Error("HotkeyService.Register failed.", ex);
            return false;
        }
    }

    /// <summary>
    /// Re-registers the hotkey previously registered under <paramref name="name"/> to
    /// <paramref name="newShortcut"/>, keeping its existing callback. Takes effect
    /// immediately — no restart needed. Returns false (and leaves the old binding
    /// active) if the new combo can't be registered.
    /// </summary>
    public bool Rebind(string name, string newShortcut)
    {
        if (!_entries.TryGetValue(name, out var entry))
            return Register(name, newShortcut, () => { });

        try
        {
            var (modifiers, vk) = ParseShortcut(newShortcut);
            var newId = _nextId++;

            if (!RegisterHotKey(_source!.Handle, newId, modifiers | MOD_NOREPEAT, vk))
            {
                LogService.Warn($"Failed to rebind '{name}' to '{newShortcut}'; it may be in use by another app. Keeping '{entry.Shortcut}'.");
                return false;
            }

            UnregisterHotKey(_source.Handle, entry.Id);
            entry.Id = newId;
            entry.Shortcut = newShortcut;
            return true;
        }
        catch (Exception ex)
        {
            LogService.Error($"HotkeyService.Rebind('{name}') failed.", ex);
            return false;
        }
    }

    /// <summary>Current shortcut string bound to <paramref name="name"/>, or null if not registered.</summary>
    public string? GetShortcut(string name) => _entries.TryGetValue(name, out var e) ? e.Shortcut : null;

    private void EnsureMessageWindow()
    {
        if (_source is not null) return;

        var parameters = new HwndSourceParameters("LauncherHotkeyWindow")
        {
            WindowStyle = 0,
            Width = 0,
            Height = 0,
            ParentWindow = new IntPtr(-3) // HWND_MESSAGE
        };
        _source = new HwndSource(parameters);
        _source.AddHook(WndProc);
    }

    private IntPtr WndProc(IntPtr hwnd, int msg, IntPtr wParam, IntPtr lParam, ref bool handled)
    {
        if (msg == WM_HOTKEY)
        {
            var id = wParam.ToInt32();
            foreach (var entry in _entries.Values)
            {
                if (entry.Id == id)
                {
                    entry.Callback();
                    handled = true;
                    break;
                }
            }
        }
        return IntPtr.Zero;
    }

    /// <summary>Formats a WPF <c>Key</c> plus modifier set into the "Ctrl+Shift+X" shortcut string this service understands.</summary>
    public static string FormatShortcut(System.Windows.Input.ModifierKeys modifiers, System.Windows.Input.Key key)
    {
        var parts = new List<string>();
        if (modifiers.HasFlag(System.Windows.Input.ModifierKeys.Control)) parts.Add("Ctrl");
        if (modifiers.HasFlag(System.Windows.Input.ModifierKeys.Alt)) parts.Add("Alt");
        if (modifiers.HasFlag(System.Windows.Input.ModifierKeys.Shift)) parts.Add("Shift");
        if (modifiers.HasFlag(System.Windows.Input.ModifierKeys.Windows)) parts.Add("Win");

        var keyName = key switch
        {
            System.Windows.Input.Key.Space => "Space",
            System.Windows.Input.Key.OemComma => "OemComma",
            System.Windows.Input.Key.OemPeriod => "OemPeriod",
            _ => key.ToString()
        };
        parts.Add(keyName);
        return string.Join("+", parts);
    }

    /// <summary>Renders a shortcut string (e.g. "Ctrl+OemComma") the way a user expects to read it (e.g. "Ctrl+,").</summary>
    public static string DisplayName(string shortcut)
    {
        return shortcut
            .Replace("OemComma", ",", StringComparison.OrdinalIgnoreCase)
            .Replace("OemPeriod", ".", StringComparison.OrdinalIgnoreCase);
    }

    private static (uint modifiers, uint vk) ParseShortcut(string shortcut)
    {
        uint modifiers = 0;
        uint vk = 0;

        var parts = shortcut.Split('+', StringSplitOptions.TrimEntries | StringSplitOptions.RemoveEmptyEntries);
        foreach (var part in parts)
        {
            switch (part.ToLowerInvariant())
            {
                case "ctrl":
                case "control":
                    modifiers |= MOD_CONTROL;
                    break;
                case "alt":
                    modifiers |= MOD_ALT;
                    break;
                case "shift":
                    modifiers |= MOD_SHIFT;
                    break;
                case "win":
                case "windows":
                    modifiers |= MOD_WIN;
                    break;
                case "space":
                    vk = 0x20;
                    break;
                case "oemcomma":
                case ",":
                    vk = 0xBC; // VK_OEM_COMMA
                    break;
                case "oemperiod":
                case ".":
                    vk = 0xBE; // VK_OEM_PERIOD
                    break;
                default:
                    if (part.Length == 1)
                        vk = char.ToUpperInvariant(part[0]);
                    break;
            }
        }

        if (vk == 0)
        {
            // Fall back to the documented default: Ctrl+Space.
            modifiers = MOD_CONTROL;
            vk = 0x20;
        }

        return (modifiers, vk);
    }

    public void Dispose()
    {
        try
        {
            if (_source is not null)
            {
                foreach (var entry in _entries.Values)
                    UnregisterHotKey(_source.Handle, entry.Id);
                _source.Dispose();
            }
        }
        catch (Exception ex)
        {
            LogService.Error("HotkeyService.Dispose failed.", ex);
        }
    }
}
