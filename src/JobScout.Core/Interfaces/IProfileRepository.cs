using JobScout.Core.Models;

namespace JobScout.Core.Interfaces;

public interface IProfileRepository
{
    Task<IReadOnlyList<SearchProfile>> GetAllAsync();
    Task<SearchProfile?> GetByIdAsync(Guid id);
    Task AddAsync(SearchProfile profile);
    Task UpdateAsync(SearchProfile profile);
    Task DeleteAsync(Guid id);
    Task SetActiveAsync(Guid id);
}
