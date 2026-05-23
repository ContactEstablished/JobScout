# Phase 3: Application Tracking Pipeline

**Priority: HIGH** | **Branch:** `phase3/application-tracking`

> Reference: [Roadmap.md](Roadmap.md) — Phase 3 (sections 3.1 through 3.5)

---

## Status Overview

| # | Task | Status |
|---|------|--------|
| 3.1 | Expand JobApplication model + EF navigation properties | TODO |
| 3.2 | IApplicationRepository + EF implementation | TODO |
| 3.3 | IApplicationTrackingService + status transition logic | TODO |
| 3.4 | ApplicationsController (5 endpoints) | TODO |
| 3.5 | Application DTOs + mapping | TODO |
| 3.6 | Applications.razor Kanban board page | TODO |
| 3.7 | Quick-apply button on job cards | TODO |
| 3.8 | Sidebar nav + badge count | TODO |
| 3.9 | DailyMetric auto-increment on apply | TODO |
| 3.10 | End-to-end verification | TODO |
| 3.11 | Commit & PR | TODO |

---

## Existing Infrastructure

The following already exist and should be leveraged:

- **`JobApplication` model** — `src/JobScout.Core/Models/JobApplication.cs`
  - Fields: `Id`, `JobId`, `ProfileId`, `AppliedAt`, `Status`, `Notes`
  - Missing: navigation properties to `Job` and `SearchProfile`, status history tracking
- **`ApplicationStatus` enum** — `src/JobScout.Core/Enums/ApplicationStatus.cs`
  - Values: `Applied`, `Interviewing`, `Offered`, `Rejected`, `Withdrawn`
  - Missing: `Accepted` value (referenced in Roadmap 3.3 Kanban columns)
- **`DbSet<JobApplication>`** — already registered in `JobScoutDbContext`
  - Entity config: `HasKey(Id)`, `Status` stored as string
  - Missing: FK relationships to `Job` and `SearchProfile`, unique index on `(JobId, ProfileId)`

---

## Task Details

### 3.1 Expand JobApplication Model + EF Navigation Properties

**Files to modify:**
- `src/JobScout.Core/Models/JobApplication.cs`
- `src/JobScout.Core/Enums/ApplicationStatus.cs`
- `src/JobScout.Infrastructure/Data/JobScoutDbContext.cs`

**Requirements:**
- [ ] Add `Accepted` to `ApplicationStatus` enum (Roadmap 3.3 specifies an "Accepted" Kanban column)
- [ ] Add navigation properties to `JobApplication`: `Job Job`, `SearchProfile Profile`
- [ ] Add `StatusHistory` property — `List<StatusChange>` stored as JSON, where `StatusChange` is a record with `Status`, `ChangedAt`, `Notes`
- [ ] Add `ICollection<JobApplication> Applications` navigation to `SearchProfile` and `Job` models
- [ ] Configure FK relationships in `JobScoutDbContext.OnModelCreating`:
  - `JobApplication.JobId` → `Job.Id` (cascade delete)
  - `JobApplication.ProfileId` → `SearchProfile.Id` (cascade delete)
  - Unique index on `(JobId, ProfileId)` — one application per job per profile
- [ ] JSON column config for `StatusHistory` with value comparer
- [ ] Generate EF Core migration: `AddApplicationTracking`
- [ ] Verify migration applies cleanly

**Acceptance criteria:** Migration adds FK constraints and unique index. Build succeeds with 0 errors.

---

### 3.2 IApplicationRepository + EF Implementation

**Files to create:**
- `src/JobScout.Core/Interfaces/IApplicationRepository.cs`
- `src/JobScout.Infrastructure/Repositories/ApplicationRepository.cs`

**Requirements:**
- [ ] `GetByProfileAsync(Guid profileId, string userId, ApplicationStatus? status = null)` — returns applications for a profile, optionally filtered by status. Include `Job` navigation.
- [ ] `GetByIdAsync(Guid id, string userId)` — single application by ID with `Job` included. Verify ownership via profile's UserId.
- [ ] `GetByJobAsync(Guid jobId, Guid profileId, string userId)` — check if application exists for a specific job+profile combo
- [ ] `GetPipelineAsync(Guid profileId, string userId)` — return aggregated counts per status
- [ ] `AddAsync(JobApplication application)` — insert
- [ ] `UpdateAsync(JobApplication application)` — update
- [ ] `DeleteAsync(Guid id, string userId)` — delete with ownership check
- [ ] `GetActiveCountAsync(string userId)` — count of non-Rejected, non-Withdrawn applications (for sidebar badge)
- [ ] Register in DI: `builder.Services.AddScoped<IApplicationRepository, ApplicationRepository>()`

**Acceptance criteria:** All repository methods compile. Ownership scoping flows through `SearchProfile.UserId`.

---

### 3.3 IApplicationTrackingService + Status Transition Logic

**Files to create:**
- `src/JobScout.Core/Interfaces/IApplicationTrackingService.cs`
- `src/JobScout.Infrastructure/Services/ApplicationTrackingService.cs`

**Requirements:**
- [ ] `ApplyAsync(Guid jobId, Guid profileId, string userId, string? notes)` — creates application with `Applied` status, records initial `StatusChange`, returns created application
- [ ] `UpdateStatusAsync(Guid applicationId, ApplicationStatus newStatus, string userId, string? notes)` — validates transition, appends to `StatusHistory`, updates status
- [ ] `GetPipelineAsync(Guid profileId, string userId)` — delegates to repository, returns `PipelineDto` with counts
- [ ] Enforce valid status transitions:
  - `Applied` → `Interviewing`, `Rejected`, `Withdrawn`
  - `Interviewing` → `Offered`, `Rejected`, `Withdrawn`
  - `Offered` → `Accepted`, `Rejected`, `Withdrawn`
  - `Accepted` → `Withdrawn`
  - `Rejected` → (terminal, no transitions)
  - `Withdrawn` → (terminal, no transitions)
- [ ] Return error/exception for invalid transitions
- [ ] Register in DI: `builder.Services.AddScoped<IApplicationTrackingService, ApplicationTrackingService>()`

**Acceptance criteria:** Invalid transitions are rejected. Status history records each change with timestamp and notes.

---

### 3.4 ApplicationsController (5 Endpoints)

**File to create:**
- `src/JobScout.Api/Controllers/ApplicationsController.cs`

**Requirements:**
- [ ] `[ApiController]`, `[Authorize]`, `[Route("api/[controller]")]`
- [ ] Inject `IApplicationTrackingService`, `IApplicationRepository`, `ICurrentUserService`
- [ ] `POST /api/applications` — body: `{ jobId, profileId, notes? }`. Returns 201 with created application DTO. Returns 409 if application already exists for that job+profile.
- [ ] `GET /api/applications?profileId={guid}&status={status?}` — list applications with optional status filter. Returns list of application DTOs with job summary included.
- [ ] `GET /api/applications/pipeline?profileId={guid}` — returns `{ applied, interviewing, offered, accepted, rejected, withdrawn }` counts
- [ ] `PUT /api/applications/{id}/status` — body: `{ status, notes? }`. Returns 200 on success, 400 on invalid transition.
- [ ] `DELETE /api/applications/{id}` — hard delete. Returns 204.

**Acceptance criteria:** All 5 endpoints return correct status codes. Invalid transitions return 400 with message.

---

### 3.5 Application DTOs + Mapping

**Files to create/modify:**
- `src/JobScout.Core/DTOs/ApplicationDtos.cs` (new)
- `src/JobScout.Api/Mapping/MappingExtensions.cs` (modify)

**DTOs to create:**
- [ ] `CreateApplicationRequest` — `JobId`, `ProfileId`, `Notes?`
- [ ] `UpdateStatusRequest` — `Status` (ApplicationStatus), `Notes?`
- [ ] `ApplicationDto` — `Id`, `JobId`, `ProfileId`, `AppliedAt`, `Status`, `Notes`, `StatusHistory`, `Job` (JobSummaryDto)
- [ ] `StatusChangeDto` — `Status`, `ChangedAt`, `Notes`
- [ ] `PipelineDto` — `Applied`, `Interviewing`, `Offered`, `Accepted`, `Rejected`, `Withdrawn` (all int)

**Mapping:**
- [ ] `ToDto()` extension for `JobApplication` → `ApplicationDto` (include job summary mapping)

**Acceptance criteria:** All DTOs serialize correctly with `JsonStringEnumConverter`.

---

### 3.6 Applications.razor Kanban Board Page

**Files to create/modify:**
- `src/JobScout.Web/Pages/Applications.razor` (new)
- `src/JobScout.Web/Services/ApplicationsService.cs` (new)
- `src/JobScout.Web/wwwroot/css/app.css` (modify)

**ApplicationsService methods:**
- [ ] `GetByProfileAsync(Guid profileId, ApplicationStatus? status)` → `List<ApplicationDto>`
- [ ] `CreateAsync(CreateApplicationRequest)` → `ApplicationDto?`
- [ ] `UpdateStatusAsync(Guid id, UpdateStatusRequest)` → `bool`
- [ ] `DeleteAsync(Guid id)` → `bool`
- [ ] `GetPipelineAsync(Guid profileId)` → `PipelineDto`

**Page requirements:**
- [ ] Route: `/applications`
- [ ] Scoped to active profile (from `ProfileStateService`)
- [ ] Pipeline summary bar at top showing counts per status
- [ ] Kanban columns: Applied, Interviewing, Offered, Accepted, Rejected
  - Each column shows application cards with: job title, company, date applied, days-in-stage counter
  - Status dropdown on each card for quick status updates
  - Delete button on each card
- [ ] Empty state when no applications exist
- [ ] Click card to expand and show status timeline (list of StatusChange entries with timestamps and notes)
- [ ] Consistent dark-mode styling matching existing pages

**CSS requirements:**
- [ ] `.kanban-board` — horizontal flex container for columns
- [ ] `.kanban-column` — vertical column with header showing status + count
- [ ] `.kanban-card` — individual application card
- [ ] `.timeline` — status history timeline within expanded card
- [ ] Responsive: columns stack vertically on mobile

**Acceptance criteria:** Page renders with all columns. Status updates via dropdown work. Timeline shows history.

---

### 3.7 Quick-Apply Button on Job Cards

**Files to modify:**
- `src/JobScout.Web/Pages/Home.razor` (or wherever JobCard is rendered)
- Possibly `src/JobScout.Web/Components/` if a JobCard component exists

**Requirements:**
- [ ] Add "Apply" button to each job card in the feed
- [ ] Clicking opens a compact modal/popover with:
  - Job title + company (confirmation)
  - Notes textarea (optional)
  - "Track Application" button
- [ ] On confirm: calls `POST /api/applications`, opens `job.SourceUrl` in new tab
- [ ] Button changes to "Applied ✓" after successful creation
- [ ] If application already exists for this job+profile, show "Applied ✓" by default

**Acceptance criteria:** Apply flow creates application and opens job URL. Applied state persists across page refreshes.

---

### 3.8 Sidebar Nav + Badge Count

**Files to modify:**
- `src/JobScout.Web/Components/Sidebar.razor`
- `src/JobScout.Web/Services/ApplicationsService.cs`

**Requirements:**
- [ ] Add "Applications" nav item to sidebar (between Profiles and Analytics sections)
- [ ] Icon: `ti ti-briefcase` or `ti ti-clipboard-list`
- [ ] Badge showing active application count (non-Rejected, non-Withdrawn)
- [ ] Badge updates when applications are created or status changes
- [ ] Link navigates to `/applications`

**Acceptance criteria:** Sidebar shows Applications link with live count badge.

---

### 3.9 DailyMetric Auto-Increment on Apply

**Files to modify:**
- `src/JobScout.Infrastructure/Services/ApplicationTrackingService.cs`
- `src/JobScout.Core/Interfaces/IMetricsService.cs` (if needed)

**Requirements:**
- [ ] When `ApplyAsync` creates a new application, increment today's `DailyMetric.Applied` count for the relevant profile and source
- [ ] If no `DailyMetric` exists for today's date + profile + source, create one
- [ ] Determine the source from the `Job.Source` field

**Acceptance criteria:** Creating an application increments the Applied count in DailyMetrics.

---

### 3.10 End-to-End Verification

- [ ] Solution builds: 0 errors, 0 warnings across all projects
- [ ] Migration applies cleanly
- [ ] `POST /api/applications` creates application, returns 201
- [ ] `POST /api/applications` with duplicate job+profile returns 409
- [ ] `GET /api/applications?profileId=` returns list with job details
- [ ] `GET /api/applications/pipeline?profileId=` returns correct counts
- [ ] `PUT /api/applications/{id}/status` with valid transition returns 200
- [ ] `PUT /api/applications/{id}/status` with invalid transition returns 400
- [ ] `DELETE /api/applications/{id}` returns 204
- [ ] Status history records all transitions with timestamps
- [ ] Sidebar badge shows correct active count
- [ ] Kanban board renders with correct columns and cards

---

### 3.11 Commit & PR

- [ ] Stage all Phase 3 changes
- [ ] Commit with descriptive message
- [ ] Create PR targeting `main`
