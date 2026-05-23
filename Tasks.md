# Phase 7: Testing Strategy

**Priority: HIGH** | **Branch:** `phase7/testing`

> Reference: [Roadmap.md](Roadmap.md) — Phase 7 (sections 7.1 through 7.3)

---

## Status Overview

| # | Task | Status |
|---|------|--------|
| 7.1 | Test project scaffolding (4 projects, solution wiring, shared packages) | DONE |
| 7.2 | Test data builders + `tests/fixtures/` directory | DONE |
| 7.3 | `JobScout.Core.Tests` — domain/model unit tests | DONE |
| 7.4 | `JobScout.Infrastructure.Tests` — AI scoring service tests | DONE |
| 7.5 | `JobScout.Infrastructure.Tests` — Job ingestion pipeline tests | DONE |
| 7.6 | `JobScout.Infrastructure.Tests` — Resume parser tests | DONE |
| 7.7 | `JobScout.Infrastructure.Tests` — Profile repository tests (user-scoping, cascades) | DONE |
| 7.8 | `JobScout.Api.Tests` — Auth flow + protected-endpoint integration tests | DONE |
| 7.9 | `JobScout.Web.Tests` — Blazor component tests (bUnit) + final `dotnet test` verification | DONE |
| 7.10 | Commit & PR | DONE |

---

## Existing Infrastructure

The following already exist and inform how tests should be written:

- **Solution layout** — `JobScout.slnx` (new SLNX format). Test projects must be added under a new `<Folder Name="/tests/">` entry.
- **`JobScoutDbContext`** — `src/JobScout.Infrastructure/Data/JobScoutDbContext.cs`
  - Inherits from `IdentityDbContext<ApplicationUser>`; tests need to seed Identity tables or skip them where not exercised.
  - Currently configured for SQLite. Tests should use `Microsoft.EntityFrameworkCore.Sqlite` with `:memory:` (kept open via a single connection) to preserve column-type semantics — `InMemory` provider would skip the JSON converters that production code relies on.
- **`ClaudeAiScoringService`** — `src/JobScout.Infrastructure/AI/ClaudeAiScoringService.cs`
  - Post-Phase-5, this now constructs `new AnthropicClient(...)` internally. To make it testable we will inject an `IAnthropicClientFactory` (or pass the client in) — this is the only production-code refactor in Phase 7. The "no API key configured" branch already returns `DefaultScore` and can be tested today without touching the production API.
- **`IJobBoardClient` implementations** — `src/JobScout.Infrastructure/ExternalServices/`
  - All use `HttpClient` via DI, so each can be tested with a stub `HttpMessageHandler` returning canned JSON / RSS payloads.
- **`Program.cs`** — `src/JobScout.Api/Program.cs`
  - Uses top-level statements. `WebApplicationFactory<TEntryPoint>` requires a public `Program` class — add `public partial class Program {}` at the bottom of the file to expose it for tests.
- **`ResumeParser`** — `src/JobScout.Infrastructure/Parsing/ResumeParser.cs`
  - Dispatches by extension; tests for `.txt`, `.docx`, and `.pdf` need fixture files of each type.
- **`ICurrentUserService`** — `src/JobScout.Infrastructure/Identity/CurrentUserService.cs`
  - Throws when there is no authenticated user. Integration tests must either log in via `AuthController` first or replace this service with a stub registered via `WebApplicationFactory.WithWebHostBuilder(b => b.ConfigureServices(...))`.

---

## Task Details

### 7.1 Test Project Scaffolding

**Files to create:**
- `tests/JobScout.Core.Tests/JobScout.Core.Tests.csproj`
- `tests/JobScout.Core.Tests/Usings.cs`
- `tests/JobScout.Infrastructure.Tests/JobScout.Infrastructure.Tests.csproj`
- `tests/JobScout.Infrastructure.Tests/Usings.cs`
- `tests/JobScout.Api.Tests/JobScout.Api.Tests.csproj`
- `tests/JobScout.Api.Tests/Usings.cs`
- `tests/JobScout.Web.Tests/JobScout.Web.Tests.csproj`
- `tests/JobScout.Web.Tests/Usings.cs`

**Files to modify:**
- `JobScout.slnx` — add a `<Folder Name="/tests/">` containing all four test projects
- `src/JobScout.Api/Program.cs` — append `public partial class Program {}` to make the entry point accessible to `WebApplicationFactory`

**Requirements:**
- [ ] All four projects target `net10.0` and have `<IsPackable>false</IsPackable>`
- [ ] Common test packages on every test project: `Microsoft.NET.Test.Sdk`, `xunit.v3`, `xunit.runner.visualstudio`, `FluentAssertions`, `NSubstitute`
- [ ] `JobScout.Core.Tests` references `JobScout.Core`
- [ ] `JobScout.Infrastructure.Tests` references `JobScout.Core`, `JobScout.Infrastructure`, and adds `Microsoft.EntityFrameworkCore.Sqlite` (for in-memory)
- [ ] `JobScout.Api.Tests` references `JobScout.Api`, `JobScout.Infrastructure`, `JobScout.Core`, and `Microsoft.AspNetCore.Mvc.Testing`
- [ ] `JobScout.Web.Tests` references `JobScout.Web`, `JobScout.Core`, and adds `bunit` (latest 1.x)
- [ ] `dotnet test` from the solution root discovers all four projects with zero tests defined yet — confirms wiring is correct
- [ ] `dotnet build` still reports 0 warnings, 0 errors

**Acceptance criteria:** Each test project compiles. `dotnet test` exits 0. Solution loads cleanly in IDEs.

---

### 7.2 Test Data Builders & Fixture Directory

**Files to create:**
- `tests/fixtures/resumes/sample.txt`
- `tests/fixtures/resumes/sample.docx`
- `tests/fixtures/resumes/sample.pdf`
- `tests/fixtures/job-board-responses/remoteok.json`
- `tests/fixtures/job-board-responses/adzuna.json`
- `tests/fixtures/job-board-responses/themuse.json`
- `tests/fixtures/job-board-responses/serpapi-linkedin.json`
- `tests/fixtures/anthropic/strong-match-tool-use.json` — canned `MessageResponse` JSON with a `ToolUseContent` block
- `tests/JobScout.Infrastructure.Tests/Builders/JobBuilder.cs`
- `tests/JobScout.Infrastructure.Tests/Builders/ProfileBuilder.cs`
- `tests/JobScout.Infrastructure.Tests/Builders/AiScoreBuilder.cs`
- `tests/JobScout.Infrastructure.Tests/Builders/UserRatingBuilder.cs`
- `tests/JobScout.Infrastructure.Tests/Builders/JobApplicationBuilder.cs`
- `tests/JobScout.Infrastructure.Tests/Fixtures/SqliteFixture.cs` — opens a single `SqliteConnection` to `:memory:` and creates a `JobScoutDbContext` against it, calling `EnsureCreated` so all migrations apply. Implements `IDisposable`.
- `tests/JobScout.Infrastructure.Tests/Fixtures/TestDbContextFactory.cs` — produces a fresh `JobScoutDbContext` per test using the fixture connection

**Requirements:**
- [ ] Fixture files are committed binary content for `.docx` / `.pdf`; `.txt` and `.json` are plain text
- [ ] `JobScout.Infrastructure.Tests.csproj` copies `tests/fixtures/**/*` next to the test DLL with `CopyToOutputDirectory=PreserveNewest`
- [ ] Builders expose a fluent surface: `new JobBuilder().WithTitle("...").WithSource(JobSource.RemoteOK).Build()` returning a `Job` with sensible defaults for every field
- [ ] All builders default `Id = Guid.NewGuid()` and timestamps to `DateTime.UtcNow`
- [ ] `SqliteFixture` is reusable as both an `IClassFixture<>` and a per-test `using` — pick the per-test style as the default for isolation

**Acceptance criteria:** Two tests using the same `SqliteFixture` do not see each other's data. Builders compile and produce entities the DbContext can `Add` + `SaveChangesAsync` without error.

---

### 7.3 Core Unit Tests (`JobScout.Core.Tests`)

**Files to create:**
- `tests/JobScout.Core.Tests/EnumSerializationTests.cs`
- `tests/JobScout.Core.Tests/Models/JobTests.cs`
- `tests/JobScout.Core.Tests/DTOs/MappingShapeTests.cs` — verify DTO property counts haven't drifted from the model
- `tests/JobScout.Core.Tests/Notifications/NotificationTypeTests.cs`

**Requirements:**
- [ ] Each test class follows `Subject_When_Should` naming
- [ ] Pure unit tests — no DB, no HTTP. Pure C# only.
- [ ] Cover `ApplicationStatus`, `JobSource`, `NotificationType`, `FeedFormat`, `LocationType`, `JobType` enums for `ToString()` ↔ parse round-trips

**Acceptance criteria:** At least 15 tests in this project, all passing. Tests run in under 1s.

---

### 7.4 AI Scoring Service Tests

**Files to create:**
- `src/JobScout.Infrastructure/AI/IAnthropicClientFactory.cs` — wraps `new AnthropicClient(...)` so tests can substitute
- `src/JobScout.Infrastructure/AI/AnthropicClientFactory.cs` — production implementation
- `tests/JobScout.Infrastructure.Tests/AI/ClaudeAiScoringServiceTests.cs`

**Files to modify:**
- `src/JobScout.Infrastructure/AI/ClaudeAiScoringService.cs` — inject `IAnthropicClientFactory` (constructor change); call `factory.Create(apiKey)` instead of `new AnthropicClient(...)`
- `src/JobScout.Api/Program.cs` — register `AddSingleton<IAnthropicClientFactory, AnthropicClientFactory>()`
- `functions/JobScout.Functions/Program.cs` — same registration

**Requirements:**
- [ ] **No API key path:** `ScoreJobAsync` returns a `DefaultScore` (`Score == 5m`, `ModelVersion == "default"`) when `Anthropic:ApiKey` is empty, no factory call is made
- [ ] **Happy path:** factory returns a stubbed client whose `GetClaudeMessageAsync` resolves to a `MessageResponse` containing a `ToolUseContent` with valid scoring JSON → `AiScore` has overall score, all four sub-scores, matched keywords, growth areas, red flags, and populated token counts
- [ ] **Missing tool_use:** response has only `TextContent` (no `ToolUseContent`) → falls back to `DefaultScore`
- [ ] **Bad JSON in tool input:** input has wrong types or missing required fields → clamped to defaults without throwing
- [ ] **Anthropic call throws:** `GetClaudeMessageAsync` throws → caught, `DefaultScore` returned, error logged
- [ ] **Batch dedup:** `BatchScoreAsync` skips jobs already scored for that profile
- [ ] **Strong-fit notification:** when a score crosses 8.0, `INotificationService.OnHighScoreCreatedAsync` is invoked exactly once with the matching job
- [ ] **Few-shot prompt:** when 12 ratings exist for the profile, the system prompt embeds the **most recent 10** in descending order of `RatedAt`
- [ ] **Per-profile model selection:** `profile.PreferredModel` takes precedence over `Anthropic:Model` config, which takes precedence over the Haiku default
- [ ] Use `NSubstitute` for `IAnthropicClientFactory`, `INotificationService`, and `ILogger`; use `SqliteFixture` for the real DbContext

**Acceptance criteria:** All 9 scenarios above are individual `[Fact]` or `[Theory]` tests, all green.

---

### 7.5 Job Ingestion Pipeline Tests

**Files to create:**
- `tests/JobScout.Infrastructure.Tests/Services/JobIngestionServiceTests.cs`
- `tests/JobScout.Infrastructure.Tests/Services/DeduplicationServiceTests.cs`
- `tests/JobScout.Infrastructure.Tests/TestHelpers/StubJobBoardClient.cs` — implements `IJobBoardClient` with a configurable canned response and optional exception

**Requirements:**
- [ ] **Exact dedup:** ingesting the same `(ExternalId, Source)` twice does not double-insert; `Duplicates` counter increments
- [ ] **Fuzzy dedup:** ingesting an Indeed job and a LinkedIn job with normalized-equal title/company sets `IsPotentialDuplicate = true` and `DuplicateOfJobId` to the first ingestion's `Id`; `FuzzyDuplicates` counter increments
- [ ] **Source filtering:** when `profile.PreferredSources` contains only `JobSource.RemoteOK`, only the RemoteOK stub client is invoked
- [ ] **Partial failure:** one of three stubs throws → ingestion completes with results from the other two, error logged, `NewJobsFound` reflects only the successful results
- [ ] **Notification fired:** `INotificationService.OnIngestionCompleteAsync` is invoked iff `NewJobsFound > 0`
- [ ] **DeduplicationService.NormalizeTitle:** `"Sr. Software Developer (Remote)"` and `"senior software developer"` normalize equal; `"Software Developer"` vs `"Data Scientist"` do not
- [ ] **DeduplicationService.NormalizeCompany:** `"Acme Corp, Inc."` and `"ACME Corporation"` normalize equal

**Acceptance criteria:** All scenarios pass against `SqliteFixture`-backed DbContext. No real HTTP calls.

---

### 7.6 Resume Parser Tests

**Files to create:**
- `tests/JobScout.Infrastructure.Tests/Parsing/ResumeParserTests.cs`

**Requirements:**
- [ ] **`.txt` round-trip:** parsing `sample.txt` returns `PlainText` matching the file contents and a non-empty `DetectedSkills`
- [ ] **`.docx` extraction:** `sample.docx` contains the string "C#" → `PlainText` includes it
- [ ] **`.pdf` extraction:** `sample.pdf` parses without throwing, `WordCount > 0`
- [ ] **Skill detection:** at least 3 known skills from the resume appear in `DetectedSkills` (e.g. "Python", "React", "AWS")
- [ ] **Empty stream:** an empty `MemoryStream` returns `PlainText = ""` and `DetectedSkills.Count == 0`
- [ ] **Unsupported extension:** `.rtf` throws or returns empty — assert whichever the production code does

**Acceptance criteria:** Six scenarios pass. Tests complete in under 5 seconds even with PDF parsing.

---

### 7.7 Profile Repository Tests

**Files to create:**
- `tests/JobScout.Infrastructure.Tests/Repositories/ProfileRepositoryTests.cs`
- `tests/JobScout.Infrastructure.Tests/Repositories/ApplicationRepositoryTests.cs`

**Requirements:**
- [ ] **CRUD:** `AddAsync` → `GetByIdAsync` round-trips all Phase 5 fields (`PreferredModel`, `DesiredSalaryMin/Max`)
- [ ] **User scoping on GetAll:** seed 2 profiles under user A and 1 under user B → `GetAllAsync(userA.Id)` returns exactly 2
- [ ] **User scoping on GetById:** user A's `GetByIdAsync(profileBId, userA.Id)` returns null
- [ ] **Cascade delete:** deleting an `ApplicationUser` removes all their `SearchProfile`s and dependent `AiScore`s, `UserRating`s, `JobApplication`s, `CustomJobSource`s
- [ ] **`SearchKeywords` JSON column:** save a profile with a 5-element list, read it back, list count is 5 and values match
- [ ] **Application status pipeline:** seed 3 applied + 2 interviewing → `GetPipelineAsync` returns `Applied = 3, Interviewing = 2`
- [ ] Use `SqliteFixture` so JSON converters fire

**Acceptance criteria:** Each test runs against a fresh DB. No cross-test contamination.

---

### 7.8 API Auth & Controller Integration Tests

**Files to create:**
- `tests/JobScout.Api.Tests/Fixtures/JobScoutWebApplicationFactory.cs` — extends `WebApplicationFactory<Program>`, swaps `DbContext` to in-memory SQLite, exposes a helper for obtaining a JWT token
- `tests/JobScout.Api.Tests/Auth/AuthControllerTests.cs`
- `tests/JobScout.Api.Tests/Controllers/NotificationsControllerTests.cs`
- `tests/JobScout.Api.Tests/Controllers/ProfilesControllerTests.cs`
- `tests/JobScout.Api.Tests/Controllers/JobsControllerTests.cs`

**Requirements:**
- [ ] **Factory:** registers a fresh in-memory SQLite database per `WebApplicationFactory` instance; seeds one default `ApplicationUser` and exposes `GetTokenAsync(email, password)` for tests
- [ ] **Registration:** `POST /api/auth/register` with valid payload → 200 with token; weak password → 400 with validation errors
- [ ] **Login:** valid credentials → 200 with `accessToken`; wrong password → 401
- [ ] **Token validation:** any protected endpoint returns 401 when no `Authorization` header is sent
- [ ] **Expired token:** mint a token with `ValidTo = DateTime.UtcNow.AddMinutes(-5)` and confirm 401
- [ ] **Cross-tenant isolation:** user A creates a profile, user B's `GET /api/profiles/{id}` returns 404
- [ ] **Notifications cross-tenant:** user A triggers a notification, user B's `GET /api/notifications` returns an empty list and `PUT /api/notifications/{id}/read` for that id returns 404
- [ ] **Jobs list:** `GET /api/jobs?profileId=` returns jobs for that profile only

**Acceptance criteria:** ≥10 tests, all green. Factory cleans up SQLite connection on dispose.

---

### 7.9 Blazor Component Tests + Verification

**Files to create:**
- `tests/JobScout.Web.Tests/Components/NotificationDropdownTests.cs`
- `tests/JobScout.Web.Tests/Components/ToggleRowTests.cs`
- `tests/JobScout.Web.Tests/Components/JobCardTests.cs`
- `tests/JobScout.Web.Tests/TestHelpers/BUnitContextExtensions.cs` — registers the WASM-style services (auth, navigation, fake `HttpClient`) needed by components

**Requirements:**
- [ ] **NotificationDropdown:** with `unreadCount = 0`, no badge renders; with `unreadCount = 3`, a badge with text "3" appears; with `unreadCount = 12`, the badge reads "9+"
- [ ] **NotificationDropdown:** clicking the bell opens the panel and triggers `NotificationsService.GetAsync` once
- [ ] **NotificationDropdown:** clicking a notification calls `MarkReadAsync` and navigates to the related job
- [ ] **ToggleRow:** changing the checkbox raises `CheckedChanged` with the new value
- [ ] **JobCard:** renders score ring with the correct color band (green ≥ 8, amber 5–7, red < 5)
- [ ] Mock `NotificationsService` and `JobsService` via NSubstitute injected through `Services.AddSingleton`
- [ ] Final step: `dotnet test` from solution root → all four projects pass, total runtime under 30 seconds
- [ ] Final step: `dotnet build` reports 0 warnings, 0 errors

**Acceptance criteria:** ≥5 component tests pass. Full solution build + full test suite both green.

---

### 7.10 Commit & PR

- [ ] Stage all Phase 7 changes
- [ ] Commit with descriptive message summarizing test counts per project
- [ ] Push branch `phase7/testing`
- [ ] Create PR targeting `main` with a test plan listing each scenario covered
