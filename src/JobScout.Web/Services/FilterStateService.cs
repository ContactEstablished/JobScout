using System.Text.Json;
using JobScout.Core.DTOs;
using JobScout.Core.Enums;
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

    // 9.4.c — hydrate the panel from a saved preset, and capture the current state into a save request.
    public void LoadFrom(FilterPresetDto preset)
    {
        Source = preset.Source?.ToString();
        MinScore = preset.MinScore;
        LocationType = preset.LocationType?.ToString();
        JobType = preset.JobType?.ToString();
        SearchQuery = preset.Query;
        SortBy = preset.SortBy.ToString();
        OnFiltersChanged?.Invoke();
    }

    public SaveFilterPresetRequest ToSaveRequest(Guid profileId, string name) => new()
    {
        ProfileId = profileId,
        Name = name,
        Source = Enum.TryParse<JobSource>(Source, out var src) ? src : null,
        MinScore = MinScore,
        LocationType = Enum.TryParse<JobScout.Core.Enums.LocationType>(LocationType, out var lt) ? lt : null,
        JobType = Enum.TryParse<JobScout.Core.Enums.JobType>(JobType, out var jt) ? jt : null,
        Query = string.IsNullOrEmpty(SearchQuery) ? null : SearchQuery,
        SortBy = Enum.TryParse<JobSortBy>(SortBy, out var sb) ? sb : JobSortBy.AiScore
    };

    private record FilterSnapshot(
        string? SearchQuery, string? Source, decimal? MinScore,
        string? LocationType, string? JobType, string? SortBy);
}
