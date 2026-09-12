using Launcher.Models;
using Launcher.Services;

namespace Launcher.Search;

/// <summary>
/// Search provider backed by an in-memory index of discovered applications.
/// Discovery happens once (at <see cref="InitializeAsync"/>); every keystroke
/// after that only re-scores the cached index, never touches disk again.
/// </summary>
public sealed class AppSearchProvider : ISearchProvider
{
    private readonly AppDiscoveryService _discovery;
    private readonly IconService _iconService;
    private List<SearchResult> _index = new();

    public string Id => "apps";

    public AppSearchProvider(AppDiscoveryService discovery, IconService iconService)
    {
        _discovery = discovery;
        _iconService = iconService;
    }

    public Task InitializeAsync()
    {
        return Task.Run(() =>
        {
            try
            {
                _index = _discovery.DiscoverAll();
            }
            catch (Exception ex)
            {
                LogService.Error("App discovery failed; search index will be empty.", ex);
                _index = new List<SearchResult>();
            }
        });
    }

    public Task<IReadOnlyList<SearchResult>> SearchAsync(string query, CancellationToken cancellationToken)
    {
        return Task.Run(() =>
        {
            var scored = new List<SearchResult>();

            foreach (var entry in _index)
            {
                cancellationToken.ThrowIfCancellationRequested();

                var score = FuzzyMatcher.Score(query, entry.Title);
                if (score is null)
                    continue;

                // Lazily resolve the icon only for entries that actually matched,
                // and only once per unique target (IconService caches internally).
                var icon = entry.Kind == ResultKind.PackagedApp
                    ? _iconService.GetIconForShellItem(entry.LaunchTarget)
                    : _iconService.GetIconForPath(entry.Kind == ResultKind.ShellShortcut ? entry.LaunchTarget : entry.LaunchTarget);

                scored.Add(new SearchResult
                {
                    Title = entry.Title,
                    Subtitle = entry.Subtitle,
                    Category = entry.Category,
                    Icon = icon,
                    Kind = entry.Kind,
                    LaunchTarget = entry.LaunchTarget,
                    Arguments = entry.Arguments,
                    DedupeKey = entry.DedupeKey,
                    ProviderId = Id,
                    Score = score.Value
                });
            }

            return (IReadOnlyList<SearchResult>)scored
                .OrderByDescending(r => r.Score)
                .ThenBy(r => r.Title, StringComparer.OrdinalIgnoreCase)
                .ToList();
        }, cancellationToken);
    }
}
