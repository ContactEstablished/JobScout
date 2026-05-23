using JobScout.Core.Models;
using JobScout.Infrastructure.Data;
using Microsoft.AspNetCore.DataProtection;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Configuration;

namespace JobScout.Infrastructure.Configuration;

public class SecretStore : ISecretStore
{
    private const string DataProtectionPurpose = "JobScout.AppSecrets.v1";

    private readonly JobScoutDbContext _db;
    private readonly IDataProtector _protector;
    private readonly IConfiguration _config;

    public SecretStore(
        JobScoutDbContext db,
        IDataProtectionProvider protectionProvider,
        IConfiguration config)
    {
        _db = db;
        _protector = protectionProvider.CreateProtector(DataProtectionPurpose);
        _config = config;
    }

    public async Task<string?> GetAsync(string key, CancellationToken ct = default)
    {
        var row = await _db.AppSecrets.AsNoTracking().FirstOrDefaultAsync(s => s.Key == key, ct);
        if (row is not null && !string.IsNullOrEmpty(row.EncryptedValue))
        {
            try { return _protector.Unprotect(row.EncryptedValue); }
            catch { /* Data Protection ring rotated/lost — fall through to config */ }
        }

        return _config[key];
    }

    public async Task SetAsync(string key, string? value, CancellationToken ct = default)
    {
        var row = await _db.AppSecrets.FirstOrDefaultAsync(s => s.Key == key, ct);

        if (string.IsNullOrEmpty(value))
        {
            if (row is not null)
            {
                _db.AppSecrets.Remove(row);
                await _db.SaveChangesAsync(ct);
            }
            return;
        }

        var ciphertext = _protector.Protect(value);
        if (row is null)
        {
            _db.AppSecrets.Add(new AppSecret
            {
                Key = key,
                EncryptedValue = ciphertext,
                UpdatedAt = DateTime.UtcNow
            });
        }
        else
        {
            row.EncryptedValue = ciphertext;
            row.UpdatedAt = DateTime.UtcNow;
        }

        await _db.SaveChangesAsync(ct);
    }

    public async Task<IReadOnlyDictionary<string, string>> GetAllAsync(CancellationToken ct = default)
    {
        var rows = await _db.AppSecrets.AsNoTracking().ToListAsync(ct);
        var dict = new Dictionary<string, string>(StringComparer.Ordinal);
        foreach (var row in rows)
        {
            try { dict[row.Key] = _protector.Unprotect(row.EncryptedValue); }
            catch { /* skip undecryptable rows */ }
        }
        return dict;
    }
}
