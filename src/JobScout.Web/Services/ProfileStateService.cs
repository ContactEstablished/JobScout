using JobScout.Core.DTOs;

namespace JobScout.Web.Services;

public class ProfileStateService
{
    public SearchProfileDto? ActiveProfile { get; private set; }
    public IReadOnlyList<SearchProfileDto> Profiles { get; private set; } = [];

    public event Action? OnChange;

    public void SetProfiles(IReadOnlyList<SearchProfileDto> profiles)
    {
        Profiles = profiles;
        if (ActiveProfile is null)
            ActiveProfile = profiles.FirstOrDefault(p => p.IsActive) ?? profiles.FirstOrDefault();
        NotifyStateChanged();
    }

    public void SetActiveProfile(SearchProfileDto profile)
    {
        ActiveProfile = profile;
        NotifyStateChanged();
    }

    public void AddProfile(SearchProfileDto profile)
    {
        Profiles = [.. Profiles, profile];
        NotifyStateChanged();
    }

    public void RemoveProfile(Guid id)
    {
        Profiles = Profiles.Where(p => p.Id != id).ToList();
        if (ActiveProfile?.Id == id)
            ActiveProfile = Profiles.FirstOrDefault();
        NotifyStateChanged();
    }

    public void UpdateProfile(SearchProfileDto updated)
    {
        Profiles = Profiles.Select(p => p.Id == updated.Id ? updated : p).ToList();
        if (ActiveProfile?.Id == updated.Id)
            ActiveProfile = updated;
        NotifyStateChanged();
    }

    private void NotifyStateChanged() => OnChange?.Invoke();
}
