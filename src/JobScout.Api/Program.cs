using System.Text;
using JobScout.Core.Interfaces;
using JobScout.Infrastructure.AI;
using JobScout.Infrastructure.Data;
using JobScout.Infrastructure.ExternalServices;
using JobScout.Infrastructure.Identity;
using JobScout.Infrastructure.Repositories;
using JobScout.Infrastructure.Services;
using Microsoft.AspNetCore.Authentication.JwtBearer;
using Microsoft.AspNetCore.Identity;
using Microsoft.EntityFrameworkCore;
using Microsoft.IdentityModel.Tokens;
using Scalar.AspNetCore;

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

// JWT Authentication
var jwtKey = builder.Configuration["Jwt:Key"]
    ?? throw new InvalidOperationException("Jwt:Key must be configured.");

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
        ValidIssuer = builder.Configuration["Jwt:Issuer"],
        ValidAudience = builder.Configuration["Jwt:Audience"],
        IssuerSigningKey = new SymmetricSecurityKey(Encoding.UTF8.GetBytes(jwtKey))
    };
});

builder.Services.AddAuthorization();
builder.Services.AddHttpContextAccessor();

builder.Services.AddCors(options =>
{
    options.AddPolicy("BlazorClient", policy =>
    {
        policy
            .WithOrigins(
                "https://localhost:7036",
                "http://localhost:5079",
                builder.Configuration["AllowedOrigins"] ?? "https://localhost:7036")
            .AllowAnyMethod()
            .AllowAnyHeader()
            .AllowCredentials();
    });
});

builder.Services.AddDbContext<JobScoutDbContext>(options =>
    options.UseSqlite(builder.Configuration.GetConnectionString("DefaultConnection")));

// Auth services
builder.Services.AddScoped<ICurrentUserService, CurrentUserService>();

// Repositories
builder.Services.AddScoped<IJobRepository, JobRepository>();
builder.Services.AddScoped<IProfileRepository, ProfileRepository>();
builder.Services.AddScoped<IApplicationRepository, ApplicationRepository>();
builder.Services.AddScoped<IMetricsService, MetricsService>();
builder.Services.AddScoped<IApplicationTrackingService, ApplicationTrackingService>();

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
builder.Services.AddScoped<IAiScoringService, ClaudeAiScoringService>();

var app = builder.Build();

if (app.Environment.IsDevelopment())
{
    app.MapOpenApi();
    app.MapScalarApiReference();

    await DbSeeder.SeedAsync(app.Services);
}

app.UseExceptionHandler();
app.UseStatusCodePages();

app.UseHttpsRedirection();
app.UseCors("BlazorClient");

app.UseAuthentication();
app.UseAuthorization();
app.MapControllers();

app.Run();
