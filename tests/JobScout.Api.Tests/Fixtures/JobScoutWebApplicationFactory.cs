using JobScout.Infrastructure.Data;
using Microsoft.AspNetCore.Hosting;
using Microsoft.AspNetCore.Mvc.Testing;
using Microsoft.Data.Sqlite;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Hosting;

namespace JobScout.Api.Tests.Fixtures;

public class JobScoutWebApplicationFactory : WebApplicationFactory<Program>
{
    private readonly SqliteConnection _connection = new("DataSource=:memory:");

    public JobScoutWebApplicationFactory()
    {
        // Program.cs reads these at builder construction, before ConfigureAppConfiguration runs.
        Environment.SetEnvironmentVariable("Jwt__Key", "test-jwt-key-must-be-at-least-32-characters-long");
        Environment.SetEnvironmentVariable("Jwt__Issuer", "JobScout.Tests");
        Environment.SetEnvironmentVariable("Jwt__Audience", "JobScout.Tests");
        Environment.SetEnvironmentVariable("ConnectionStrings__DefaultConnection", "DataSource=:memory:");
    }

    protected override IHost CreateHost(IHostBuilder builder)
    {
        _connection.Open();
        return base.CreateHost(builder);
    }

    protected override void ConfigureWebHost(IWebHostBuilder builder)
    {
        builder.UseEnvironment("Testing");

        builder.ConfigureServices(services =>
        {
            // Strip out the production SQLite-on-disk registration and replace with our shared in-memory connection.
            var dbDescriptor = services.SingleOrDefault(d => d.ServiceType == typeof(DbContextOptions<JobScoutDbContext>));
            if (dbDescriptor is not null) services.Remove(dbDescriptor);

            services.AddDbContext<JobScoutDbContext>(opt => opt.UseSqlite(_connection));

            using var scope = services.BuildServiceProvider().CreateScope();
            var db = scope.ServiceProvider.GetRequiredService<JobScoutDbContext>();
            db.Database.EnsureCreated();
        });
    }

    public async Task<string> RegisterAsync(HttpClient client, string email, string password, string displayName = "Test User")
    {
        var response = await client.PostAsJsonAsync("/api/auth/register", new RegisterRequest
        {
            Email = email,
            Password = password,
            DisplayName = displayName
        });
        response.EnsureSuccessStatusCode();
        var body = await response.Content.ReadFromJsonAsync<AuthResponse>();
        return body!.Token;
    }

    public async Task<string> LoginAsync(HttpClient client, string email, string password)
    {
        var response = await client.PostAsJsonAsync("/api/auth/login", new LoginRequest
        {
            Email = email,
            Password = password
        });
        response.EnsureSuccessStatusCode();
        var body = await response.Content.ReadFromJsonAsync<AuthResponse>();
        return body!.Token;
    }

    protected override void Dispose(bool disposing)
    {
        if (disposing) _connection.Dispose();
        base.Dispose(disposing);
    }
}
