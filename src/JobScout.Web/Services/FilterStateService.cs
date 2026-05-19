namespace JobScout.Web.Services;

public class FilterStateService
{
    public bool IsFilterPanelOpen { get; private set; }
    public string? SearchQuery { get; set; }
    public string? Source { get; set; }
    public decimal? MinScore { get; set; }
    public string? LocationType { get; set; }
    public string? JobType { get; set; }

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
        OnFiltersChanged?.Invoke();
    }

    public JobsFilter ToJobsFilter() => new()
    {
        Source = Source,
        MinScore = MinScore,
        LocationType = LocationType,
        JobType = JobType,
        Query = string.IsNullOrEmpty(SearchQuery) ? null : SearchQuery
    };
}
