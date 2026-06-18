using JobScout.Core.Interfaces;
using JobScout.Core.Models;
using JobScout.Infrastructure.Data;
using Microsoft.EntityFrameworkCore;

namespace JobScout.Infrastructure.Repositories;

public class FilterPresetRepository : IFilterPresetRepository
{
    private readonly JobScoutDbContext _db;

    public FilterPresetRepository(JobScoutDbContext db) => _db = db;

    public async Task<IReadOnlyList<FilterPreset>> GetByProfileAsync(Guid profileId) =>
        await _db.FilterPresets
            .Where(p => p.ProfileId == profileId)
            .OrderBy(p => p.Name)
            .ToListAsync();

    public async Task<FilterPreset?> GetByIdAsync(Guid id) =>
        await _db.FilterPresets.FirstOrDefaultAsync(p => p.Id == id);

    public async Task AddAsync(FilterPreset preset)
    {
        await _db.FilterPresets.AddAsync(preset);
        await _db.SaveChangesAsync();
    }

    public async Task UpdateAsync(FilterPreset preset)
    {
        _db.FilterPresets.Update(preset);
        await _db.SaveChangesAsync();
    }

    public async Task DeleteAsync(Guid id)
    {
        var preset = await _db.FilterPresets.FirstOrDefaultAsync(p => p.Id == id);
        if (preset is not null)
        {
            _db.FilterPresets.Remove(preset);
            await _db.SaveChangesAsync();
        }
    }
}
