using Launcher.Models;

namespace Launcher.Search;

/// <summary>
/// A pluggable source of search results. Only <see cref="AppSearchProvider"/>
/// ships today, but the SearchEngine fans queries out to every registered
/// provider so future providers (files, calculator, web search, commands,
/// volume, clipboard, timers, ...) can be added without touching existing
/// code.
/// </summary>
public interface ISearchProvider
{
    /// <summary>Stable identifier for this provider, used for diagnostics/ranking.</summary>
    string Id { get; }

    /// <summary>
    /// Performs any expensive setup (e.g. indexing) needed before this provider
    /// can answer queries. Called once at startup, off the UI thread.
    /// </summary>
    Task InitializeAsync();

    /// <summary>
    /// Returns matching results for <paramref name="query"/>, best matches first.
    /// Must be fast and non-blocking; long-running providers should honor
    /// <paramref name="cancellationToken"/> so stale searches can be abandoned.
    /// </summary>
    Task<IReadOnlyList<SearchResult>> SearchAsync(string query, CancellationToken cancellationToken);
}
