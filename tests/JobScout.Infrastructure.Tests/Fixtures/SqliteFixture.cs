using JobScout.Infrastructure.Data;
using JobScout.Infrastructure.Identity;
using Microsoft.Data.Sqlite;
using Microsoft.EntityFrameworkCore;

namespace JobScout.Infrastructure.Tests.Fixtures;

/// <summary>
/// Per-test SQLite :memory: database. Keeps a single open connection so the
/// schema survives across multiple DbContext instances within one test.
/// Disposing the fixture closes the connection and drops the database.
/// </summary>
public sealed class SqliteFixture : IDisposable
{
    private readonly SqliteConnection _connection;
    private readonly DbContextOptions<JobScoutDbContext> _options;

    public SqliteFixture()
    {
        _connection = new SqliteConnection("DataSource=:memory:");
        _connection.Open();

        _options = new DbContextOptionsBuilder<JobScoutDbContext>()
            .UseSqlite(_connection)
            .Options;

        using var ctx = new JobScoutDbContext(_options);
        ctx.Database.EnsureCreated();
    }

    public JobScoutDbContext CreateContext() => new(_options);

    /// <summary>
    /// Adds an <see cref="ApplicationUser"/> row so SearchProfile FKs resolve.
    /// Returns the seeded user's Id.
    /// </summary>
    public async Task<string> SeedUserAsync(string userId = "test-user", string? email = null)
    {
        await using var ctx = CreateContext();
        if (await ctx.Users.AnyAsync(u => u.Id == userId)) return userId;

        ctx.Users.Add(new ApplicationUser
        {
            Id = userId,
            UserName = email ?? $"{userId}@example.com",
            NormalizedUserName = (email ?? $"{userId}@example.com").ToUpperInvariant(),
            Email = email ?? $"{userId}@example.com",
            NormalizedEmail = (email ?? $"{userId}@example.com").ToUpperInvariant(),
            DisplayName = userId,
            CreatedAt = DateTime.UtcNow,
            SecurityStamp = Guid.NewGuid().ToString()
        });
        await ctx.SaveChangesAsync();
        return userId;
    }

    public void Dispose()
    {
        _connection.Close();
        _connection.Dispose();
    }
}
