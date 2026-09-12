using System.Windows.Media;

namespace Launcher.Models;

/// <summary>
/// The kind of launchable target a <see cref="SearchResult"/> represents.
/// </summary>
public enum ResultKind
{
    Win32Executable,
    ShellShortcut,
    PackagedApp,
    Other
}

/// <summary>
/// A single search result surfaced by any <c>ISearchProvider</c>.
/// Immutable-ish DTO consumed by the UI; providers create these, the
/// SearchEngine ranks them, the ViewModel displays them, and the
/// LaunchService knows how to run them.
/// </summary>
public sealed class SearchResult
{
    /// <summary>Display title, e.g. "Visual Studio Code".</summary>
    public required string Title { get; init; }

    /// <summary>Secondary/subtitle text, e.g. the exe path or source.</summary>
    public string? Subtitle { get; init; }

    /// <summary>Category label shown on the right side of the row, e.g. "App".</summary>
    public string Category { get; init; } = "App";

    /// <summary>Icon to render for this result. May be null; UI should show a placeholder.</summary>
    public ImageSource? Icon { get; set; }

    /// <summary>How to launch this result.</summary>
    public required ResultKind Kind { get; init; }

    /// <summary>
    /// The launch target: full path for exe/lnk, or "shell:AppsFolder\{AUMID}" for packaged apps.
    /// </summary>
    public required string LaunchTarget { get; init; }

    /// <summary>Optional working directory / arguments for launch, if applicable.</summary>
    public string? Arguments { get; init; }

    /// <summary>Stable key used for de-duplication across discovery sources.</summary>
    public string DedupeKey { get; init; } = string.Empty;

    /// <summary>Which provider produced this result (for future multi-provider ranking).</summary>
    public string ProviderId { get; init; } = string.Empty;

    /// <summary>Score assigned by the fuzzy matcher for the current query; higher is better.</summary>
    public int Score { get; set; }
}
