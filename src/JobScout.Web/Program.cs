using Microsoft.AspNetCore.Components.Authorization;
using Microsoft.AspNetCore.Components.Web;
using Microsoft.AspNetCore.Components.WebAssembly.Hosting;
using JobScout.Web;
using JobScout.Web.Auth;
using JobScout.Web.Services;

var builder = WebAssemblyHostBuilder.CreateDefault(args);
builder.RootComponents.Add<App>("#app");
builder.RootComponents.Add<HeadOutlet>("head::after");

// When the API hosts the SPA (the default local setup), ApiBaseUrl is empty
// and we use the page's own origin via HostEnvironment.BaseAddress.
// During standalone dev (Blazor's own dev server), set ApiBaseUrl in wwwroot/appsettings.json.
var configuredApi = builder.Configuration["ApiBaseUrl"];
var apiBaseUrl = string.IsNullOrWhiteSpace(configuredApi)
    ? builder.HostEnvironment.BaseAddress
    : configuredApi;

// Auth
builder.Services.AddScoped<JwtAuthenticationStateProvider>();
builder.Services.AddScoped<AuthenticationStateProvider>(sp =>
    sp.GetRequiredService<JwtAuthenticationStateProvider>());
builder.Services.AddAuthorizationCore();
builder.Services.AddTransient<AuthTokenHandler>();

builder.Services.AddSingleton<ProfileStateService>();
builder.Services.AddSingleton<ToastService>();
builder.Services.AddSingleton<FilterStateService>();

// HTTP clients with auth token handler
builder.Services.AddHttpClient<AuthService>(c => c.BaseAddress = new Uri(apiBaseUrl));

builder.Services.AddHttpClient<JobsService>(c => c.BaseAddress = new Uri(apiBaseUrl))
    .AddHttpMessageHandler<AuthTokenHandler>();
builder.Services.AddHttpClient<ProfilesService>(c => c.BaseAddress = new Uri(apiBaseUrl))
    .AddHttpMessageHandler<AuthTokenHandler>();
builder.Services.AddHttpClient<RatingsService>(c => c.BaseAddress = new Uri(apiBaseUrl))
    .AddHttpMessageHandler<AuthTokenHandler>();
builder.Services.AddHttpClient<MetricsService>(c => c.BaseAddress = new Uri(apiBaseUrl))
    .AddHttpMessageHandler<AuthTokenHandler>();
builder.Services.AddHttpClient<ApplicationsService>(c => c.BaseAddress = new Uri(apiBaseUrl))
    .AddHttpMessageHandler<AuthTokenHandler>();
builder.Services.AddHttpClient<NotificationsService>(c => c.BaseAddress = new Uri(apiBaseUrl))
    .AddHttpMessageHandler<AuthTokenHandler>();
builder.Services.AddHttpClient<SetupService>(c => c.BaseAddress = new Uri(apiBaseUrl));

await builder.Build().RunAsync();
