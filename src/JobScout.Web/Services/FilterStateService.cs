using System.Text.Json;
using Microsoft.JSInterop;

namespace JobScout.Web.Services;

public class FilterStateService(IJSRuntime js)
{
    private const string StorageKey = "jobscout_filter_state";

    public bool IsFilterPanelOpen { get; private set; }
    public string? SearchQuery { get; set; }
    public string? Source { get; set; }
    public decimal? MinScore { get; set; }
    public string? LocationType { get; set; }
    public string? JobType { get; set; }
    public string? SortBy { get; set; } = "AiScore";

    public bool HasActiveFilters =>
        !string.IsNullOrEmpty(SearchQuery) || Source is not null ||
        MinScore is not null || LocationType is not null || JobType is not null;

    public event Action? OnFiltersChanged;
    public event Action? OnPanelToggled;

    public void ToggleFilterPanel()
    {
        IsFilterPanelOpen = !IsFilterPanelOpen;
        OnPanelToggled?.Invoke();
    }

    public void CloseFilterPanel()
    {
        IsFilterPanelOpen = false;
        OnPanelToggled?.Invoke();
    }

    public void Apply() => OnFiltersChanged?.Invoke();

    public void Clear()
    {
        SearchQuery = null;
        Source = null;
        MinScore = null;
        LocationType = null;
        JobType = null;
        SortBy = "AiScore";
        OnFiltersChanged?.Invoke();
    }

    public JobsFilter ToJobsFilter() => new()
    {
        Source = Source,
        MinScore = MinScore,
        LocationType = LocationType,
        JobType = JobType,
        Query = string.IsNullOrEmpty(SearchQuery) ? null : SearchQuery,
        SortBy = SortBy
    };

    // 9.4.d — persist the current (unsaved) view to localStorage so it survives a page refresh.
    // Uses the same raw IJSRuntime interop the auth layer uses; no extra dependency.
    public async Task SaveAsync()
    {
        var snapshot = new FilterSnapshot(SearchQuery, Source, MinScore, LocationType, JobType, SortBy);
        await js.InvokeVoidAsync("localStorage.setItem", StorageKey, JsonSerializer.Serialize(snapshot));
    }

    public async Task LoadAsync()
    {
        try
        {
            var json = await js.InvokeAsync<string?>("localStorage.getItem", StorageKey);
            if (string.IsNullOrEmpty(json)) return;

            var snap = JsonSerializer.Deserialize<FilterSnapshot>(json);
            if (snap is null) return;

            SearchQuery = snap.SearchQuery;
            Source = snap.Source;
            MinScore = snap.MinScore;
            LocationType = snap.LocationType;
            JobType = snap.JobType;
            SortBy = snap.SortBy ?? "AiScore";
        }
        catch
        {
            // Ignore malformed or out-of-date persisted state; fall back to defaults.
        }
    }

    private record FilterSnapshot(
        string? SearchQuery, string? Source, decimal? MinScore,
        string? LocationType, string? JobType, string? SortBy);
}
