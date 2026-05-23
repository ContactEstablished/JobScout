# JobScout Development Roadmap
**Version 1.0 | May 2026 | Contact Established**

---

## Executive Summary

JobScout is an AI-powered job search aggregator that pulls listings from multiple job boards, scores them against a user's resume using Claude AI, and presents a curated, ranked feed through a Blazor WebAssembly frontend. The application has reached a functional MVP state: four job board integrations are live, AI scoring works end-to-end, resume parsing supports PDF/DOCX/TXT, and a polished dark-mode UI renders jobs, metrics, and trends.

However, several critical capabilities must be built before JobScout is ready for daily personal use or broader release. This roadmap organizes the remaining development effort into nine phases, ordered by dependency and business value. Each phase includes detailed task breakdowns, estimated complexity, and architectural guidance.

---

## Current State Assessment

### What Works Today

- Four job board clients operational (RemoteOK, Adzuna, The Muse, LinkedIn via SerpAPI)
- Multi-profile data model: SearchProfile entity supports multiple resumes and search contexts
- AI scoring via Claude Haiku with batch processing and concurrency control
- Resume parsing for .txt, .docx, and .pdf with automatic skill extraction (~200 terms)
- Full REST API with 12+ endpoints across Jobs, Profiles, Ratings, and Metrics controllers
- Blazor WebAssembly frontend with Home (job feed), Profiles, and Trends pages
- Azure Functions for scheduled ingestion (every 4 hours) and manual triggers
- SQLite database with EF Core 10 migrations, seeder, and proper indexing
- Dark-mode UI with filter panel, star ratings, AI score rings, and bookmark toggles

### Critical Gaps

- **No authentication or user accounts:** Every API endpoint is public. There is no concept of a "user" in the data model. Profiles float freely without ownership.
- **Application tracking is skeletal:** The JobApplication entity and ApplicationStatus enum exist in the data model, but there is no controller, no service, and no UI for tracking applications.
- **LinkedIn integration is passive:** The SearchProfile has a LinkedInUrl field, but it's just stored text — there's no OAuth flow, no profile import, and no enrichment from LinkedIn data.
- **Notification system is a stub:** The TopBar renders a bell icon, but no notification infrastructure exists.
- **Zero test coverage:** No unit, integration, or end-to-end tests exist anywhere in the solution.
- **No deployment pipeline:** The app runs locally only. No CI/CD, no Azure infrastructure-as-code, no environment configuration.

---

## Phase 1: Authentication & User Accounts

**Priority: CRITICAL** | Estimated effort: **2–3 weeks**

Without authentication, JobScout cannot distinguish between users, protect data, or support the multi-profile-per-user workflow (e.g., a "Software Developer" profile and a "Chef" profile belonging to the same account). This phase establishes identity as the foundation for everything that follows.

### 1.1 ASP.NET Core Identity Setup

Install `Microsoft.AspNetCore.Identity.EntityFrameworkCore` and configure the identity system. This involves creating a custom `ApplicationUser` class that extends `IdentityUser`, registering identity services in `Program.cs`, and generating the EF Core migration that creates the `AspNetUsers`, `AspNetRoles`, `AspNetUserClaims`, and related tables in the database.

- **ApplicationUser entity:** Extend `IdentityUser` with `DisplayName` (string), `AvatarUrl` (string, nullable), `CreatedAt` (DateTimeOffset), and a navigation property `ICollection<SearchProfile> Profiles`.
- **DbContext update:** Change `JobScoutDbContext` to inherit from `IdentityDbContext<ApplicationUser>` instead of plain `DbContext`. Add the Identity model configuration in `OnModelCreating`.
- **EF Core migration:** Generate and apply `AddIdentity` migration. This creates ~7 tables (AspNetUsers, AspNetRoles, AspNetUserClaims, AspNetUserRoles, AspNetRoleClaims, AspNetUserLogins, AspNetUserTokens).
- **Password policy:** Configure minimum length (8+), require uppercase, lowercase, digit. Disable lockout for v1 (single-user context).

### 1.2 JWT Authentication for the API

The Blazor WebAssembly frontend runs entirely in the browser and communicates with the API over HTTP. JWT bearer tokens are the standard approach for this architecture.

- **Token generation:** Create an `AuthController` with `POST /api/auth/register` and `POST /api/auth/login` endpoints. On successful authentication, generate a JWT with claims for `UserId`, `Email`, and `DisplayName`. Sign with a symmetric key stored in configuration (user secrets for dev, Azure Key Vault for prod).
- **Token configuration:** Set token lifetime to 7 days for convenience (single-user tool). Include refresh token logic if shorter-lived access tokens are needed later.
- **API protection:** Add `[Authorize]` attribute to all controllers. Add `builder.Services.AddAuthentication().AddJwtBearer()` in `Program.cs`.
- **User context injection:** Create a `CurrentUserService` that reads the `UserId` claim from `HttpContext.User`. Inject it into repositories so all queries are automatically scoped to the authenticated user.

### 1.3 User-to-Profile Relationship

Currently, `SearchProfile` has no foreign key to a user. All profiles are global. This task adds the ownership link.

- **Schema change:** Add `UserId` (string, required) foreign key to `SearchProfile`, pointing to `ApplicationUser.Id`. Generate migration `AddUserIdToSearchProfile`.
- **Repository scoping:** Update `ProfileRepository` so `GetAll` and `GetById` filter by the current user's ID.
- **Cascade to jobs:** Jobs are currently linked to profiles via a join. Since profiles are now user-scoped, jobs are transitively user-scoped. Query filters must flow through the profile relationship.
- **Seed data update:** Update `DbSeeder` to create a default dev user and assign the seeded profile to that user.

### 1.4 Blazor Authentication State

- **AuthenticationStateProvider:** Implement a custom `JwtAuthenticationStateProvider` that reads the token from localStorage, parses its claims, and exposes the user's identity to Blazor's `<AuthorizeView>` and `[Authorize]` route attributes.
- **HTTP interceptor:** Register a `DelegatingHandler` that attaches the `Authorization: Bearer {token}` header to every outgoing `HttpClient` request.
- **Login/Register pages:** Create `Login.razor` and `Register.razor` pages with email/password forms. On success, store the JWT in localStorage and navigate to the home feed.
- **TopBar integration:** Replace the hardcoded "MW" initials pill in `TopBar.razor` with the authenticated user's `DisplayName` initials. Add a Sign Out option to the dropdown.

### 1.5 Phase 1 Deliverables

| Work Item | Priority | Estimate |
|---|---|---|
| ApplicationUser entity + Identity DbContext | Critical | 2 days |
| EF Core Identity migration | Critical | 0.5 days |
| AuthController (register + login + JWT) | Critical | 2 days |
| [Authorize] on all controllers + CurrentUserService | Critical | 1 day |
| UserId FK on SearchProfile + migration | Critical | 1 day |
| Repository scoping by UserId | Critical | 1 day |
| JwtAuthenticationStateProvider (Blazor) | Critical | 1.5 days |
| Login.razor + Register.razor pages | Critical | 2 days |
| HTTP DelegatingHandler for JWT | Critical | 0.5 days |
| TopBar auth integration + sign out | Critical | 0.5 days |
| DbSeeder update for dev user | Low | 0.5 days |

---

## Phase 2: Profile Management Enhancements

**Priority: HIGH** | Estimated effort: **1.5–2 weeks**

The multi-profile system is the core UX differentiator. The data model already supports multiple profiles per user, but the experience needs polish.

### 2.1 Profile Creation Workflow

A guided, multi-step workflow for setting up new profiles:

- **Step 1 – Identity:** Name the profile. Add an optional description and select an icon or color.
- **Step 2 – Resume:** Upload a resume (.pdf, .docx, or .txt) or paste plain text. Show a live preview of extracted text and detected skills. Allow manual skill editing.
- **Step 3 – Search Preferences:** Define default search keywords, preferred job types, preferred location types, and geographic preferences.
- **Step 4 – Job Boards:** Choose which sources to query for this profile.

### 2.2 Profile-Specific Search Keywords

- **Schema change:** Add `SearchKeywords` (List\<string\>, stored as JSON) and `PreferredSources` (List\<JobSource\>, stored as JSON) to `SearchProfile`. Generate migration.
- **Client updates:** Modify each `IJobBoardClient.FetchJobsAsync` implementation to use the profile's `SearchKeywords` as query terms instead of hardcoded strings.
- **Ingestion filtering:** Update `JobIngestionService` to only invoke job board clients that appear in the profile's `PreferredSources` list.

### 2.3 LinkedIn Profile Connection

- **Option A – Manual import (Recommended):** Allow the user to export their LinkedIn profile as a PDF and upload it through the resume parser. Achievable in a day, covers 90% of the value.
- **Option B – OAuth integration:** Register a LinkedIn app, implement the OAuth 2.0 authorization code flow, and call the LinkedIn Profile API. More seamless but requires developer approval and ongoing API maintenance.

### 2.4 Profile Cloning & Templates

- **Clone endpoint:** Add `POST /api/profiles/{id}/clone`. Deep-copy the profile's name (with "(Copy)" suffix), description, resume text, search keywords, and preferred sources. Do not copy scores, ratings, or metrics.
- **UI integration:** Add a "Duplicate" button to each profile card. Pre-fill the creation form with cloned data, allowing modification before saving.

### 2.5 Phase 2 Deliverables

| Work Item | Priority | Estimate |
|---|---|---|
| Multi-step profile creation wizard | High | 3 days |
| SearchKeywords + PreferredSources schema + migration | High | 1 day |
| Job board clients use profile keywords | High | 2 days |
| Ingestion service respects PreferredSources | High | 0.5 days |
| LinkedIn PDF import workflow | Medium | 1 day |
| Profile clone endpoint + UI | Medium | 1 day |
| Profile icon/color picker | Low | 0.5 days |
| Manual skill editing on resume upload | Medium | 1 day |

---

## Phase 3: Application Tracking Pipeline

**Priority: HIGH** | Estimated effort: **1.5–2 weeks**

The `JobApplication` entity and `ApplicationStatus` enum already exist in the data model, but there is no controller, no service layer, and no UI. This phase builds the full application tracking pipeline.

### 3.1 Application Service & Repository

- **IApplicationRepository:** `GetByProfile(Guid profileId)`, `GetByJob(Guid jobId, Guid profileId)`, `Add`, `Update`, `Delete`. Include filtering by `ApplicationStatus` and date range.
- **IApplicationTrackingService:** `ApplyAsync`, `UpdateStatusAsync`, `GetPipelineAsync` returning counts per status stage.
- **Status transitions:** Enforce valid transitions: `Applied → Interviewing → Offered → Accepted/Rejected`. Allow `Withdrawn` from any state. Log each transition with a timestamp.

### 3.2 Applications Controller

- `POST /api/applications` — Create a new application. Returns created application with status `Applied`.
- `PUT /api/applications/{id}/status` — Update status with new `ApplicationStatus` and optional notes.
- `GET /api/applications?profileId=&status=` — List applications with optional status filter.
- `GET /api/applications/pipeline?profileId=` — Return aggregated counts: `{ applied, interviewing, offered, rejected, withdrawn }`.
- `DELETE /api/applications/{id}` — Soft-delete or hard-delete an application record.

### 3.3 Application Tracking UI

Add a new `Applications.razor` page at `/applications` with a Kanban-style board:

- **Kanban columns:** Applied, Interviewing, Offered, Accepted, Rejected — each showing application cards with job title, company, date applied, and days-in-stage counter.
- **Quick-apply from job feed:** Add an "Apply" button to `JobCard.razor`. Clicking opens a modal to confirm and add notes, then creates the application and opens the job's `SourceUrl` in a new tab.
- **Status updates:** Drag-and-drop between columns (stretch goal), or a status dropdown on each card.
- **Timeline view:** Each application card expands to show a timeline of status transitions with timestamps and notes.
- **Sidebar integration:** Add "Applications" to the left navigation with a badge showing active application count.

### 3.4 Metrics Integration

- **Auto-increment:** When a new `JobApplication` is created, increment the `Applied` count on today's `DailyMetric` for that profile and source.
- **Dashboard update:** Add application pipeline summary (applied/interviewing/offered) to the `MetricsController`'s dashboard endpoint.
- **Trends integration:** Show application rates over time in the Trends page.

### 3.5 Phase 3 Deliverables

| Work Item | Priority | Estimate |
|---|---|---|
| IApplicationRepository + EF implementation | High | 1.5 days |
| IApplicationTrackingService + status transitions | High | 1.5 days |
| ApplicationsController (5 endpoints) | High | 1.5 days |
| Applications.razor Kanban board page | High | 3 days |
| Quick-apply button on JobCard | High | 1 day |
| Status timeline expansion on cards | Medium | 1 day |
| DailyMetric auto-increment on apply | Medium | 0.5 days |
| Sidebar nav + badge count | Low | 0.5 days |

---

## Phase 4: Job Board Expansion

**Priority: MEDIUM** | Estimated effort: **2–3 weeks**

JobScout currently integrates with four sources. The `JobSource` enum already defines eight sources. This phase fills the gaps and adds new ones.

### 4.1 Indeed Integration

- **SerpAPI route:** SerpAPI supports an Indeed engine (similar to the existing LinkedIn integration). Fastest path, cost metered per-search.
- **Implementation:** Create `IndeedClient : IJobBoardClient`. Map Indeed-specific fields to the `Job` entity.
- **Deduplication:** Strengthen deduplication by comparing normalized titles + companies, not just `ExternalId + Source`.

### 4.2 Glassdoor Integration

Glassdoor's job listings API requires a partner account. If approved, implement `GlassdoorClient`. Otherwise, consider SerpAPI's Google Jobs engine as a fallback.

### 4.3 Dice Integration

Dice is a major tech-focused job board with an RSS feed and search API. Implement `DiceClient` using their public search endpoint.

### 4.4 Wellfound (AngelList) Integration

Wellfound focuses on startup jobs and has a GraphQL API that requires authentication. Implement `WellfoundClient` targeting early-stage and venture-backed company listings.

### 4.5 Custom Source Support

- **CustomSourceClient:** Implement a generic `IJobBoardClient` that accepts a URL pattern, expected response format (JSON or RSS/Atom), and field mappings. Store configuration in a new `CustomJobSource` entity.
- **RSS support:** Parse standard RSS/Atom feeds where each `<item>` becomes a `Job`.

### 4.6 Cross-Source Deduplication Improvements

- **Fuzzy matching:** Implement a secondary deduplication pass comparing normalized `(Title + Company + Location)` tuples. Flag potential duplicates for user review rather than auto-merging.
- **Canonical job linking:** When a duplicate is confirmed, link the secondary listing to the primary job and track all source URLs.

### 4.7 Phase 4 Deliverables

| Work Item | Priority | Estimate |
|---|---|---|
| IndeedClient via SerpAPI | High | 2 days |
| DiceClient | Medium | 2 days |
| WellfoundClient (GraphQL) | Medium | 3 days |
| GlassdoorClient or Google Jobs fallback | Low | 2 days |
| Custom RSS/API source support | Low | 3 days |
| Cross-source fuzzy deduplication | Medium | 2 days |
| Canonical job linking | Low | 1 day |

---

## Phase 5: AI Scoring & Intelligence Enhancements

**Priority: MEDIUM** | Estimated effort: **1.5–2 weeks**

The Claude AI scoring pipeline works end-to-end, but several improvements would dramatically increase match quality and user trust.

### 5.1 Scoring Model Selection

The service currently hardcodes Claude Haiku for cost efficiency. Allow configuration and per-profile model selection.

- **Model options:** `claude-haiku-4-5` (fast, cheap, good for bulk screening), `claude-sonnet-4` (better reasoning, ideal for final scoring), `claude-opus-4` (highest quality, for premium profiles).
- **Configuration:** Add `PreferredModel` (string, nullable) to `SearchProfile`. Default to Haiku.
- **Cost tracking:** Log input/output token counts per scoring call. Show estimated API cost per profile on the Trends page.

### 5.2 Enhanced Scoring Prompt

- **Structured output:** Use Claude's `tool_use` feature to force structured JSON output instead of relying on regex parsing of freeform text. This eliminates parsing failures entirely.
- **Multi-dimensional scoring:** Break the single 1–10 score into sub-scores: Skills Match (0–10), Experience Level Fit (0–10), Culture/Values Alignment (0–10), Compensation Fit (0–10).
- **Salary analysis:** Compare against the user's stated range (new fields: `DesiredSalaryMin`, `DesiredSalaryMax` on `SearchProfile`). Factor into scoring.
- **Resume gap analysis:** Identify skills mentioned in the job description that are absent from the resume. Present as "growth areas" rather than pure negatives.

### 5.3 User Feedback Loop Refinement

- **Rating-informed prompts:** When scoring new jobs, include the user's last 10 ratings as few-shot examples in the prompt.
- **Automatic recalibration trigger:** After every 20 new ratings, automatically trigger a soft recalibration for unscored jobs. Notify the user that scores are being refreshed.
- **Score explanation improvements:** Add a "Why this score?" expandable section to `JobCard.razor` rendering the reasoning, matched keywords, and red flags.

### 5.4 Phase 5 Deliverables

| Work Item | Priority | Estimate |
|---|---|---|
| Per-profile model selection + config | Medium | 1 day |
| Migrate to Claude tool_use for structured output | High | 2 days |
| Multi-dimensional sub-scores | Medium | 2 days |
| Salary range analysis | Medium | 1 day |
| Rating-informed few-shot prompts | High | 2 days |
| Auto-recalibration trigger | Medium | 1 day |
| Cost tracking + display | Low | 1 day |

---

## Phase 6: Notifications & Alerts

**Priority: MEDIUM** | Estimated effort: **1–1.5 weeks**

The TopBar has a bell icon placeholder. This phase builds the notification system behind it.

### 6.1 In-App Notification System

- **Notification entity:** `Id`, `UserId`, `Type` (enum: `NewStrongFit`, `ScoreUpdate`, `IngestionComplete`, `ApplicationStatusChange`), `Title`, `Message`, `IsRead`, `CreatedAt`, `RelatedEntityId` (nullable Guid).
- **NotificationService:** Centralized service called by ingestion, scoring, and application tracking.
- **API endpoints:** `GET /api/notifications?unreadOnly=true`, `PUT /api/notifications/{id}/read`, `PUT /api/notifications/read-all`.
- **UI integration:** Populate the bell icon badge with unread count. Clicking opens a dropdown panel with recent notifications. Clicking a notification deep-links to the relevant job or application.

### 6.2 Email Notifications

- **Email service:** Integrate with SendGrid or Azure Communication Services.
- **Configurable alerts:** Daily digest of new strong fits (score ≥ 8), weekly summary, or instant alerts for score ≥ 9 matches.
- **Email templates:** HTML templates matching the app's dark theme, showing job cards with scores and one-click links.

### 6.3 Notification Preferences

- **User settings:** Add a `Settings.razor` page at `/settings` with notification preferences.
- **Quiet hours:** Allow users to set quiet hours during which no emails are sent.

### 6.4 Phase 6 Deliverables

| Work Item | Priority | Estimate |
|---|---|---|
| Notification entity + migration | Medium | 0.5 days |
| NotificationService + event hooks | Medium | 2 days |
| Notifications API endpoints | Medium | 1 day |
| Bell icon dropdown UI | Medium | 1.5 days |
| Email service integration (SendGrid) | Low | 2 days |
| Email templates | Low | 1 day |
| Settings.razor preferences page | Medium | 1.5 days |

---

## Phase 7: Testing Strategy

**Priority: HIGH** | Estimated effort: **2–3 weeks (ongoing)**

There are currently zero tests in the solution. Testing should begin in parallel with Phase 2 and continue throughout all subsequent phases.

### 7.1 Test Project Structure

- **JobScout.Core.Tests:** Unit tests for models, value objects, and domain logic. Use xUnit + FluentAssertions.
- **JobScout.Infrastructure.Tests:** Unit tests for services (with mocked dependencies), repository tests against SQLite in-memory, resume parser tests with sample files.
- **JobScout.Api.Tests:** Integration tests using `WebApplicationFactory<Program>`. Test each controller's endpoints end-to-end against an in-memory database.
- **JobScout.Web.Tests:** Blazor component tests using bUnit. Test component rendering, user interactions, and service integration.

### 7.2 Critical Test Coverage

- **AI scoring service:** Test prompt construction, response parsing (valid JSON, malformed JSON, empty response), error handling, and batch concurrency behavior. Mock the `HttpClient` to avoid live API calls.
- **Job ingestion pipeline:** Test deduplication logic, error handling when a source is down, partial ingestion (3 of 4 sources succeed), and correct attribution of jobs to profiles.
- **Resume parser:** Test with sample .txt, .docx, and .pdf files. Test skill extraction accuracy and edge cases (empty files, password-protected PDFs, huge files).
- **Profile repository:** Test CRUD operations, user-scoping (user A cannot see user B's profiles), and cascade delete behavior.
- **Authentication flow:** Test registration, login, token validation, expired tokens, and unauthorized access to protected endpoints.

### 7.3 Test Data & Fixtures

- **Sample files:** Create a `tests/fixtures/` directory with sample resumes, sample API responses from each job board, and sample Claude API responses.
- **Test database:** Use SQLite in-memory for repository and integration tests. Reset per test to ensure isolation.
- **Builder pattern:** Create test data builders (`JobBuilder`, `ProfileBuilder`, `ScoreBuilder`) for fluent test setup.

### 7.4 Phase 7 Deliverables

| Work Item | Priority | Estimate |
|---|---|---|
| Test project scaffolding (4 projects) | High | 1 day |
| AI scoring service unit tests | High | 2 days |
| Job ingestion pipeline tests | High | 2 days |
| Resume parser tests + fixtures | High | 1.5 days |
| Profile repository integration tests | High | 1 day |
| API controller integration tests | High | 2 days |
| Blazor component tests (bUnit) | Medium | 2 days |
| Auth flow integration tests | High | 1.5 days |
| Test data builders + fixtures | Medium | 1 day |

---

## Phase 8: Production Deployment & Infrastructure

**Priority: HIGH** | Estimated effort: **2–3 weeks**

The application currently runs locally with SQLite. This phase provisions Azure infrastructure, sets up CI/CD, and makes the app production-ready.

### 8.1 Azure Infrastructure

- **Azure App Service:** Host the ASP.NET Core API on an App Service (B1 tier for personal use). Configure the .NET 10 runtime stack.
- **Azure Static Web Apps:** Host the Blazor WebAssembly frontend as a static site. Configure API proxy rules to route `/api/*` to the App Service backend. Free tier is sufficient for personal use.
- **Azure SQL Database:** Migrate from SQLite to Azure SQL (Basic or S0 tier). Update the connection string and `DbContext` configuration.
- **Azure Functions:** Deploy the Functions project to an Azure Function App on the Consumption plan.
- **Azure Key Vault:** Store all secrets (Anthropic API key, SerpAPI key, Adzuna credentials, JWT signing key, database connection string). Configure managed identity on the App Service and Function App.

### 8.2 Infrastructure as Code

- **Bicep templates:** Define all Azure resources in Bicep files under an `infra/` directory. Include App Service, SQL Database, Function App, Static Web App, Key Vault, Application Insights, and all role assignments.
- **Environment parameters:** Create parameter files for dev, staging, and prod environments.
- **One-command provisioning:** Write a `deploy.ps1` script that runs `az deployment group create` with the Bicep template, then deploys the application code.

### 8.3 CI/CD Pipeline

- **GitHub Actions:** Create `.github/workflows/ci.yml` for continuous integration (build + test on every push/PR). Create `.github/workflows/deploy.yml` for deployment to Azure on push to main.
- **Build steps:** `dotnet restore → dotnet build → dotnet test → dotnet publish`. Separate jobs for API, Web, and Functions.
- **Deployment steps:** API → `az webapp deploy`. Web → Static Web Apps. Functions → `func azure functionapp publish`.
- **Environment secrets:** Store Azure credentials, API keys, and connection strings as GitHub Actions secrets.

### 8.4 Observability & Monitoring

- **Application Insights:** Extend from the Functions project to the API project. Track request rates, response times, failure rates, and dependency calls.
- **Structured logging:** Add Serilog with an Application Insights sink. Include correlation IDs for tracing requests across API → Functions → external APIs.
- **Health checks:** Add `/health` (basic) and `/health/ready` (includes DB and external API connectivity) endpoints. Wire into Azure App Service health probes.
- **Alerting:** Configure Azure Monitor alerts for API error rate > 5%, ingestion function failures, AI scoring API errors, and database connection failures.

### 8.5 Database Migration Strategy

- **Provider swap:** Replace `Microsoft.EntityFrameworkCore.Sqlite` with `Microsoft.EntityFrameworkCore.SqlServer`. Update `DbContext` configuration.
- **Schema compatibility:** Review type mappings: `TEXT → nvarchar(max)`, `REAL → decimal`, `INTEGER → int/bigint`. JSON columns may need different handling.
- **Data migration:** Write a one-time script to export SQLite data and import into Azure SQL.
- **Dual provider support:** Keep SQLite for local development and SQL Server for production using conditional provider registration in `Program.cs` based on environment.

### 8.6 Phase 8 Deliverables

| Work Item | Priority | Estimate |
|---|---|---|
| Bicep templates for all Azure resources | High | 3 days |
| Azure SQL Database setup + EF migration | High | 2 days |
| App Service deployment configuration | High | 1 day |
| Static Web Apps for Blazor WASM | High | 1 day |
| Function App deployment | High | 1 day |
| Key Vault + managed identity setup | High | 1 day |
| GitHub Actions CI pipeline | High | 1.5 days |
| GitHub Actions CD pipeline | High | 2 days |
| Application Insights + Serilog | Medium | 1.5 days |
| Health check endpoints | Medium | 0.5 days |
| Azure Monitor alerts | Low | 1 day |
| Data migration script (SQLite → SQL) | High | 1 day |

---

## Phase 9: UX Polish & Accessibility

**Priority: MEDIUM** | Estimated effort: **1.5–2 weeks**

The UI is functional and visually polished, but several areas need refinement for daily-driver quality.

### 9.1 Mobile Responsiveness

- **Responsive audit:** Test every page at mobile (375px), tablet (768px), and desktop (1200px+) widths.
- **Sidebar drawer:** On mobile, convert the left sidebar to a hamburger-triggered drawer.
- **Filter panel:** Ensure the slide-in filter panel works on touch devices without horizontal scroll.
- **Job cards:** Stack metadata horizontally on desktop, vertically on mobile. Ensure tap targets are at least 44×44px.

### 9.2 Accessibility (WCAG 2.1 AA)

- **Color contrast:** Verify all text meets 4.5:1 contrast ratio against the dark background.
- **Keyboard navigation:** Ensure all interactive elements are focusable and operable via keyboard. Add visible focus indicators.
- **Screen reader support:** Add ARIA labels to the score ring, star rating, filter panel toggle, and profile selector dropdown.
- **Motion preferences:** Respect `prefers-reduced-motion` for animations and transitions.

### 9.3 Data Export

- **CSV export:** Allow exporting the current job feed (with filters applied) as a CSV file for spreadsheet analysis.
- **PDF report:** Generate a summary report per profile: top-scored jobs, application pipeline status, trends charts, and AI scoring insights. Use QuestPDF (MIT-licensed).

### 9.4 Search & Filtering Improvements

- **Full-text search:** Extend search to cover descriptions, company names, tags, and AI reasoning text — not just job titles.
- **Saved filters:** Allow users to save named filter combinations (e.g., "Remote Senior Roles 8+") and quickly switch between them.
- **Sort options:** Add sort-by dropdown: AI Score (default), Posted Date, Company Name, Salary.

### 9.5 Light Theme

- **CSS custom properties:** The app already uses CSS variables for colors. Create a second set of variable values for light mode.
- **Theme toggle:** Add a sun/moon toggle to the TopBar. Persist preference in localStorage.
- **System preference:** Default to the user's OS-level `prefers-color-scheme` setting.

### 9.6 Phase 9 Deliverables

| Work Item | Priority | Estimate |
|---|---|---|
| Mobile responsive audit + fixes | Medium | 2 days |
| Sidebar drawer for mobile | Medium | 1 day |
| WCAG 2.1 AA accessibility pass | Medium | 2 days |
| CSV export for job feed | Low | 1 day |
| PDF profile report | Low | 2 days |
| Full-text search expansion | Medium | 1 day |
| Saved filter presets | Low | 1 day |
| Sort-by dropdown | Medium | 0.5 days |
| Light theme + toggle | Low | 1.5 days |

---

## Timeline Summary

Phases 1–3 are sequential. Phases 4–6 can run in parallel after Phase 3. Phase 7 (Testing) should begin alongside Phase 2 and continue through all subsequent phases. Phase 8 (Deployment) can begin after Phase 3. Phase 9 (UX Polish) can begin at any time.

| Phase | Priority | Estimate | Dependencies |
|---|---|---|---|
| **1. Authentication & User Accounts** | **CRITICAL** | 2–3 weeks | None |
| **2. Profile Management Enhancements** | **HIGH** | 1.5–2 weeks | Phase 1 |
| **3. Application Tracking Pipeline** | **HIGH** | 1.5–2 weeks | Phase 1 |
| **4. Job Board Expansion** | **MEDIUM** | 2–3 weeks | Phase 2 |
| **5. AI Scoring Enhancements** | **MEDIUM** | 1.5–2 weeks | Phase 2 |
| **6. Notifications & Alerts** | **MEDIUM** | 1–1.5 weeks | Phase 1 |
| **7. Testing Strategy** | **HIGH** | 2–3 weeks | Start with Phase 2 |
| **8. Production Deployment** | **HIGH** | 2–3 weeks | Phase 3 |
| **9. UX Polish & Accessibility** | **MEDIUM** | 1.5–2 weeks | Any time |

**Total estimated effort: 16–22 weeks** (with parallelization, 12–16 weeks for a single focused developer).

---

## Architectural Notes & Recommendations

### Recommended Development Order

1. **Phase 1 (Auth)** — Must be first. Everything else depends on user identity.
2. **Phase 7 (Testing)** — Begin immediately after Phase 1. Write tests for each feature as it's built.
3. **Phase 2 (Profiles) + Phase 3 (Applications)** — Can run in parallel after Phase 1.
4. **Phase 8 (Deployment)** — Start infrastructure provisioning during Phase 3. Deploy early; iterate in production.
5. **Phase 5 (AI Scoring)** — High impact, moderate effort. Do this before expanding job boards.
6. **Phase 4 (Job Boards)** — Each new source is independent; add them one at a time based on relevance.
7. **Phase 6 (Notifications)** — Nice-to-have after the core loop is solid.
8. **Phase 9 (UX Polish)** — Continuous. Pick items opportunistically as you work on other phases.

### Technical Debt to Address

- **Hard-coded search terms:** Job board clients use hardcoded strings like "software developer" instead of profile keywords. Phase 2 fixes this.
- **No retry/resilience:** HTTP calls to job boards and the Claude API have no retry logic, circuit breakers, or timeout configuration. Add Polly resilience policies via `Microsoft.Extensions.Http.Resilience`.
- **JSON serialization in entities:** `Tags` and `MatchedKeywords` are stored as JSON strings with manual serialization. EF Core 10 supports native JSON column mapping — migrate to owned entity types for type safety.
- **Concurrency control:** No optimistic concurrency tokens on any entity. Add `RowVersion`/`ConcurrencyToken` to `SearchProfile` and `Job`.

### Technology Choices

- **Authentication:** ASP.NET Core Identity + JWT is recommended over external providers (Auth0, Azure AD B2C) for a personal tool. Keeps the dependency graph simple and cost at zero.
- **Database:** Keep SQLite for local development; Azure SQL for production. Use conditional provider registration in `Program.cs`.
- **Email:** SendGrid's free tier (100 emails/day) is sufficient for personal notifications. Azure Communication Services is the alternative for staying in the Azure ecosystem.
- **PDF generation:** QuestPDF (MIT-licensed) is the best .NET library for generating styled PDF reports. It uses a fluent API that's easy to maintain.
