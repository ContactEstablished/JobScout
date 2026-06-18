using JobScout.Core.Models;

namespace JobScout.Core.Interfaces;

public interface IFilterPresetRepository
{
    Task<IReadOnlyList<FilterPreset>> GetByProfileAsync(Guid profileId);
    Task<FilterPreset?> GetByIdAsync(Guid id);
    Task AddAsync(FilterPreset preset);
    Task UpdateAsync(FilterPreset preset);
    Task DeleteAsync(Guid id);
}
