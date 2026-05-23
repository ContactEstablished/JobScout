using JobScout.Core.Models;

namespace JobScout.Core.Interfaces;

public interface IProfileRepository
{
    Task<IReadOnlyList<SearchProfile>> GetAllAsync(string userId);
    Task<SearchProfile?> GetByIdAsync(Guid id, string userId);
    Task AddAsync(SearchProfile profile);
    Task UpdateAsync(SearchProfile profile);
    Task DeleteAsync(Guid id, string userId);
    Task SetActiveAsync(Guid id, string userId);
}
