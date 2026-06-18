using System.Text;
using JobScout.Core.Interfaces;
using JobScout.Infrastructure.AI;
using JobScout.Infrastructure.Configuration;
using JobScout.Infrastructure.Data;
using JobScout.Infrastructure.Email;
using JobScout.Infrastructure.ExternalServices;
using JobScout.Infrastructure.Identity;
using JobScout.Infrastructure.Repositories;
using JobScout.Infrastructure.Scheduling;
using JobScout.Infrastructure.Services;
using Microsoft.AspNetCore.Authentication.JwtBearer;
using Microsoft.AspNetCore.DataProtection;
using Microsoft.AspNetCore.Identity;
using Microsoft.EntityFrameworkCore;
using Microsoft.IdentityModel.Tokens;
using Scalar.AspNetCore;

// Bootstrap the local data directory before anything else needs it.
JobScoutPaths.EnsureExists();
var localConfig = new LocalConfigStore();

var builder = WebApplication.CreateBuilder(args);

builder.Services.AddControllers()
    .AddJsonOptions(o =>
    {
        o.JsonSerializerOptions.Converters.Add(new System.Text.Json.Serialization.JsonStringEnumConverter());
    });
builder.Services.AddOpenApi(options =>
{
    options.AddDocumentTransformer((document, context, cancellationToken) =>
    {
        document.Info.Title = "JobScout API";
        document.Info.Version = "v1";
        return Task.CompletedTask;
    });
});

builder.Services.AddProblemDetails();

// Identity
builder.Services.AddIdentityCore<ApplicationUser>(options =>
{
    options.Password.RequireDigit = true;
    options.Password.RequireLowercase = true;
    options.Password.RequireUppercase = true;
    options.Password.RequiredLength = 8;
    options.Password.RequireNonAlphanumeric = false;
    options.User.RequireUniqueEmail = true;
})
.AddEntityFrameworkStores<JobScoutDbContext>()
.AddDefaultTokenProviders();

// JWT signing key — prefer explicit config (env / appsettings / user secrets),
// otherwise generate one and persist it under the local data directory so subsequent
// runs reuse it. This makes the app boot cleanly on first run with zero setup.
var jwtKey = builder.Configuration["Jwt:Key"];
if (string.IsNullOrWhiteSpace(jwtKey))
    jwtKey = localConfig.GetOrCreate("Jwt:Key", () => LocalConfigStore.GenerateBase64Key(32));

var jwtIssuer = builder.Configuration["Jwt:Issuer"] ?? "JobScout";
var jwtAudience = builder.Configuration["Jwt:Audience"] ?? "JobScout";

// Surface the resolved values back into the configuration so the rest of the pipeline
// (AuthController in particular) reads the same values.
builder.Configuration["Jwt:Key"] = jwtKey;
builder.Configuration["Jwt:Issuer"] = jwtIssuer;
builder.Configuration["Jwt:Audience"] = jwtAudience;

builder.Services.AddSingleton(localConfig);

builder.Services.AddAuthentication(options =>
{
    options.DefaultAuthenticateScheme = JwtBearerDefaults.AuthenticationScheme;
    options.DefaultChallengeScheme = JwtBearerDefaults.AuthenticationScheme;
})
.AddJwtBearer(options =>
{
    options.TokenValidationParameters = new TokenValidationParameters
    {
        ValidateIssuer = true,
        ValidateAudience = true,
        ValidateLifetime = true,
        ValidateIssuerSigningKey = true,
        ValidIssuer = jwtIssuer,
        ValidAudience = jwtAudience,
        IssuerSigningKey = new SymmetricSecurityKey(Encoding.UTF8.GetBytes(jwtKey))
    };
});

builder.Services.AddAuthorization();
builder.Services.AddHttpContextAccessor();

// Data Protection key ring persisted alongside other local data so encrypted secrets
// survive restarts. Application name pin ensures different installs don't share keys.
builder.Services.AddDataProtection()
    .PersistKeysToFileSystem(new DirectoryInfo(JobScoutPaths.DataProtectionKeyRingPath))
    .SetApplicationName("JobScout");

builder.Services.AddScoped<ISecretStore, SecretStore>();

// CORS only needed when the Blazor dev server is running separately (rare).
// When hosted by this API (default), the SPA is same-origin and CORS isn't in the path.
if (builder.Environment.IsDevelopment())
{
    builder.Services.AddCors(options =>
    {
        options.AddPolicy("BlazorClient", policy =>
        {
            policy
                .WithOrigins("https://localhost:7036", "http://localhost:5079")
                .AllowAnyMethod()
                .AllowAnyHeader()
                .AllowCredentials();
        });
    });
}

// Default the SQLite file to live alongside other JobScout local data, so the source tree
// stays clean and the same data persists regardless of where the user starts `dotnet run`.
var connectionString = builder.Configuration.GetConnectionString("DefaultConnection");
if (string.IsNullOrWhiteSpace(connectionString))
    connectionString = $"Data Source={JobScoutPaths.DatabasePath}";

builder.Services.AddDbContext<JobScoutDbContext>(options => options.UseSqlite(connectionString));

// Auth services
builder.Services.AddScoped<ICurrentUserService, CurrentUserService>();

// Repositories
builder.Services.AddScoped<IJobRepository, JobRepository>();
builder.Services.AddScoped<IProfileRepository, ProfileRepository>();
builder.Services.AddScoped<IApplicationRepository, ApplicationRepository>();
builder.Services.AddScoped<IMetricsService, MetricsService>();
builder.Services.AddScoped<IApplicationTrackingService, ApplicationTrackingService>();
builder.Services.AddScoped<INotificationService, NotificationService>();

// Email — single implementation; degrades to a no-op log when no SendGrid key is configured.
builder.Services.AddScoped<IEmailSender, SendGridEmailSender>();

// Job board clients
builder.Services.AddHttpClient<RemoteOkClient>()
    .ConfigureHttpClient(c => c.DefaultRequestHeaders.UserAgent.ParseAdd("JobScout/1.0"));
builder.Services.AddHttpClient<AdzunaClient>();
builder.Services.AddHttpClient<TheMuseClient>();
builder.Services.AddHttpClient<SerpApiLinkedInClient>();
builder.Services.AddHttpClient<SerpApiIndeedClient>();
builder.Services.AddHttpClient<SerpApiGoogleJobsClient>();
builder.Services.AddHttpClient<DiceClient>()
    .ConfigureHttpClient(c => c.DefaultRequestHeaders.UserAgent.ParseAdd("JobScout/1.0"));
builder.Services.AddHttpClient<WellfoundClient>();
builder.Services.AddHttpClient<CustomSourceClient>()
    .ConfigureHttpClient(c => c.DefaultRequestHeaders.UserAgent.ParseAdd("JobScout/1.0"));

builder.Services.AddTransient<IJobBoardClient, RemoteOkClient>();
builder.Services.AddTransient<IJobBoardClient, AdzunaClient>();
builder.Services.AddTransient<IJobBoardClient, TheMuseClient>();
builder.Services.AddTransient<IJobBoardClient, SerpApiLinkedInClient>();
builder.Services.AddTransient<IJobBoardClient, SerpApiIndeedClient>();
builder.Services.AddTransient<IJobBoardClient, SerpApiGoogleJobsClient>();
builder.Services.AddTransient<IJobBoardClient, DiceClient>();
builder.Services.AddTransient<IJobBoardClient, WellfoundClient>();
builder.Services.AddTransient<IJobBoardClient, CustomSourceClient>();

// Ingestion + AI
builder.Services.AddScoped<IDeduplicationService, DeduplicationService>();
builder.Services.AddScoped<IJobIngestionService, JobIngestionService>();
builder.Services.AddSingleton<IAnthropicClientFactory, AnthropicClientFactory>();
builder.Services.AddScoped<IAiScoringService, ClaudeAiScoringService>();

// Scheduled background services (replaces Azure Functions for local-only mode)
builder.Services.AddHostedService<JobIngestionScheduler>();
builder.Services.AddHostedService<DailyDigestScheduler>();
builder.Services.AddHostedService<WeeklySummaryScheduler>();

var app = builder.Build();

// Apply EF migrations on every startup so non-developers never need `dotnet ef`.
using (var scope = app.Services.CreateScope())
{
    var db = scope.ServiceProvider.GetRequiredService<JobScoutDbContext>();
    await db.Database.MigrateAsync();
}

if (app.Environment.IsDevelopment())
{
    app.MapOpenApi();
    app.MapScalarApiReference();

    await DbSeeder.SeedAsync(app.Services);
}

app.UseExceptionHandler();
app.UseStatusCodePages();

app.UseHttpsRedirection();

if (app.Environment.IsDevelopment())
    app.UseCors("BlazorClient");

// Serve the Blazor WASM client from this same host. Compose a file provider that
// pulls from the Web project's two static-asset locations:
//   1) source wwwroot (index.html, CSS, images, manifest.json)
//   2) compiled wwwroot/_framework (the WASM bundle + boot config)
// We probe both Debug and Release build configs so this works regardless of how
// the user ran `dotnet build`.
{
    var webProjectRoot = Path.GetFullPath(Path.Combine(builder.Environment.ContentRootPath, "..", "JobScout.Web"));
    var candidateCompiled = new[]
    {
        Path.Combine(webProjectRoot, "bin", "Debug", "net10.0", "wwwroot"),
        Path.Combine(webProjectRoot, "bin", "Release", "net10.0", "wwwroot"),
    };
    var sourceWebRoot = Path.Combine(webProjectRoot, "wwwroot");

    var fileProviders = new List<Microsoft.Extensions.FileProviders.IFileProvider>();
    foreach (var dir in candidateCompiled)
    {
        if (Directory.Exists(dir))
        {
            fileProviders.Add(new Microsoft.Extensions.FileProviders.PhysicalFileProvider(dir));
            break;
        }
    }
    if (Directory.Exists(sourceWebRoot))
        fileProviders.Add(new Microsoft.Extensions.FileProviders.PhysicalFileProvider(sourceWebRoot));

    if (fileProviders.Count > 0)
    {
        var composite = new Microsoft.Extensions.FileProviders.CompositeFileProvider(fileProviders);
        app.UseStaticFiles(new StaticFileOptions { FileProvider = composite });
        // Override the WebRoot file provider too so MapFallbackToFile("index.html") finds it.
        app.Environment.WebRootFileProvider = composite;
    }
    else
    {
        app.UseStaticFiles();
    }
}

app.UseAuthentication();
app.UseAuthorization();
app.MapControllers();

// SPA fallback: anything not /api/* should serve the Blazor index.html.
app.MapFallbackToFile("index.html");

app.Run();

public partial class Program { }

