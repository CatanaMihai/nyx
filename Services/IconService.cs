using System.Collections.Concurrent;
using System.IO;
using System.Runtime.InteropServices;
using System.Windows;
using System.Windows.Interop;
using System.Windows.Media;
using System.Windows.Media.Imaging;

namespace Launcher.Services;

/// <summary>
/// Extracts and caches icons for files, shortcuts, and packaged (UWP) apps.
/// Icon extraction is best-effort: any failure yields a null icon rather than
/// throwing, so a single broken shortcut can never take down the app.
/// </summary>
public sealed class IconService
{
    private readonly ConcurrentDictionary<string, ImageSource?> _cache = new(StringComparer.OrdinalIgnoreCase);

    [DllImport("shell32.dll", CharSet = CharSet.Unicode)]
    private static extern int SHGetFileInfo(string pszPath, uint dwFileAttributes, ref SHFILEINFO psfi, uint cbFileInfo, uint uFlags);

    [StructLayout(LayoutKind.Sequential, CharSet = CharSet.Unicode)]
    private struct SHFILEINFO
    {
        public IntPtr hIcon;
        public int iIcon;
        public uint dwAttributes;
        [MarshalAs(UnmanagedType.ByValTStr, SizeConst = 260)] public string szDisplayName;
        [MarshalAs(UnmanagedType.ByValTStr, SizeConst = 80)] public string szTypeName;
    }

    private const uint SHGFI_ICON = 0x100;
    private const uint SHGFI_LARGEICON = 0x0;
    private const uint SHGFI_USEFILEATTRIBUTES = 0x10;
    private const uint FILE_ATTRIBUTE_NORMAL = 0x80;

    [DllImport("user32.dll")]
    private static extern bool DestroyIcon(IntPtr hIcon);

    // IShellItemImageFactory — used to fetch thumbnails/icons for shell items
    // that aren't ordinary file paths, e.g. "shell:AppsFolder\<AUMID>".
    [ComImport, Guid("bcc18b79-ba16-442f-80c4-8a59c30c463b"), InterfaceType(ComInterfaceType.InterfaceIsIUnknown)]
    private interface IShellItemImageFactory
    {
        [PreserveSig]
        int GetImage([In, MarshalAs(UnmanagedType.Struct)] SIZE size, [In] uint flags, out IntPtr phbm);
    }

    [StructLayout(LayoutKind.Sequential)]
    private struct SIZE { public int cx; public int cy; }

    [DllImport("shell32.dll", CharSet = CharSet.Unicode, PreserveSig = false)]
    private static extern void SHCreateItemFromParsingName(string pszPath, IntPtr pbc, [MarshalAs(UnmanagedType.LPStruct)] Guid riid, out IShellItemImageFactory ppv);

    [DllImport("gdi32.dll")]
    private static extern bool DeleteObject(IntPtr hObject);

    private static readonly Guid IID_IShellItemImageFactory = new("bcc18b79-ba16-442f-80c4-8a59c30c463b");

    /// <summary>Gets (and caches) an icon for a plain file/shortcut path.</summary>
    public ImageSource? GetIconForPath(string path)
    {
        if (_cache.TryGetValue(path, out var cached))
            return cached;

        ImageSource? icon = null;
        try
        {
            var shinfo = new SHFILEINFO();
            uint flags = SHGFI_ICON | SHGFI_LARGEICON;
            // Use file-attribute mode for non-existent virtual targets; otherwise read the real file.
            if (!File.Exists(path) && !Directory.Exists(path))
                flags |= SHGFI_USEFILEATTRIBUTES;

            SHGetFileInfo(path, FILE_ATTRIBUTE_NORMAL, ref shinfo, (uint)Marshal.SizeOf<SHFILEINFO>(), flags);
            if (shinfo.hIcon != IntPtr.Zero)
            {
                icon = ToImageSource(shinfo.hIcon);
                DestroyIcon(shinfo.hIcon);
            }
        }
        catch (Exception ex)
        {
            LogService.Warn($"Icon extraction failed for '{path}': {ex.Message}");
        }

        _cache[path] = icon;
        return icon;
    }

    /// <summary>Gets (and caches) an icon for a packaged app via its shell AppsFolder path.</summary>
    public ImageSource? GetIconForShellItem(string shellPath)
    {
        if (_cache.TryGetValue(shellPath, out var cached))
            return cached;

        ImageSource? icon = null;
        try
        {
            SHCreateItemFromParsingName(shellPath, IntPtr.Zero, IID_IShellItemImageFactory, out var factory);
            if (factory is not null)
            {
                var size = new SIZE { cx = 48, cy = 48 };
                const uint SIIGBF_RESIZETOFIT = 0x00;
                int hr = factory.GetImage(size, SIIGBF_RESIZETOFIT, out var hBitmap);
                if (hr == 0 && hBitmap != IntPtr.Zero)
                {
                    icon = ToImageSourceFromHBitmap(hBitmap);
                    DeleteObject(hBitmap);
                }
                Marshal.ReleaseComObject(factory);
            }
        }
        catch (Exception ex)
        {
            LogService.Warn($"Packaged-app icon extraction failed for '{shellPath}': {ex.Message}");
        }

        _cache[shellPath] = icon;
        return icon;
    }

    private static ImageSource? ToImageSource(IntPtr hIcon)
    {
        try
        {
            var src = Imaging.CreateBitmapSourceFromHIcon(hIcon, Int32Rect.Empty, BitmapSizeOptions.FromEmptyOptions());
            src.Freeze();
            return src;
        }
        catch
        {
            return null;
        }
    }

    private static ImageSource? ToImageSourceFromHBitmap(IntPtr hBitmap)
    {
        try
        {
            var src = Imaging.CreateBitmapSourceFromHBitmap(hBitmap, IntPtr.Zero, Int32Rect.Empty, BitmapSizeOptions.FromEmptyOptions());
            src.Freeze();
            return src;
        }
        catch
        {
            return null;
        }
    }
}
