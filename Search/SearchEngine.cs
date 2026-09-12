using Launcher.Models;
using Launcher.Services;

namespace Launcher.Search;

/// <summary>
/// Fans a query out to every registered <see cref="ISearchProvider"/>, merges
/// and ranks the combined results, and caps the output. Cancels any in-flight
/// search when a newer one starts, so the UI thread only ever sees the latest.
/// </summary>
public sealed class SearchEngine
{
    private readonly List<ISearchProvider> _providers = new();
    private CancellationTokenSource? _currentSearch;

    public void RegisterProvider(ISearchProvider provider) => _providers.Add(provider);

    public async Task InitializeAllAsync()
    {
        foreach (var provider in _providers)
        {
            try
            {
                await provider.InitializeAsync();
            }
            catch (Exception ex)
            {
                LogService.Error($"Provider '{provider.Id}' failed to initialize.", ex);
            }
        }
    }

    /// <summary>
    /// Searches all providers for <paramref name="query"/>, cancelling any
    /// previous in-flight search. Returns up to <paramref name="maxResults"/> items.
    /// </summary>
    public async Task<IReadOnlyList<SearchResult>> SearchAsync(string query, int maxResults)
    {
        _currentSearch?.Cancel();
        var cts = new CancellationTokenSource();
        _currentSearch = cts;

        if (string.IsNullOrWhiteSpace(query))
            return Array.Empty<SearchResult>();

        try
        {
            var tasks = _providers.Select(async p =>
            {
                try
                {
                    return await p.SearchAsync(query, cts.Token);
                }
                catch (OperationCanceledException)
                {
                    return (IReadOnlyList<SearchResult>)Array.Empty<SearchResult>();
                }
                catch (Exception ex)
                {
                    LogService.Error($"Provider '{p.Id}' search failed.", ex);
                    return (IReadOnlyList<SearchResult>)Array.Empty<SearchResult>();
                }
            });

            var resultSets = await Task.WhenAll(tasks);
            cts.Token.ThrowIfCancellationRequested();

            return resultSets
                .SelectMany(r => r)
                .OrderByDescending(r => r.Score)
                .ThenBy(r => r.Title, StringComparer.OrdinalIgnoreCase)
                .Take(maxResults)
                .ToList();
        }
        catch (OperationCanceledException)
        {
            return Array.Empty<SearchResult>();
        }
    }
}
