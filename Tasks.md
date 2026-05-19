# JobScout — Development Task List

**Stack:** Blazor WebAssembly · ASP.NET Core 8 Web API · Entity Framework Core · SQLite (dev) · Azure Functions · Claude AI API · SerpAPI

**Repo:** https://github.com/ContactEstablished/JobScout

**UI Reference:** `jobscout-responsive-template.html` — a fully-styled dark-theme HTML mockup with CSS variables,
job card components, filter panel, sidebar nav, mobile drawer, and posting-window chart. Use this as the
direct visual spec for all Blazor component work.

---

## Solution Structure (target)

```
JobScout.sln
├── src/
│   ├── JobScout.Web/           # Blazor WebAssembly (client)
│   ├── JobScout.Api/           # ASP.NET Core 8 Web API (host + WASM server)
│   ├── JobScout.Core/          # Shared models, DTOs, interfaces (no dependencies)
│   └── JobScout.Infrastructure/# EF Core, repositories, external service clients
└── functions/
    └── JobScout.Functions/     # Azure Functions — timer-triggered job ingestion
```

---

## Phase 1 — Solution & Project Scaffolding

### Task 1.1 — Create the .NET solution and projects
- Create a blank solution: `JobScout.sln`
- Add four projects:
  - `src/JobScout.Core` — Class Library, .NET 8, no external NuGet deps
  - `src/JobScout.Infrastructure` — Class Library, .NET 8
  - `src/JobScout.Api` — ASP.NET Core Web API, .NET 8 (also serves Blazor WASM)
  - `src/JobScout.Web` — Blazor WebAssembly Standalone App, .NET 8
  - `functions/JobScout.Functions` — Azure Functions v4, .NET 8 isolated worker
- Add project references:
  - `JobScout.Api` → `JobScout.Core`, `JobScout.Infrastructure`
  - `JobScout.Infrastructure` → `JobScout.Core`
  - `JobScout.Web` → `JobScout.Core`
  - `JobScout.Functions` → `JobScout.Core`, `JobScout.Infrastructure`
- Add a root `.gitignore` (Visual Studio / .NET standard)
- Add a root `README.md` with a brief project description

### Task 1.2 — Install NuGet packages
**JobScout.Infrastructure:**
- `Microsoft.EntityFrameworkCore.Sqlite`
- `Microsoft.EntityFrameworkCore.Design`
- `Anthropic.SDK` (or `Anthropic.NET.SDK` — verify package name on NuGet)
- `Microsoft.Extensions.Http`

**JobScout.Api:**
- `Microsoft.EntityFrameworkCore.Tools`
- `Swashbuckle.AspNetCore`

**JobScout.Functions:**
- `Microsoft.Azure.Functions.Worker`
- `Microsoft.Azure.Functions.Worker.Extensions.Timer`
- `Microsoft.Azure.Functions.Worker.Extensions.Http`
- `Microsoft.Extensions.Http`

**JobScout.Web:**
- No additional packages in Phase 1

### Task 1.3 — Configure appsettings and user secrets
- In `JobScout.Api/appsettings.Development.json`, add placeholder sections:
  ```json
  {
    "ConnectionStrings": {
      "DefaultConnection": "Data Source=jobscout.db"
    },
    "SerpApi": {
      "ApiKey": ""
    },
    "Anthropic": {
      "ApiKey": ""
    },
    "Adzuna": {
      "AppId": "",
      "AppKey": ""
    }
  }
  ```
- Enable user secrets on `JobScout.Api` and `JobScout.Functions`
- Document in README that API keys go in user secrets, never in committed config

---

## Phase 2 — Core Domain Models

### Task 2.1 — Define entity models in `JobScout.Core/Models/`

**`SearchProfile.cs`**
```
Id (Guid)
Name (string)                  // "Software Engineering", "Photography Gigs"
Description (string?)
ResumeText (string?)           // Parsed plain-text resume content
ResumeFileName (string?)       // Original filename for display
LinkedInUrl (string?)          // Optional LinkedIn profile URL
CreatedAt (DateTime)
UpdatedAt (DateTime)
IsActive (bool)
-- Navigation --
Jobs (ICollection<Job>)
AiScores (ICollection<AiScore>)
UserRatings (ICollection<UserRating>)
DailyMetrics (ICollection<DailyMetric>)
```

**`Job.cs`**
```
Id (Guid)
ExternalId (string)            // Source-specific ID for deduplication
Title (string)
Company (string)
Location (string?)
LocationType (enum: Remote, Hybrid, OnSite)
JobType (enum: FullTime, Contract, PartTime, Freelance)
Description (string)           // Full job description text
Tags (string)                  // JSON-serialized string[] of tech tags
Salary (string?)               // Raw salary string as scraped
PostedAt (DateTime?)
DiscoveredAt (DateTime)        // When JobScout first saw it
Source (enum: LinkedIn, Indeed, Glassdoor, Dice, Wellfound, RemoteOK, Adzuna, TheMuse)
SourceUrl (string)
IsActive (bool)
-- Navigation --
AiScores (ICollection<AiScore>)
UserRatings (ICollection<UserRating>)
```

**`AiScore.cs`**
```
Id (Guid)
JobId (Guid)
ProfileId (Guid)
Score (decimal)                // 1.0–10.0
Reasoning (string)             // AI explanation paragraph
MatchedKeywords (string)       // JSON-serialized string[]
ScoredAt (DateTime)
ModelVersion (string)          // e.g. "claude-sonnet-4-20250514"
-- Navigation --
Job (Job)
Profile (SearchProfile)
```

**`UserRating.cs`**
```
Id (Guid)
JobId (Guid)
ProfileId (Guid)
Stars (int)                    // 1–5
Notes (string?)                // Optional user comment
RatedAt (DateTime)
-- Navigation --
Job (Job)
Profile (SearchProfile)
```

**`DailyMetric.cs`**
```
Id (Guid)
ProfileId (Guid)
Date (DateOnly)
Source (enum: same as Job.Source)
JobsFound (int)
StrongFits (int)               // AiScore >= 7.0
UserLiked (int)                // UserRating >= 4
Applied (int)
-- Navigation --
Profile (SearchProfile)
```

**`JobApplication.cs`**
```
Id (Guid)
JobId (Guid)
ProfileId (Guid)
AppliedAt (DateTime)
Status (enum: Applied, Interviewing, Offered, Rejected, Withdrawn)
Notes (string?)
```

### Task 2.2 — Define DTOs in `JobScout.Core/DTOs/`
Create request/response DTOs (separate from EF entities) for:
- `JobSummaryDto` — card-level fields (no full description)
- `JobDetailDto` — full job including description
- `SearchProfileDto` / `CreateProfileRequest` / `UpdateProfileRequest`
- `AiScoreDto`
- `UserRatingRequest` / `UserRatingDto`
- `DashboardStatsDto` — This Week stats (jobs found, strong fits, saved, applied)
- `SourceBreakdownDto` — per-source job counts
- `PostingWindowDto` — day-of-week distribution data
- `RecalibrateRequest` — profileId + optional "reset history" flag

### Task 2.3 — Define service interfaces in `JobScout.Core/Interfaces/`
- `IJobRepository`
- `IProfileRepository`
- `IAiScoringService`
- `IJobIngestionService`
- `IMetricsService`

---

## Phase 3 — Infrastructure & Data Layer

### Task 3.1 — EF Core DbContext
- Create `JobScoutDbContext` in `JobScout.Infrastructure/Data/`
- Register all entities with appropriate configurations:
  - `Job.Tags` stored as JSON string (use `HasConversion` or `[Column(TypeName = "TEXT")]`)
  - `AiScore.MatchedKeywords` same
  - Unique index on `(Job.ExternalId, Job.Source)` for deduplication
  - Unique index on `(UserRating.JobId, UserRating.ProfileId)` — one rating per job/profile
  - Unique index on `(DailyMetric.ProfileId, DailyMetric.Date, DailyMetric.Source)`
- Register `JobScoutDbContext` in `JobScout.Api/Program.cs` using SQLite connection string

### Task 3.2 — EF Core migrations
- Add initial migration: `dotnet ef migrations add InitialCreate --project JobScout.Infrastructure --startup-project JobScout.Api`
- Apply migration and verify `jobscout.db` is created with correct schema
- Add `jobscout.db` to `.gitignore`

### Task 3.3 — Repository implementations
Implement `IJobRepository` and `IProfileRepository` in `JobScout.Infrastructure/Repositories/`:
- `JobRepository`: GetById, GetByProfile (with filters/pagination), GetByExternalId, Add, Update, GetRecentBySource
- `ProfileRepository`: GetAll, GetById, Add, Update, Delete, SetActive

### Task 3.4 — Seed data (development only)
Create `DbSeeder.cs` that inserts:
- One `SearchProfile` named "Software Engineering" with a sample resume text
- 6–8 sample `Job` records across different sources
- Matching `AiScore` records (scores 6.0–9.5)
- A few `UserRating` records
- 14 days of `DailyMetric` records per source (for trend chart testing)

Call the seeder in `Program.cs` when environment is Development and DB is empty.

---

## Phase 4 — External Service Clients

### Task 4.1 — SerpAPI LinkedIn Jobs client
Create `SerpApiLinkedInClient` in `JobScout.Infrastructure/ExternalServices/`:
- HTTP GET to `https://serpapi.com/search.json?engine=linkedin_jobs&q={query}&location={location}&api_key={key}`
- Deserialize response into a typed result model
- Map to `Job` entities (set `Source = JobSource.LinkedIn`)
- Handle pagination (SerpAPI returns up to 10 per page; fetch up to 3 pages per run)
- Respect rate limits with `Task.Delay` between pages

### Task 4.2 — Adzuna API client
Create `AdzunaClient` in `JobScout.Infrastructure/ExternalServices/`:
- Free API: `https://api.adzuna.com/v1/api/jobs/us/search/{page}?app_id=&app_key=&results_per_page=20&what={query}`
- Map response to `Job` entities (set `Source = JobSource.Adzuna`)

### Task 4.3 — RemoteOK client
Create `RemoteOkClient` in `JobScout.Infrastructure/ExternalServices/`:
- Free JSON endpoint: `https://remoteok.com/api`
- Filter by relevant tags from profile (e.g., "dotnet", "csharp", "azure")
- Map to `Job` entities (set `Source = JobSource.RemoteOK`)
- Note: No API key required; include `User-Agent` header to avoid 403

### Task 4.4 — The Muse API client
Create `TheMuseClient` in `JobScout.Infrastructure/ExternalServices/`:
- Free API: `https://www.themuse.com/api/public/jobs?category=Software+Engineer&page=0`
- Map to `Job` entities (set `Source = JobSource.TheMuse`)

### Task 4.5 — Job ingestion orchestrator
Create `JobIngestionService` implementing `IJobIngestionService`:
- Accepts a `SearchProfile`
- Builds search query from resume keywords and profile preferences
- Calls all enabled source clients in parallel (`Task.WhenAll`)
- Deduplicates by `(ExternalId, Source)` before saving
- Returns count of new jobs found
- Log all ingestion activity with `ILogger`

---

## Phase 5 — AI Scoring Engine

### Task 5.1 — Claude API scoring service
Create `ClaudeAiScoringService` implementing `IAiScoringService` in `JobScout.Infrastructure/AI/`:

The scoring prompt should instruct Claude to:
- Act as a senior technical recruiter evaluating fit
- Be given: the candidate's resume text, their stated preferences, and the full job description
- Return a JSON object (no markdown fences) with:
  ```json
  {
    "score": 8.4,
    "reasoning": "...",
    "matchedKeywords": ["C#", ".NET", "Azure"],
    "redFlags": ["Requires 10+ years Go experience"]
  }
  ```
- Use model `claude-sonnet-4-20250514`
- Parse response safely; if JSON parse fails, retry once, then default to score 5.0 with error note

### Task 5.2 — Batch scoring
- After ingestion, score all newly-discovered jobs for the active profile
- Store results in `AiScore` table
- Skip re-scoring jobs that already have a score for this profile (unless recalibration is requested)
- Run scoring with concurrency limit of 3 simultaneous Claude API calls (`SemaphoreSlim`)

### Task 5.3 — Recalibration logic
Implement `RecalibrateAsync(Guid profileId, bool resetHistory)` in `ClaudeAiScoringService`:
- If `resetHistory = true`: delete all `AiScore` records for this profile
- Re-score all jobs for this profile using the current resume
- Optionally: include recent `UserRating` data in the prompt as calibration examples
  - "The user gave 5 stars to jobs X, Y, Z — here are their descriptions"
  - "The user gave 1–2 stars to jobs A, B — avoid these patterns"
- This feedback loop is what makes the AI smarter over time

---

## Phase 6 — Azure Function (Timer-Triggered Ingestion)

### Task 6.1 — Timer trigger function
Create `JobIngestionFunction` in `JobScout.Functions/`:
- Timer trigger: `0 0 */4 * * *` (every 4 hours)
- On trigger: fetch all active `SearchProfile` records from DB
- For each profile: run `IJobIngestionService.IngestAsync` then `IAiScoringService.BatchScoreAsync`
- Update `DailyMetric` records with today's counts
- Log summary: "Profile '{name}': {newJobs} new jobs found, {scored} scored."

### Task 6.2 — HTTP trigger for manual runs
Create `ManualIngestionFunction` with an HTTP trigger:
- POST `/api/ingest?profileId={guid}`
- Allows manually triggering ingestion from the UI (useful during development)
- Returns a JSON summary of results

### Task 6.3 — Function local settings
Create `local.settings.json` (gitignored) in `JobScout.Functions/`:
```json
{
  "IsEncrypted": false,
  "Values": {
    "AzureWebJobsStorage": "UseDevelopmentStorage=true",
    "FUNCTIONS_WORKER_RUNTIME": "dotnet-isolated",
    "ConnectionStrings__DefaultConnection": "Data Source=../JobScout.Api/jobscout.db",
    "SerpApi__ApiKey": "",
    "Anthropic__ApiKey": "",
    "Adzuna__AppId": "",
    "Adzuna__AppKey": ""
  }
}
```

---

## Phase 7 — REST API Layer

### Task 7.1 — Jobs endpoints
```
GET    /api/jobs?profileId=&source=&minScore=&locationType=&jobType=&q=&page=&pageSize=
GET    /api/jobs/{id}
GET    /api/jobs/{id}/score?profileId=
```

### Task 7.2 — Profiles endpoints
```
GET    /api/profiles
GET    /api/profiles/{id}
POST   /api/profiles
PUT    /api/profiles/{id}
DELETE /api/profiles/{id}
POST   /api/profiles/{id}/resume     (multipart: upload .docx or .txt; extract plain text)
POST   /api/profiles/{id}/recalibrate
```

### Task 7.3 — Ratings endpoints
```
POST   /api/ratings           { jobId, profileId, stars, notes? }
PUT    /api/ratings/{id}
GET    /api/jobs/{id}/rating?profileId=
```

### Task 7.4 — Metrics endpoints
```
GET    /api/metrics/dashboard?profileId=          → DashboardStatsDto
GET    /api/metrics/by-source?profileId=          → SourceBreakdownDto[]
GET    /api/metrics/posting-windows?profileId=    → PostingWindowDto[]
GET    /api/metrics/trends?profileId=&days=30     → trend time series
```

### Task 7.5 — API configuration
- Enable CORS for Blazor WASM client origin (localhost dev + production URL)
- Add Swagger/OpenAPI via Swashbuckle
- Add global exception handler middleware that returns `ProblemDetails`
- Configure Blazor WASM static file hosting from the API project (so it's one deployable unit)

---

## Phase 8 — Blazor WebAssembly UI

> **Reference the HTML file `jobscout-responsive-template.html` for all visual details.**
> The CSS variables, component structure, animations, and responsive breakpoints in that file
> are the definitive visual spec. Port them faithfully into Blazor component CSS isolation files.

### Task 8.1 — Global styles and layout shell
- Copy CSS custom properties (`:root` block) from the HTML template into `wwwroot/css/app.css`
- Copy the background radial gradients and grid overlay (`body::before`) into `app.css`
- Create `MainLayout.razor` with a three-column grid:
  - `<Sidebar />` (280px)
  - `<main>` (flex 1)
  - `<InsightsPanel />` (340px)
- Create `TopBar.razor` component matching the HTML `.topbar` element

### Task 8.2 — Sidebar component (`Sidebar.razor`)
Port the HTML `.sidebar` section:
- Views nav (All Jobs, Strong Fit, Saved, Applied, Trends) with live counts
- Profiles section: list profiles, "Add profile" button, active profile highlighted
- Job Boards section: board rows with toggle switches (bound to enabled/disabled state)
- Mobile drawer behavior (hamburger open/close) matching `.drawer-open` CSS class

### Task 8.3 — Job card component (`JobCard.razor`)
Port the HTML `.job-card` element — this is the most important component:
- Company logo (colored initials badge, matching the HTML `.company-logo` style)
- Job title, company name, location badge
- Tech tag pills (from `Job.Tags` JSON)
- Circular AI fit score gauge (SVG, identical to HTML `.score-ring` element)
- Star rating display + interactive user rating (1–5 stars, click to rate)
- Bookmark/save toggle button
- "Featured" ribbon on top-scoring cards (score ≥ 9.0)
- "Why it's a strong match" expandable section from `AiScore.Reasoning`
- Source badge + posted time
- `[Parameter]` props: `Job JobSummaryDto`, `AiScore AiScoreDto`, `UserRating? UserRatingDto`
- `[Parameter] EventCallback<int> OnRated` — fires when user submits a star rating

### Task 8.4 — Job feed / main content area (`JobFeed.razor`)
- Page header ("All Jobs" / "Strong Fit" / etc.) with job count
- Sync status pill with last-synced timestamp
- Search input (client-side filter on title/company, calls API for server-side on Enter)
- Filter chips: Remote, C# / .NET, Azure, Contract, Full-time, AI fit ≥ N ★ (match HTML `.chips`)
- "Filters" button → opens advanced filter panel
- List/grid view toggle
- Virtualized job card list (`<Virtualize>` component for large lists)
- "Load more" button for pagination

### Task 8.5 — Advanced filter panel (`FilterPanel.razor`)
Port the HTML `.filter-panel` slide-in overlay:
- Role type, Location type, Salary range, Minimum AI fit dropdowns
- Cancel / Apply buttons
- Slide-in/out CSS transition matching the HTML `filter-panel.open` class

### Task 8.6 — Insights panel (`InsightsPanel.razor`)
Port the HTML `.insights` aside:
- **This Week** — four stat cards (Jobs Found, Strong Fits, Saved, Applied) with trend arrows
- **Jobs by Source** — horizontal bar chart with source color coding (match HTML `.source-list`)
- **Best Posting Windows** — day-of-week bar chart + time window card (match HTML `.week-bars`)
- **Recent Activity** — scrollable activity log
- **Recalibrate** button → calls `POST /api/profiles/{id}/recalibrate`, shows confirmation dialog

### Task 8.7 — Profile management page (`Profiles.razor`)
- List all profiles as cards
- Create new profile form (name, description, LinkedIn URL)
- Resume upload section: drag-and-drop or file picker for `.docx` / `.txt` / `.pdf`
  - Show resume filename and last uploaded date once uploaded
- "Delete profile" with confirmation
- "Recalibrate from scratch" option per profile

### Task 8.8 — Trends page (`Trends.razor`)
Charts using a lightweight Blazor-compatible charting library (e.g., `Blazor-ApexCharts` or plain SVG):
- Jobs found per day line chart (last 30 days, one line per source)
- Strong Fits over time (trend upward = AI is learning your taste)
- Best performing sources (sorted bar chart)
- Heat map of posting times by day-of-week × hour-of-day

### Task 8.9 — HTTP client service layer (`JobScout.Web/Services/`)
Create typed `HttpClient` services that call the API:
- `JobsService` — `GetJobsAsync`, `GetJobDetailAsync`
- `ProfilesService` — `GetProfilesAsync`, `CreateProfileAsync`, `UploadResumeAsync`, `RecalibrateAsync`
- `RatingsService` — `SubmitRatingAsync`
- `MetricsService` — `GetDashboardStatsAsync`, `GetSourceBreakdownAsync`, `GetPostingWindowsAsync`, `GetTrendsAsync`
- Register all services in `Program.cs` with `builder.Services.AddHttpClient<T>()`
- Store active `ProfileId` in a `ProfileStateService` singleton for cross-component sharing

### Task 8.10 — Toast notification component
Port the HTML `.toast` element:
- `ToastService` singleton with `ShowAsync(string message)`
- `<ToastHost />` component placed in `MainLayout.razor`
- Auto-dismiss after 2 seconds (matching HTML JS behavior)

---

## Phase 9 — Resume Parsing

### Task 9.1 — .docx text extraction
In `JobScout.Infrastructure/Parsing/ResumeParser.cs`:
- Use `DocumentFormat.OpenXml` NuGet package to extract plain text from `.docx`
- Strip headers, footers, and formatting — return clean paragraph text
- For `.txt` files: read directly
- For `.pdf`: use `itext7` or `PdfPig` (add NuGet) to extract text
- Return a `ResumeParseResult { PlainText, WordCount, DetectedSkills[] }`

### Task 9.2 — Skill extraction
After parsing, run a simple keyword scan against a predefined skill list to populate `DetectedSkills`:
- Maintain a static `SkillsDictionary.cs` list of ~200 tech terms
  (C#, .NET, ASP.NET, Azure, Vue.js, Python, SQL Server, Entity Framework, etc.)
- Surface these in the profile view so the user can see what the AI is working with

---

## Phase 10 — Testing

### Task 10.1 — Unit tests project
- Add `tests/JobScout.Tests` xUnit project
- Reference `JobScout.Core` and `JobScout.Infrastructure`
- Test `ClaudeAiScoringService` with a mocked `HttpClient` (use `Moq`)
- Test `JobIngestionService` deduplication logic
- Test `ResumeParser` with a sample `.docx` and `.txt` fixture file
- Test `DailyMetric` aggregation logic

### Task 10.2 — Integration tests
- Add `tests/JobScout.Api.Tests` xUnit project
- Use `WebApplicationFactory<Program>` with an in-memory SQLite DB
- Test key API endpoints: GET /api/jobs, POST /api/ratings, GET /api/metrics/dashboard

---

## Phase 11 — Azure Deployment

### Task 11.1 — Infrastructure setup (Azure Portal / CLI)
- Create Resource Group: `jobscout-rg`
- Create Azure App Service (Free or B1 tier) for `JobScout.Api` + Blazor WASM static files
- Create Azure Function App (Consumption plan) for `JobScout.Functions`
- Create Azure SQL Database (Basic tier, ~$5/mo) — swap SQLite connection string for production
- Store all API keys in Azure App Service → Configuration → Application Settings (not in code)

### Task 11.2 — GitHub Actions CI/CD
Create `.github/workflows/deploy.yml`:
- Trigger: push to `main`
- Steps: checkout → dotnet build → dotnet test → dotnet publish → deploy to Azure App Service
- Use GitHub Secrets for `AZURE_PUBLISH_PROFILE`

### Task 11.3 — Production configuration
- Switch `ConnectionStrings:DefaultConnection` to Azure SQL in production app settings
- Enable EF Core migrations on startup (or use a migration job step in the pipeline)
- Set `ASPNETCORE_ENVIRONMENT=Production`

---

## Development Order Recommendation

```
Phase 1 (setup) → Phase 2 (models) → Phase 3 (data layer + seed data)
→ Phase 7 (API skeleton with seeded data)
→ Phase 8 (Blazor UI against mock/seeded API — get the UI working first)
→ Phase 5 (AI scoring with real Claude API)
→ Phase 4 (external clients, one source at a time — start with RemoteOK, it's free/no key)
→ Phase 6 (Azure Function)
→ Phase 9 (resume parsing)
→ Phase 10 (tests)
→ Phase 11 (deployment)
```

---

## Key Design Decisions to Keep in Mind

- **One API key per source** — never commit keys; always use user secrets locally, App Settings in Azure
- **Deduplication is critical** — the `(ExternalId, Source)` unique index prevents re-scoring jobs the AI has already seen
- **Score 1–10, not 1–5** — the AI uses a 10-point scale internally; the UI displays it as a decimal (9.4, 8.2, etc.) matching the mockup. The user's personal rating is 1–5 stars and is a separate field.
- **The recalibration prompt** includes user ratings as few-shot examples — this is the feedback loop that makes the tool genuinely improve over time
- **Blazor WASM is hosted by the API project** — one Azure App Service hosts both; configure `app.UseBlazorFrameworkFiles()` and `app.UseStaticFiles()` in `Program.cs`
- **RemoteOK is the easiest first source** to wire up (free, no key, returns JSON) — use it to test the full pipeline end-to-end before integrating SerpAPI
