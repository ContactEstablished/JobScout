using Azure.Monitor.OpenTelemetry.Exporter;
using JobScout.Core.Interfaces;
using JobScout.Infrastructure.AI;
using JobScout.Infrastructure.Data;
using JobScout.Infrastructure.Email;
using JobScout.Infrastructure.ExternalServices;
using JobScout.Infrastructure.Repositories;
using JobScout.Infrastructure.Services;
using Microsoft.Azure.Functions.Worker;
using Microsoft.Azure.Functions.Worker.Builder;
using Microsoft.Azure.Functions.Worker.OpenTelemetry;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Hosting;
using OpenTelemetry;

var builder = FunctionsApplication.CreateBuilder(args);

builder.ConfigureFunctionsWebApplication();

builder.Services.AddOpenTelemetry()
    .UseFunctionsWorkerDefaults()
    .UseAzureMonitorExporter();

// Database
builder.Services.AddDbContext<JobScoutDbContext>(options =>
    options.UseSqlite(builder.Configuration["ConnectionStrings:DefaultConnection"]));

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
builder.Services.AddScoped<INotificationService, NotificationService>();

// Email
if (!string.IsNullOrWhiteSpace(builder.Configuration["SendGrid:ApiKey"]))
    builder.Services.AddSingleton<IEmailSender, SendGridEmailSender>();
else
    builder.Services.AddSingleton<IEmailSender, NullEmailSender>();

builder.Build().Run();
