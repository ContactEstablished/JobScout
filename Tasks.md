# Phase 8: Local-First Setup

**Priority: HIGH** | **Branch:** `phase8/local-first`

> Replaces the original Phase 8 (Azure Deployment) — JobScout is now intended for local-only use. Users download the repo, install .NET 10, and run a single command. No cloud dependencies.

---

## Status Overview

| # | Task | Status |
|---|------|--------|
| 8.1 | Move scheduled jobs from Azure Functions to `BackgroundService` in the API | DONE |
| 8.2 | Delete `functions/JobScout.Functions/` project + solution references | DONE |
| 8.3 | Host Blazor WASM from the API (single process, drop CORS) | DONE |
| 8.4 | Sensible config defaults — auto-generate JWT key, default DB path, graceful key-missing | DONE |
| 8.5 | API-keys settings UI (Anthropic, SerpAPI, SendGrid in `/settings`) | DONE |
| 8.6 | Cross-platform launcher scripts (`start.ps1`, `start.sh`) + VS Code launch config | DONE |
| 8.7 | First-run setup wizard (create admin user + enter keys) | DONE |
| 8.8 | README rewrite — single quickstart page | DONE |
| 8.9 | Build + test verification (0 warnings, 0 errors, 93+ tests green) | DONE |
| 8.10 | Commit & PR | DONE |

---

## Existing Infrastructure

The following already exist and inform the redesign:

- **`functions/JobScout.Functions/`** — three timer-triggered functions (`JobIngestionFunction` every 4h, `DailyDigestFunction` 13:00 UTC, `WeeklySummaryFunction` Mon 14:00 UTC) plus the `ManualIngestionFunction` HTTP trigger. The timer logic is plain async code that will translate 1:1 into `BackgroundService`. The manual HTTP trigger is already duplicated by `POST /api/profiles/{id}/ingest`.
- **`src/JobScout.Api/Program.cs`** — currently throws if `Jwt:Key` is missing and reads it via `builder.Configuration["Jwt:Key"] ?? throw`. Defaults the SQLite path to `Data Source=jobscout.db` via `appsettings.json`. CORS allows only `localhost:7036` and `localhost:5079`.
- **`src/JobScout.Web/Program.cs`** — Blazor WASM bootstrap. Reads `ApiBaseUrl` from `wwwroot/appsettings.json`. Currently runs as its own dev server.
- **`src/JobScout.Web/wwwroot/index.html`** — the static WASM entry point that the API will serve.
- **`Settings.razor`** — Phase 6 page at `/settings` with the notification preferences UI. We'll extend it with an "Integrations" section for API keys.
- **`NotificationPreferences` entity** — pattern for per-user key-value storage. The API-key store will likely be a new entity, since keys are encrypted and could be system-wide rather than per-user.
- **`DbSeeder`** — already wires a dev profile/user in development. We'll repurpose it to also generate the JWT key on first run.

---

## Task Details

### 8.1 Move Scheduled Jobs to `BackgroundService`

**Files to create:**
- `src/JobScout.Infrastructure/Scheduling/JobIngestionScheduler.cs`
- `src/JobScout.Infrastructure/Scheduling/DailyDigestScheduler.cs`
- `src/JobScout.Infrastructure/Scheduling/WeeklySummaryScheduler.cs`

**Files to modify:**
- `src/JobScout.Api/Program.cs` — register the three `HostedService`s

**Requirements:**
- [ ] Each scheduler inherits from `BackgroundService` and runs the same logic the Function did
- [ ] Use `IServiceScopeFactory` to resolve scoped services (`JobScoutDbContext`, `IJobIngestionService`, `IEmailSender`, etc.) per tick
- [ ] `JobIngestionScheduler`: every 4 hours, run ingestion + scoring for each active profile (matches `JobIngestionFunction`)
- [ ] `DailyDigestScheduler`: at the next 13:00 UTC, then every 24h, dispatch digests
- [ ] `WeeklySummaryScheduler`: at the next Monday 14:00 UTC, then every 7d, dispatch summaries
- [ ] Log start, tick, and any exception per scheduler
- [ ] Honor `CancellationToken` so graceful shutdown works (`Ctrl+C` in dev)
- [ ] Add an opt-out via configuration: `Scheduling:Enabled` (default `true`); useful in tests and dev

**Acceptance criteria:** Starting the API logs three "scheduler started" lines. Killing the API stops all three cleanly. Setting `Scheduling:Enabled=false` keeps them dormant.

---

### 8.2 Delete the Functions Project

**Files to delete:**
- `functions/JobScout.Functions/` (entire directory)
- `functions/` folder if empty after that

**Files to modify:**
- `JobScout.slnx` — remove the `<Project Path="functions/JobScout.Functions/..." />` entry
- `README.md` — remove any references to Azure Functions / Functions Core Tools

**Requirements:**
- [ ] Solution builds with 0 errors after removal
- [ ] No references in any other csproj
- [ ] `dotnet test` still passes
- [ ] No leftover Function-specific NuGet packages in other projects (none currently — Functions packages are isolated to that csproj)

**Acceptance criteria:** `find . -name "*.cs" | xargs grep -l "Microsoft.Azure.Functions"` returns nothing. Solution loads cleanly.

---

### 8.3 Host Blazor WASM from the API

**Files to modify:**
- `src/JobScout.Api/JobScout.Api.csproj` — add `<ProjectReference Include="..\JobScout.Web\JobScout.Web.csproj" />`
- `src/JobScout.Api/Program.cs` — add `app.UseBlazorFrameworkFiles()` + `app.UseStaticFiles()` + `app.MapFallbackToFile("index.html")`; drop the CORS policy (no longer needed since same-origin)
- `src/JobScout.Web/wwwroot/appsettings.json` — change `ApiBaseUrl` to empty string (same-origin requests)
- `src/JobScout.Web/Program.cs` — fall back to `builder.HostEnvironment.BaseAddress` when `ApiBaseUrl` is empty
- `src/JobScout.Api/Properties/launchSettings.json` — set the single dev URL to `http://localhost:5000` (and HTTPS variant)

**Requirements:**
- [ ] `dotnet run --project src/JobScout.Api` boots the API and serves the Blazor app at the root URL
- [ ] All `/api/*` calls hit the same origin — no CORS preflight, no port juggling
- [ ] The standalone `dotnet run --project src/JobScout.Web` path still works for dev iteration on UI (Web project keeps its own launchSettings)
- [ ] Static asset caching headers behave (this comes free with `UseStaticFiles`)

**Acceptance criteria:** Single command launches the app. Hitting `http://localhost:5000` shows the login screen. Hitting `http://localhost:5000/api/profiles` returns JSON. Refresh on any route loads the SPA shell.

---

### 8.4 Sensible Config Defaults

**Files to create:**
- `src/JobScout.Infrastructure/Configuration/LocalConfigStore.cs` — reads/writes `~/.jobscout/local.json`
- `src/JobScout.Infrastructure/Configuration/JobScoutPaths.cs` — centralized `LocalDataDirectory` (e.g. `~/.jobscout` on Unix, `%LOCALAPPDATA%\JobScout` on Windows)

**Files to modify:**
- `src/JobScout.Api/Program.cs` — replace the throwing `Jwt:Key` read with: 1) check config, 2) check env, 3) load from `LocalConfigStore`, 4) generate + persist
- `src/JobScout.Api/appsettings.json` — change `Jwt:Key` to empty, `ConnectionStrings:DefaultConnection` to `Data Source={LocalDataDirectory}/jobscout.db`
- `src/JobScout.Api/appsettings.Development.json` — keep dev-friendly defaults but no hardcoded secrets

**Requirements:**
- [ ] First boot with no config: app generates a 256-bit random key, writes it to `{LocalDataDirectory}/local.json`, and continues without throwing
- [ ] Subsequent boots reuse the persisted key
- [ ] `LocalDataDirectory` is created if it doesn't exist
- [ ] `LocalConfigStore` is thread-safe and lazy
- [ ] Connection string resolves the SQLite file to `{LocalDataDirectory}/jobscout.db` so the DB sits next to the key, outside the source tree
- [ ] EF migrations run automatically on startup in **all** environments (today they only run in `Development` via `DbSeeder`) — non-devs shouldn't have to run `dotnet ef`

**Acceptance criteria:** Delete `~/.jobscout`, run the API, the directory is recreated with `local.json` + `jobscout.db`. Run a second time and the same key is used (verifiable by issuing a JWT and confirming it still validates).

---

### 8.5 API-Keys Settings UI

**Files to create:**
- `src/JobScout.Core/Models/AppSecret.cs` — `{ string Key, string EncryptedValue, DateTime UpdatedAt }` (system-wide for v1; per-user later if multi-user becomes a real workflow)
- `src/JobScout.Core/DTOs/IntegrationSettingsDto.cs` — `{ string? AnthropicApiKey, string? SerpApiKey, string? SendGridApiKey, string? SendGridFromAddress }`
- `src/JobScout.Infrastructure/Configuration/SecretStore.cs` + interface — encrypts via ASP.NET Data Protection API (built-in, keyed off the local config directory)
- EF Core migration: `AddAppSecrets`

**Files to modify:**
- `src/JobScout.Api/Controllers/SettingsController.cs` — add `GET /api/settings/integrations` (returns masked values) and `PUT /api/settings/integrations`
- `src/JobScout.Web/Pages/Settings.razor` — add an "Integrations" card with three masked input fields
- `src/JobScout.Web/Services/NotificationsService.cs` (or new `SettingsService`) — methods for the new endpoint
- `src/JobScout.Infrastructure/AI/ClaudeAiScoringService.cs` — read API key from `ISecretStore` first, then `IConfiguration` (backward compat)
- `src/JobScout.Infrastructure/ExternalServices/SerpApi*Client.cs` — same fallback
- `src/JobScout.Infrastructure/Email/SendGridEmailSender.cs` — same fallback

**Requirements:**
- [ ] Values stored encrypted at rest via Data Protection API; key ring written under `{LocalDataDirectory}/dpapi-keys/`
- [ ] `GET` returns the **last 4 characters** only (e.g. `…abcd`); never echoes the full secret
- [ ] `PUT` with an empty string clears that secret; with a non-empty value replaces it
- [ ] `SecretStore.GetAsync(key)` returns the decrypted value or null; called services use this in place of `IConfiguration["Anthropic:ApiKey"]`
- [ ] Migration `AddAppSecrets` creates the `AppSecrets` table with `Key` as PK
- [ ] If the DB key is missing, fall back to `IConfiguration` so existing setups (env vars / appsettings) keep working

**Acceptance criteria:** Enter a fake Anthropic key in the UI → save → restart the app → scoring service picks up the saved key (verifiable via Scalar by triggering a recalibrate and seeing the AI scoring path take effect rather than the "no key" default branch).

---

### 8.6 Launcher Scripts + VS Code Config

**Files to create:**
- `start.ps1` — Windows
- `start.sh` — Unix (chmod +x committed)
- `.vscode/launch.json` — F5 → run the API with browser open
- `.vscode/tasks.json` — build, watch, test
- `.vscode/extensions.json` — recommend `ms-dotnettools.csdevkit`

**Requirements:**
- [ ] `start.ps1` / `start.sh`: check for `dotnet` ≥ 10, run `dotnet build`, run `dotnet run --project src/JobScout.Api`, open the default browser to `http://localhost:5000` after a 2s delay
- [ ] Both scripts exit cleanly on `Ctrl+C` (kill the `dotnet` child)
- [ ] `.vscode/launch.json` includes a "Run JobScout" entry that builds + launches + opens the browser
- [ ] `.vscode/tasks.json` exposes `build`, `test`, `clean` tasks
- [ ] If .NET isn't installed, both scripts print a clear message with the install link, exit non-zero

**Acceptance criteria:** A fresh clone + `./start.sh` (or `.\start.ps1`) puts the user at the login screen in their default browser.

---

### 8.7 First-Run Setup Wizard

**Files to create:**
- `src/JobScout.Web/Pages/Setup.razor` — three-step wizard: account → API keys (optional) → first profile
- `src/JobScout.Api/Controllers/SetupController.cs` — `GET /api/setup/status` (returns `{ needsSetup: bool }`) and `POST /api/setup/complete`
- `src/JobScout.Web/Auth/SetupGuard.razor` — top-level route guard that redirects to `/setup` when needed

**Files to modify:**
- `src/JobScout.Web/App.razor` — wrap routing with `SetupGuard`
- `src/JobScout.Web/Pages/Login.razor` — if `needsSetup`, redirect to `/setup` instead of showing login

**Requirements:**
- [ ] `GET /api/setup/status` is unauthenticated; returns `true` when there are zero `ApplicationUser`s
- [ ] `POST /api/setup/complete` creates the first user, optionally writes API keys via `ISecretStore`, optionally creates a default `SearchProfile`, returns a JWT — all in one transaction
- [ ] After setup, status flips to `false` and the wizard is unreachable (route guard sends users to the home feed)
- [ ] Subsequent users register normally via `/register`

**Acceptance criteria:** Delete `jobscout.db`, restart, the app opens directly into `/setup`. Complete the wizard, get redirected to the feed, can sign out and back in. A second visit to `/setup` redirects away.

---

### 8.8 README Rewrite

**Files to modify:**
- `README.md` — rewrite from scratch

**Requirements:**
- [ ] One-page quickstart at the top: **install .NET 10 → clone → `./start.sh`** (or `.\start.ps1`)
- [ ] Screenshot of the first-run wizard
- [ ] "What you'll need" list: only .NET 10. Optional: Anthropic key for AI scoring, SerpAPI key for LinkedIn/Indeed/Google Jobs, SendGrid key for email.
- [ ] "How it works" — brief architecture diagram showing single API process + SQLite + scheduled BackgroundServices
- [ ] "Where data lives" — point to `~/.jobscout` (or `%LOCALAPPDATA%\JobScout` on Windows)
- [ ] Drop all Azure mentions
- [ ] Link to a longer `DEVELOPING.md` (new) for `dotnet test`, schema changes, etc.

**Acceptance criteria:** A non-developer reading just the README can install .NET, run the app, log in, and see the feed.

---

### 8.9 Build & Test Verification

- [ ] `dotnet build` from solution root: 0 warnings, 0 errors
- [ ] `dotnet test` from solution root: all 93+ tests pass
- [ ] `./start.sh` (or `.\start.ps1`) on a clean clone takes the user from nothing to the login screen
- [ ] First-run wizard end-to-end works
- [ ] After completing setup, scoring/ingestion run on schedule without Azure Functions Core Tools installed
- [ ] CORS no longer in the request path (same-origin)

---

### 8.10 Commit & PR

- [ ] Stage all Phase 8 changes
- [ ] Commit with a descriptive message that calls out the architectural pivot (deleted Functions, added schedulers, hosted WASM, secret store)
- [ ] Push branch `phase8/local-first`
- [ ] Open PR targeting `main`
