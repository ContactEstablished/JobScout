using JobScout.Core.Interfaces;
using JobScout.Infrastructure.AI;
using JobScout.Infrastructure.Data;
using JobScout.Infrastructure.ExternalServices;
using JobScout.Infrastructure.Repositories;
using JobScout.Infrastructure.Services;
using Microsoft.EntityFrameworkCore;
using Scalar.AspNetCore;

var builder = WebApplication.CreateBuilder(args);

builder.Services.AddControllers();
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
            .AllowAnyHeader();
    });
});

builder.Services.AddDbContext<JobScoutDbContext>(options =>
    options.UseSqlite(builder.Configuration.GetConnectionString("DefaultConnection")));

// Repositories
builder.Services.AddScoped<IJobRepository, JobRepository>();
builder.Services.AddScoped<IProfileRepository, ProfileRepository>();
builder.Services.AddScoped<IMetricsService, MetricsService>();

// Job board clients
builder.Services.AddHttpClient<RemoteOkClient>()
    .ConfigureHttpClient(c => c.DefaultRequestHeaders.UserAgent.ParseAdd("JobScout/1.0"));
builder.Services.AddHttpClient<AdzunaClient>();
builder.Services.AddHttpClient<TheMuseClient>();
builder.Services.AddHttpClient<SerpApiLinkedInClient>();

builder.Services.AddTransient<IJobBoardClient, RemoteOkClient>();
builder.Services.AddTransient<IJobBoardClient, AdzunaClient>();
builder.Services.AddTransient<IJobBoardClient, TheMuseClient>();
builder.Services.AddTransient<IJobBoardClient, SerpApiLinkedInClient>();

// Ingestion + AI
builder.Services.AddScoped<IJobIngestionService, JobIngestionService>();
builder.Services.AddHttpClient<ClaudeAiScoringService>();
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

app.UseAuthorization();
app.MapControllers();

app.Run();
