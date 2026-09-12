using System.Diagnostics;
using System.IO;
using Launcher.Models;

namespace Launcher.Services;

/// <summary>
/// Knows how to launch each <see cref="ResultKind"/>. Isolated behind an
/// interface-free service so the ViewModel doesn't need to branch on kind.
/// </summary>
public sealed class LaunchService
{
    /// <summary>Launches the given result. Returns true on success.</summary>
    public bool Launch(SearchResult result)
    {
        try
        {
            switch (result.Kind)
            {
                case ResultKind.PackagedApp:
                    // "explorer.exe shell:AppsFolder\<AUMID>" is the documented, stable
                    // way to activate a packaged app without WinRT activation interop.
                    Process.Start(new ProcessStartInfo("explorer.exe", result.LaunchTarget)
                    {
                        UseShellExecute = true
                    });
                    return true;

                case ResultKind.ShellShortcut:
                case ResultKind.Win32Executable:
                case ResultKind.Other:
                default:
                    var psi = new ProcessStartInfo(result.LaunchTarget)
                    {
                        UseShellExecute = true
                    };
                    if (!string.IsNullOrWhiteSpace(result.Arguments))
                        psi.Arguments = result.Arguments;

                    var workDir = Path.GetDirectoryName(result.LaunchTarget);
                    if (!string.IsNullOrEmpty(workDir) && Directory.Exists(workDir))
                        psi.WorkingDirectory = workDir;

                    Process.Start(psi);
                    return true;
            }
        }
        catch (Exception ex)
        {
            LogService.Error($"Failed to launch '{result.Title}' ({result.LaunchTarget}).", ex);
            return false;
        }
    }
}
