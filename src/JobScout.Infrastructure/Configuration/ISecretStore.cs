namespace JobScout.Infrastructure.Configuration;

/// <summary>
/// Per-application encrypted key/value store, backed by the DB and the ASP.NET Data Protection API.
/// Reads fall back to configuration when the DB has no entry, so existing env-var / appsettings
/// setups keep working.
/// </summary>
public interface ISecretStore
{
    Task<string?> GetAsync(string key, CancellationToken ct = default);
    Task SetAsync(string key, string? value, CancellationToken ct = default);
    Task<IReadOnlyDictionary<string, string>> GetAllAsync(CancellationToken ct = default);
}
