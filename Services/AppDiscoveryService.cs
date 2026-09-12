using System.IO;
using Launcher.Models;

namespace Launcher.Services;

/// <summary>
/// Discovers launchable applications from Start Menu shortcuts (per-user and
/// common) and packaged/UWP apps registered in the shell's AppsFolder.
/// Results are de-duplicated by launch target. Discovery is a one-shot scan
/// meant to run at startup; callers are expected to cache the result.
/// </summary>
public sealed class AppDiscoveryService
{
    public List<SearchResult> DiscoverAll()
    {
        var results = new List<SearchResult>();
        var seenKeys = new HashSet<string>(StringComparer.OrdinalIgnoreCase);

        foreach (var dir in GetStartMenuDirectories())
        {
            foreach (var result in ScanDirectory(dir))
            {
                if (seenKeys.Add(result.DedupeKey))
                    results.Add(result);
            }
        }

        foreach (var result in ScanPackagedApps())
        {
            if (seenKeys.Add(result.DedupeKey))
                results.Add(result);
        }

        return results;
    }

    private static IEnumerable<string> GetStartMenuDirectories()
    {
        var dirs = new List<string>();

        var userStartMenu = Environment.GetFolderPath(Environment.SpecialFolder.StartMenu);
        var commonStartMenu = Environment.GetFolderPath(Environment.SpecialFolder.CommonStartMenu);

        if (!string.IsNullOrEmpty(userStartMenu)) dirs.Add(userStartMenu);
        if (!string.IsNullOrEmpty(commonStartMenu)) dirs.Add(commonStartMenu);

        // Also include the Desktop, a common place users pin .lnk/.exe shortcuts.
        var desktop = Environment.GetFolderPath(Environment.SpecialFolder.Desktop);
        if (!string.IsNullOrEmpty(desktop)) dirs.Add(desktop);

        return dirs.Where(Directory.Exists);
    }

    private static IEnumerable<SearchResult> ScanDirectory(string root)
    {
        var results = new List<SearchResult>();

        IEnumerable<string> files;
        try
        {
            files = Directory.EnumerateFiles(root, "*.*", SearchOption.AllDirectories)
                .Where(f => f.EndsWith(".lnk", StringComparison.OrdinalIgnoreCase) ||
                            f.EndsWith(".exe", StringComparison.OrdinalIgnoreCase));
        }
        catch (Exception ex)
        {
            LogService.Warn($"Could not enumerate '{root}': {ex.Message}");
            yield break;
        }

        foreach (var file in files)
        {
            var result = TryBuildResult(file);
            if (result is not null)
                results.Add(result);
        }

        foreach (var r in results)
            yield return r;
    }

    private static SearchResult? TryBuildResult(string filePath)
    {
        try
        {
            var name = Path.GetFileNameWithoutExtension(filePath);
            if (string.IsNullOrWhiteSpace(name))
                return null;

            // Skip common noise: uninstallers, help links, readme shortcuts.
            var lowerName = name.ToLowerInvariant();
            if (lowerName.Contains("uninstall") || lowerName.Contains("read me") || lowerName.Contains("readme"))
                return null;

            if (filePath.EndsWith(".lnk", StringComparison.OrdinalIgnoreCase))
            {
                var resolved = ShellLinkResolver.Resolve(filePath);
                if (resolved is null || string.IsNullOrWhiteSpace(resolved.TargetPath))
                    return null;

                // Skip shortcuts pointing at folders/documents rather than executables,
                // unless the target is itself missing (still show it, best-effort).
                var target = resolved.TargetPath;
                var ext = Path.GetExtension(target).ToLowerInvariant();
                if (ext is not (".exe" or ""))
                    return null;

                return new SearchResult
                {
                    Title = name,
                    Subtitle = target,
                    Category = "App",
                    Kind = ResultKind.ShellShortcut,
                    LaunchTarget = filePath,
                    Arguments = resolved.Arguments,
                    DedupeKey = target.ToLowerInvariant(),
                    ProviderId = "apps"
                };
            }
            else // .exe
            {
                return new SearchResult
                {
                    Title = name,
                    Subtitle = filePath,
                    Category = "App",
                    Kind = ResultKind.Win32Executable,
                    LaunchTarget = filePath,
                    DedupeKey = filePath.ToLowerInvariant(),
                    ProviderId = "apps"
                };
            }
        }
        catch (Exception ex)
        {
            LogService.Warn($"Skipping broken Start Menu entry '{filePath}': {ex.Message}");
            return null;
        }
    }

    /// <summary>
    /// Enumerates packaged (MSIX/UWP) apps via the shell's AppsFolder using the
    /// late-bound Shell.Application COM automation object. This avoids needing
    /// WinRT projections while still surfacing name + AppUserModelID + icon path.
    /// </summary>
    private static IEnumerable<SearchResult> ScanPackagedApps()
    {
        var results = new List<SearchResult>();
        try
        {
            dynamic shell = Activator.CreateInstance(Type.GetTypeFromProgID("Shell.Application")!)!;
            dynamic appsFolder = shell.NameSpace("shell:AppsFolder");
            if (appsFolder is null)
                return results;

            foreach (dynamic item in appsFolder.Items())
            {
                try
                {
                    string name = item.Name;
                    // FolderItem.Path for AppsFolder items returns "AppsFolder\<AUMID>" or the AUMID directly
                    // depending on OS build; normalize to the shell: form used for launching.
                    string path = item.Path;

                    if (string.IsNullOrWhiteSpace(name) || string.IsNullOrWhiteSpace(path))
                        continue;

                    // Desktop (Win32) apps also show up in AppsFolder as a convenience view;
                    // those have a real file-system path and are already covered by the
                    // Start Menu scan above, so skip anything that resolves to a normal path.
                    bool looksLikeAumid = path.Contains('!') || !path.Contains('\\');
                    if (!looksLikeAumid)
                        continue;

                    var shellLaunchPath = $"shell:AppsFolder\\{path}";

                    results.Add(new SearchResult
                    {
                        Title = name,
                        Subtitle = "Packaged app",
                        Category = "App",
                        Kind = ResultKind.PackagedApp,
                        LaunchTarget = shellLaunchPath,
                        DedupeKey = "pkg:" + path.ToLowerInvariant(),
                        ProviderId = "apps"
                    });
                }
                catch (Exception ex)
                {
                    LogService.Warn($"Skipping unreadable packaged app entry: {ex.Message}");
                }
            }
        }
        catch (Exception ex)
        {
            LogService.Warn($"Packaged app enumeration failed: {ex.Message}");
        }

        return results;
    }
}
