using System.Collections.ObjectModel;
using System.Windows.Input;
using Launcher.Models;
using Launcher.Search;
using Launcher.Services;

namespace Launcher.ViewModels;

/// <summary>
/// Drives the launcher window: owns the query text, the ranked result list,
/// selection, and the commands the view binds to. Talks to the SearchEngine
/// and LaunchService but knows nothing about WPF visuals/animations.
/// </summary>
public sealed class MainViewModel : ObservableObject
{
    private readonly SearchEngine _searchEngine;
    private readonly LaunchService _launchService;
    private readonly AppSettings _settings;

    private string _queryText = string.Empty;
    private int _selectedIndex;
    private int _searchGeneration;

    public ObservableCollection<SearchResult> Results { get; } = new();

    public string QueryText
    {
        get => _queryText;
        set
        {
            if (SetField(ref _queryText, value))
                _ = OnQueryChangedAsync(value);
        }
    }

    public int SelectedIndex
    {
        get => _selectedIndex;
        set => SetField(ref _selectedIndex, value);
    }

    /// <summary>Raised when the view should close the launcher window (after launch or Esc).</summary>
    public event Action? RequestClose;

    public ICommand MoveSelectionDownCommand { get; }
    public ICommand MoveSelectionUpCommand { get; }
    public ICommand LaunchSelectedCommand { get; }
    public ICommand CloseCommand { get; }

    public MainViewModel(SearchEngine searchEngine, LaunchService launchService, AppSettings settings)
    {
        _searchEngine = searchEngine;
        _launchService = launchService;
        _settings = settings;

        MoveSelectionDownCommand = new RelayCommand(_ => MoveSelection(1));
        MoveSelectionUpCommand = new RelayCommand(_ => MoveSelection(-1));
        LaunchSelectedCommand = new RelayCommand(_ => LaunchSelected());
        CloseCommand = new RelayCommand(_ => RequestClose?.Invoke());
    }

    /// <summary>Resets state for a fresh "open" — clears query/results and focuses the search box (view-side).</summary>
    public void ResetForShow()
    {
        _queryText = string.Empty;
        OnPropertyChanged(nameof(QueryText));
        Results.Clear();
        SelectedIndex = 0;
    }

    private async Task OnQueryChangedAsync(string query)
    {
        var generation = ++_searchGeneration;

        if (string.IsNullOrWhiteSpace(query))
        {
            Results.Clear();
            return;
        }

        IReadOnlyList<SearchResult> found;
        try
        {
            found = await _searchEngine.SearchAsync(query, _settings.MaxResultCount);
        }
        catch (Exception ex)
        {
            LogService.Error("Search failed.", ex);
            return;
        }

        // A newer keystroke arrived while we were searching — discard stale results.
        if (generation != _searchGeneration)
            return;

        Results.Clear();
        foreach (var r in found)
            Results.Add(r);

        SelectedIndex = Results.Count > 0 ? 0 : -1;
    }

    private void MoveSelection(int delta)
    {
        if (Results.Count == 0)
            return;

        var next = SelectedIndex + delta;
        if (next < 0) next = 0;
        if (next > Results.Count - 1) next = Results.Count - 1;
        SelectedIndex = next;
    }

    private void LaunchSelected()
    {
        if (SelectedIndex < 0 || SelectedIndex >= Results.Count)
            return;

        var target = Results[SelectedIndex];
        var launched = _launchService.Launch(target);
        if (launched)
            RequestClose?.Invoke();
    }
}
