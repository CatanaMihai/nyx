using System.Runtime.InteropServices;
using System.Text;

namespace Launcher.Services;

/// <summary>
/// Resolves .lnk shortcut files to their target path/arguments using the
/// IShellLinkW COM interface. Kept separate from discovery so a single
/// unreadable shortcut can be skipped without touching enumeration logic.
/// </summary>
public static class ShellLinkResolver
{
    [ComImport, Guid("00021401-0000-0000-C000-000000000046")]
    private class ShellLink { }

    [ComImport, InterfaceType(ComInterfaceType.InterfaceIsIUnknown), Guid("000214F9-0000-0000-C000-000000000046")]
    private interface IShellLinkW
    {
        void GetPath(StringBuilder pszFile, int cchMaxPath, IntPtr pfd, uint fFlags);
        void GetIDList(out IntPtr ppidl);
        void SetIDList(IntPtr pidl);
        void GetDescription(StringBuilder pszName, int cchMaxName);
        void SetDescription(string pszName);
        void GetWorkingDirectory(StringBuilder pszDir, int cchMaxPath);
        void SetWorkingDirectory(string pszDir);
        void GetArguments(StringBuilder pszArgs, int cchMaxPath);
        void SetArguments(string pszArgs);
        void GetHotkey(out short pwHotkey);
        void SetHotkey(short wHotkey);
        void GetShowCmd(out int piShowCmd);
        void SetShowCmd(int iShowCmd);
        void GetIconLocation(StringBuilder pszIconPath, int cchIconPath, out int piIcon);
        void SetIconLocation(string pszIconPath, int iIcon);
        void SetRelativePath(string pszPathRel, uint dwReserved);
        void Resolve(IntPtr hwnd, uint fFlags);
        void SetPath(string pszFile);
    }

    [ComImport, Guid("0000010b-0000-0000-C000-000000000046"), InterfaceType(ComInterfaceType.InterfaceIsIUnknown)]
    private interface IPersistFile
    {
        void GetClassID(out Guid pClassID);
        void IsDirty();
        void Load(string pszFileName, uint dwMode);
        void Save(string pszFileName, bool fRemember);
        void SaveCompleted(string pszFileName);
        void GetCurFile(out string ppszFileName);
    }

    public sealed record ResolvedLink(string TargetPath, string Arguments, string Description, string IconPath, int IconIndex);

    /// <summary>Resolves a .lnk file. Returns null if it cannot be read/resolved.</summary>
    public static ResolvedLink? Resolve(string lnkPath)
    {
        try
        {
            var link = (IShellLinkW)new ShellLink();
            ((IPersistFile)link).Load(lnkPath, 0);

            var targetSb = new StringBuilder(1024);
            link.GetPath(targetSb, targetSb.Capacity, IntPtr.Zero, 0);

            var argsSb = new StringBuilder(1024);
            link.GetArguments(argsSb, argsSb.Capacity);

            var descSb = new StringBuilder(1024);
            link.GetDescription(descSb, descSb.Capacity);

            var iconSb = new StringBuilder(1024);
            link.GetIconLocation(iconSb, iconSb.Capacity, out var iconIndex);

            Marshal.ReleaseComObject(link);

            var target = targetSb.ToString();
            if (string.IsNullOrWhiteSpace(target))
                return null;

            var iconPath = iconSb.Length > 0 ? iconSb.ToString() : target;

            return new ResolvedLink(target, argsSb.ToString(), descSb.ToString(), iconPath, iconIndex);
        }
        catch (Exception ex)
        {
            LogService.Warn($"Failed to resolve shortcut '{lnkPath}': {ex.Message}");
            return null;
        }
    }
}
