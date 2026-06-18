using System.Security.Cryptography;
using System.Text.Json;

namespace JobScout.Infrastructure.Configuration;

/// <summary>
/// Reads + writes a tiny JSON config file in the user's local data directory
/// for values that need to survive between runs but aren't user secrets
/// (e.g. the auto-generated JWT signing key).
/// </summary>
public class LocalConfigStore
{
    private readonly object _lock = new();
    private Dictionary<string, string>? _cache;

    public string? Get(string key)
    {
        EnsureLoaded();
        return _cache!.TryGetValue(key, out var v) ? v : null;
    }

    public void Set(string key, string value)
    {
        lock (_lock)
        {
            EnsureLoadedLocked();
            _cache![key] = value;
            WriteLocked();
        }
    }

    /// <summary>
    /// Returns the value if present, otherwise generates one via <paramref name="generator"/>,
    /// persists it, and returns the new value.
    /// </summary>
    public string GetOrCreate(string key, Func<string> generator)
    {
        var existing = Get(key);
        if (!string.IsNullOrEmpty(existing)) return existing;

        lock (_lock)
        {
            EnsureLoadedLocked();
            if (_cache!.TryGetValue(key, out var raced)) return raced;

            var fresh = generator();
            _cache[key] = fresh;
            WriteLocked();
            return fresh;
        }
    }

    public static string GenerateBase64Key(int bytes = 32)
    {
        Span<byte> buffer = stackalloc byte[bytes];
        RandomNumberGenerator.Fill(buffer);
        return Convert.ToBase64String(buffer);
    }

    private void EnsureLoaded()
    {
        if (_cache is not null) return;
        lock (_lock) { EnsureLoadedLocked(); }
    }

    private void EnsureLoadedLocked()
    {
        if (_cache is not null) return;

        JobScoutPaths.EnsureExists();
        var path = JobScoutPaths.LocalConfigPath;

        if (!File.Exists(path))
        {
            _cache = new Dictionary<string, string>(StringComparer.OrdinalIgnoreCase);
            return;
        }

        try
        {
            var json = File.ReadAllText(path);
            _cache = JsonSerializer.Deserialize<Dictionary<string, string>>(json)
                     ?? new(StringComparer.OrdinalIgnoreCase);
        }
        catch
        {
            // Corrupt file → start fresh; backup the existing one so we don't silently lose data.
            try { File.Copy(path, path + ".corrupt-" + DateTime.UtcNow.Ticks, overwrite: true); } catch { }
            _cache = new Dictionary<string, string>(StringComparer.OrdinalIgnoreCase);
        }
    }

    private void WriteLocked()
    {
        var path = JobScoutPaths.LocalConfigPath;
        var json = JsonSerializer.Serialize(_cache, new JsonSerializerOptions { WriteIndented = true });
        File.WriteAllText(path, json);
    }
}
