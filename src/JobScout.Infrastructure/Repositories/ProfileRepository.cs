using JobScout.Core.Interfaces;
using JobScout.Core.Models;
using JobScout.Infrastructure.Data;
using Microsoft.EntityFrameworkCore;

namespace JobScout.Infrastructure.Repositories;

public class ProfileRepository : IProfileRepository
{
    private readonly JobScoutDbContext _db;

    public ProfileRepository(JobScoutDbContext db) => _db = db;

    public async Task<IReadOnlyList<SearchProfile>> GetAllAsync() =>
        await _db.SearchProfiles.OrderByDescending(p => p.IsActive).ThenBy(p => p.Name).ToListAsync();

    public async Task<SearchProfile?> GetByIdAsync(Guid id) =>
        await _db.SearchProfiles.FirstOrDefaultAsync(p => p.Id == id);

    public async Task AddAsync(SearchProfile profile)
    {
        await _db.SearchProfiles.AddAsync(profile);
        await _db.SaveChangesAsync();
    }

    public async Task UpdateAsync(SearchProfile profile)
    {
        _db.SearchProfiles.Update(profile);
        await _db.SaveChangesAsync();
    }

    public async Task DeleteAsync(Guid id)
    {
        var profile = await _db.SearchProfiles.FindAsync(id);
        if (profile is not null)
        {
            _db.SearchProfiles.Remove(profile);
            await _db.SaveChangesAsync();
        }
    }

    public async Task SetActiveAsync(Guid id)
    {
        await _db.SearchProfiles.ExecuteUpdateAsync(s =>
            s.SetProperty(p => p.IsActive, false));

        await _db.SearchProfiles
            .Where(p => p.Id == id)
            .ExecuteUpdateAsync(s =>
                s.SetProperty(p => p.IsActive, true));
    }
}
