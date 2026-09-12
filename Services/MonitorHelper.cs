using System.Runtime.InteropServices;
using System.Windows;

namespace Launcher.Services;

/// <summary>A monitor's work area (taskbar-excluded bounds), already converted to
/// WPF device-independent units using that monitor's own DPI scale.</summary>
public readonly record struct MonitorWorkArea(double Left, double Top, double Width, double Height);

/// <summary>
/// Resolves which monitor the launcher should open on: the monitor containing the
/// foreground window (the app the user was in right before pressing the hotkey),
/// falling back to the monitor under the cursor, and finally the primary monitor.
/// All math is done in Win32 physical pixels and only converted to DIPs at the end,
/// using the DPI reported for that specific monitor — so mixed-DPI setups
/// (100%/125%/150%...) each resolve correctly instead of assuming the primary
/// monitor's scale.
/// </summary>
public static class MonitorHelper
{
    [DllImport("user32.dll")]
    private static extern IntPtr GetForegroundWindow();

    [DllImport("user32.dll")]
    private static extern IntPtr MonitorFromWindow(IntPtr hwnd, uint dwFlags);

    [DllImport("user32.dll")]
    private static extern IntPtr MonitorFromPoint(POINT pt, uint dwFlags);

    [DllImport("user32.dll")]
    private static extern bool GetCursorPos(out POINT lpPoint);

    [DllImport("user32.dll", CharSet = CharSet.Auto)]
    private static extern bool GetMonitorInfo(IntPtr hMonitor, ref MONITORINFO lpmi);

    [DllImport("shcore.dll")]
    private static extern int GetDpiForMonitor(IntPtr hmonitor, int dpiType, out uint dpiX, out uint dpiY);

    private const uint MONITOR_DEFAULTTONEAREST = 2;
    private const int MDT_EFFECTIVE_DPI = 0;

    [StructLayout(LayoutKind.Sequential)]
    private struct POINT
    {
        public int X;
        public int Y;
    }

    [StructLayout(LayoutKind.Sequential)]
    private struct RECT
    {
        public int Left;
        public int Top;
        public int Right;
        public int Bottom;
    }

    [StructLayout(LayoutKind.Sequential, CharSet = CharSet.Auto)]
    private struct MONITORINFO
    {
        public int cbSize;
        public RECT rcMonitor;
        public RECT rcWork;
        public uint dwFlags;
    }

    /// <summary>
    /// Returns the work area (DIPs) of the monitor that owns the current foreground
    /// window. Falls back to the monitor under the cursor if there is no foreground
    /// window, and to the primary screen's work area if Win32 lookups fail entirely.
    /// </summary>
    public static MonitorWorkArea GetTargetMonitorWorkArea()
    {
        try
        {
            IntPtr monitor = IntPtr.Zero;

            var foreground = GetForegroundWindow();
            if (foreground != IntPtr.Zero)
                monitor = MonitorFromWindow(foreground, MONITOR_DEFAULTTONEAREST);

            if (monitor == IntPtr.Zero)
            {
                if (GetCursorPos(out var pt))
                    monitor = MonitorFromPoint(pt, MONITOR_DEFAULTTONEAREST);
            }

            if (monitor == IntPtr.Zero)
                return GetPrimaryFallback();

            var info = new MONITORINFO { cbSize = Marshal.SizeOf<MONITORINFO>() };
            if (!GetMonitorInfo(monitor, ref info))
                return GetPrimaryFallback();

            double scale = 1.0;
            if (GetDpiForMonitor(monitor, MDT_EFFECTIVE_DPI, out var dpiX, out _) == 0 && dpiX > 0)
                scale = dpiX / 96.0;

            var work = info.rcWork;
            return new MonitorWorkArea(
                work.Left / scale,
                work.Top / scale,
                (work.Right - work.Left) / scale,
                (work.Bottom - work.Top) / scale);
        }
        catch (Exception ex)
        {
            LogService.Error("Failed to resolve target monitor for launcher placement; using primary screen.", ex);
            return GetPrimaryFallback();
        }
    }

    private static MonitorWorkArea GetPrimaryFallback()
    {
        var wa = SystemParameters.WorkArea;
        return new MonitorWorkArea(wa.Left, wa.Top, wa.Width, wa.Height);
    }
}
