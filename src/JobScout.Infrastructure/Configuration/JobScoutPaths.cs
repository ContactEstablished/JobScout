namespace JobScout.Infrastructure.Configuration;

/// <summary>
/// Resolves the per-user JobScout data directory.
///   Windows: %LOCALAPPDATA%\JobScout
///   Linux/macOS: ~/.jobscout
/// Files inside: jobscout.db, local.json, dpapi-keys/.
/// </summary>
public static class JobScoutPaths
{
    public static string LocalDataDirectory { get; } = ResolveDataDirectory();

    public static string LocalConfigPath => Path.Combine(LocalDataDirectory, "local.json");
    public static string DatabasePath => Path.Combine(LocalDataDirectory, "jobscout.db");
    public static string DataProtectionKeyRingPath => Path.Combine(LocalDataDirectory, "dpapi-keys");

    public static void EnsureExists()
    {
        Directory.CreateDirectory(LocalDataDirectory);
    }

    private static string ResolveDataDirectory()
    {
        if (OperatingSystem.IsWindows())
        {
            var localAppData = Environment.GetFolderPath(Environment.SpecialFolder.LocalApplicationData);
            return Path.Combine(localAppData, "JobScout");
        }

        var home = Environment.GetFolderPath(Environment.SpecialFolder.UserProfile);
        return Path.Combine(home, ".jobscout");
    }
}
