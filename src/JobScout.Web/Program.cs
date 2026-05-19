using Microsoft.AspNetCore.Components.Web;
using Microsoft.AspNetCore.Components.WebAssembly.Hosting;
using JobScout.Web;
using JobScout.Web.Services;

var builder = WebAssemblyHostBuilder.CreateDefault(args);
builder.RootComponents.Add<App>("#app");
builder.RootComponents.Add<HeadOutlet>("head::after");

var apiBaseUrl = builder.Configuration["ApiBaseUrl"] ?? "https://localhost:7007";

builder.Services.AddSingleton<ProfileStateService>();
builder.Services.AddSingleton<ToastService>();
builder.Services.AddSingleton<FilterStateService>();

builder.Services.AddHttpClient<JobsService>(c => c.BaseAddress = new Uri(apiBaseUrl));
builder.Services.AddHttpClient<ProfilesService>(c => c.BaseAddress = new Uri(apiBaseUrl));
builder.Services.AddHttpClient<RatingsService>(c => c.BaseAddress = new Uri(apiBaseUrl));
builder.Services.AddHttpClient<MetricsService>(c => c.BaseAddress = new Uri(apiBaseUrl));

await builder.Build().RunAsync();
