# Developing JobScout

Notes for contributors.

## Prerequisites

- .NET 10 SDK
- `dotnet-ef` global tool (`dotnet tool install --global dotnet-ef`)

## Project layout

- `src/JobScout.Core` — pure domain layer. No EF, no HTTP, no ASP.NET dependencies.
- `src/JobScout.Infrastructure` — EF DbContext, repositories, scoring service, scheduling, email, job board clients.
- `src/JobScout.Api` — ASP.NET Core API. Also serves the Blazor WebAssembly client and runs the scheduled background services.
- `src/JobScout.Web` — Blazor WebAssembly UI.
- `tests/` — Core / Infrastructure / Api / Web test projects (xUnit + bUnit).

## Running

`./start.sh` or `.\start.ps1` from the repo root is the easiest path. Under the hood that runs:

```
dotnet build
dotnet run --project src/JobScout.Api/JobScout.Api.csproj --launch-profile JobScout
```

VS Code: `F5` (the launch config builds first via the `build` task).

## Tests

```
dotnet test
```

The four test projects total ~93 tests and run in under 5 seconds. Use `dotnet test --filter` to scope to a single project or class.

## Database migrations

```
cd src/JobScout.Infrastructure
dotnet ef migrations add YourMigrationName --startup-project ../JobScout.Api
```

Migrations are applied automatically on every API startup, so users never need to run `dotnet ef database update`.

## Adding a new job board client

1. Create `XyzClient : IJobBoardClient` under `src/JobScout.Infrastructure/ExternalServices/`. Inject `HttpClient`, `ISecretStore`, and `ILogger`. Read your API key via `await secrets.GetAsync("Xyz:ApiKey", ct)`.
2. Register in `src/JobScout.Api/Program.cs` with both `AddHttpClient<XyzClient>()` and `AddTransient<IJobBoardClient, XyzClient>()`.
3. Add the source to `JobScout.Core.Enums.JobSource` if it's not already there.
4. If the source needs credentials, surface the field in `IntegrationSettingsDto` and `Settings.razor`.

## Secret storage

API keys land in the `AppSecrets` table, encrypted with the ASP.NET Data Protection key ring under `{LocalDataDirectory}/dpapi-keys/`. `ISecretStore.GetAsync(key)` falls back to `IConfiguration` when the DB has no entry, so the legacy env-var / appsettings path still works.

## Local data directory

`JobScout.Infrastructure.Configuration.JobScoutPaths.LocalDataDirectory` resolves to `~/.jobscout` (Unix) or `%LOCALAPPDATA%\JobScout` (Windows). The SQLite file, encryption key ring, and `local.json` (JWT key) all live there. Delete the directory to completely reset the install.

## CORS

CORS is only configured in `Development` and only for two specific origins (`https://localhost:7036`, `http://localhost:5079`) — those are the Blazor WASM dev server addresses. Production / single-process mode is same-origin, no CORS in the request path.
