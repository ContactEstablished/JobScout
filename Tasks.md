# Phase 6: Notifications & Alerts

**Priority: MEDIUM** | **Branch:** `phase6/notifications`

> Reference: [Roadmap.md](Roadmap.md) — Phase 6 (sections 6.1 through 6.3)

---

## Status Overview

| # | Task | Status |
|---|------|--------|
| 6.1 | `Notification` entity + EF migration | DONE |
| 6.2 | `NotificationService` + event hooks (ingestion, scoring, applications) | DONE |
| 6.3 | `NotificationsController` REST endpoints | DONE |
| 6.4 | Bell icon dropdown + unread-count badge in `TopBar.razor` | DONE |
| 6.5 | `NotificationPreferences` entity + `Settings.razor` page | DONE |
| 6.6 | `IEmailSender` + SendGrid implementation | DONE |
| 6.7 | HTML email templates (digest, instant alert) | DONE |
| 6.8 | Quiet hours + digest scheduler (Azure Function) | DONE |
| 6.9 | End-to-end verification | DONE |
| 6.10 | Commit & PR | DONE |

---

## Existing Infrastructure

The following already exist and should be leveraged:

- **`ApplicationUser`** — `src/JobScout.Infrastructure/Identity/ApplicationUser.cs`
  - Identity user is already the per-user anchor; notifications and preferences hang off `UserId` (string).
- **`JobScoutDbContext`** — `src/JobScout.Infrastructure/Data/JobScoutDbContext.cs`
  - Inherits from `IdentityDbContext<ApplicationUser>`. Add new `DbSet<Notification>` and `DbSet<NotificationPreferences>` here.
- **`JobIngestionService`** — `src/JobScout.Infrastructure/Services/JobIngestionService.cs`
  - Fires after every ingestion run — hook `NotificationService.OnIngestionCompleteAsync` here.
- **`ClaudeAiScoringService`** — `src/JobScout.Infrastructure/AI/ClaudeAiScoringService.cs`
  - After scoring, emit a `NewStrongFit` notification when `Score >= 8` (and `>= 9` triggers an instant email if configured).
- **`ApplicationTrackingService`** — `src/JobScout.Infrastructure/Services/ApplicationTrackingService.cs`
  - On status transition, emit an `ApplicationStatusChange` notification.
- **`TopBar.razor`** — `src/JobScout.Web/Components/TopBar.razor`
  - Already renders a placeholder bell icon (`<i class="ti ti-bell">`). Replace its click handler with the new dropdown.
- **`ICurrentUserService`** — `src/JobScout.Api/Services/CurrentUserService.cs`
  - Resolves the authenticated `UserId` for scoping notifications and preferences.
- **`Program.cs`** — `src/JobScout.Api/Program.cs`
  - Services registered as `AddScoped<IInterface, Implementation>()`. Email sender registered as `AddSingleton<IEmailSender, SendGridEmailSender>()`.
- **Azure Functions** — `functions/JobScout.Functions/`
  - Already runs scheduled ingestion every 4 hours via timer trigger. Add the digest-email function alongside it.

---

## Task Details

### 6.1 Notification Entity & Migration

**Files to create:**
- `src/JobScout.Core/Models/Notification.cs`
- `src/JobScout.Core/Enums/NotificationType.cs`
- `src/JobScout.Core/DTOs/NotificationDto.cs`
- EF Core migration: `AddNotifications`

**Files to modify:**
- `src/JobScout.Infrastructure/Data/JobScoutDbContext.cs`
- `src/JobScout.Api/Mapping/MappingExtensions.cs`

**Requirements:**
- [ ] Create `NotificationType` enum: `NewStrongFit`, `ScoreUpdate`, `IngestionComplete`, `ApplicationStatusChange`
- [ ] Create `Notification` entity:
  - `Guid Id`
  - `string UserId` — FK to `ApplicationUser.Id` (cascade delete)
  - `Guid? ProfileId` — optional FK to `SearchProfile`
  - `NotificationType Type`
  - `string Title` (max 200)
  - `string Message` (max 1000)
  - `bool IsRead` (default false)
  - `DateTime CreatedAt`
  - `DateTime? ReadAt`
  - `Guid? RelatedJobId` — nullable, no FK (jobs may be deleted)
  - `Guid? RelatedApplicationId`
- [ ] Register `DbSet<Notification>` in `JobScoutDbContext`
- [ ] Configure indexes: `(UserId, IsRead, CreatedAt DESC)` for the unread query
- [ ] Configure `Type` as string conversion (matches the existing enum pattern)
- [ ] Generate migration `AddNotifications`
- [ ] Add `NotificationDto` + `ToDto()` extension in `MappingExtensions`

**Acceptance criteria:** Migration applies cleanly. Inserting a notification row and querying by `UserId` returns it.

---

### 6.2 NotificationService + Event Hooks

**Files to create:**
- `src/JobScout.Core/Interfaces/INotificationService.cs`
- `src/JobScout.Infrastructure/Services/NotificationService.cs`

**Files to modify:**
- `src/JobScout.Infrastructure/Services/JobIngestionService.cs`
- `src/JobScout.Infrastructure/AI/ClaudeAiScoringService.cs`
- `src/JobScout.Infrastructure/Services/ApplicationTrackingService.cs`
- `src/JobScout.Api/Program.cs`

**Requirements:**
- [ ] `INotificationService` surface:
  - `Task CreateAsync(string userId, NotificationType type, string title, string message, Guid? profileId = null, Guid? jobId = null, Guid? applicationId = null)`
  - `Task<IReadOnlyList<Notification>> GetForUserAsync(string userId, bool unreadOnly = false, int take = 50)`
  - `Task<int> GetUnreadCountAsync(string userId)`
  - `Task MarkReadAsync(Guid notificationId, string userId)`
  - `Task MarkAllReadAsync(string userId)`
  - `Task OnIngestionCompleteAsync(SearchProfile profile, IngestionResult result)`
  - `Task OnHighScoreCreatedAsync(AiScore score, Job job)` — fires when `Score >= 8`
  - `Task OnApplicationStatusChangedAsync(JobApplication app, ApplicationStatus oldStatus, ApplicationStatus newStatus)`
- [ ] Hook `OnIngestionCompleteAsync` into `JobIngestionService` after the final `SaveChangesAsync` — only fire when `result.NewJobs > 0`
- [ ] Hook `OnHighScoreCreatedAsync` into `ClaudeAiScoringService.BatchScoreAsync` — call it for each score that crosses the threshold
- [ ] Hook `OnApplicationStatusChangedAsync` into `ApplicationTrackingService.UpdateStatusAsync`
- [ ] Respect `NotificationPreferences` (Task 6.5): if the user has disabled a notification type, persist nothing
- [ ] Register `INotificationService` as `AddScoped` in `Program.cs`

**Acceptance criteria:** A scored job with `Score >= 8` produces exactly one new `Notification` row. An ingestion with zero new jobs produces no notification. Status transitions on `JobApplication` produce one row each.

---

### 6.3 Notifications API Endpoints

**Files to create:**
- `src/JobScout.Api/Controllers/NotificationsController.cs`

**Files to modify:**
- `src/JobScout.Api/Mapping/MappingExtensions.cs` (if not already covered by 6.1)

**Requirements:**
- [ ] `GET /api/notifications?unreadOnly=true&take=50` — returns `IReadOnlyList<NotificationDto>` for the current user
- [ ] `GET /api/notifications/unread-count` — returns `{ count: int }`
- [ ] `PUT /api/notifications/{id}/read` — marks one notification read; 404 if not owned by current user
- [ ] `PUT /api/notifications/read-all` — marks every unread notification for the current user read
- [ ] `DELETE /api/notifications/{id}` — hard-delete, scoped to current user
- [ ] All endpoints `[Authorize]` and scoped via `ICurrentUserService.UserId`

**Acceptance criteria:** Authenticated user A cannot read or mutate user B's notifications (404 on every cross-tenant attempt).

---

### 6.4 Bell Icon Dropdown UI

**Files to create:**
- `src/JobScout.Web/Components/NotificationDropdown.razor`
- `src/JobScout.Web/Components/NotificationDropdown.razor.css`
- `src/JobScout.Web/Services/NotificationApi.cs` (HTTP client wrapper)

**Files to modify:**
- `src/JobScout.Web/Components/TopBar.razor`
- `src/JobScout.Web/Program.cs` (DI for `NotificationApi`)

**Requirements:**
- [ ] Replace the placeholder bell button in `TopBar.razor` (line 47) with a button that toggles `NotificationDropdown`
- [ ] Render a numeric badge over the bell when `unreadCount > 0` (show "9+" when count >= 10)
- [ ] Dropdown lists the most recent 10 notifications with: icon (per type), title, message preview, relative time ("3m ago"), and an unread dot for unread items
- [ ] Clicking a notification: marks it read and navigates to the related job/application page (use `RelatedJobId` / `RelatedApplicationId` to build the route)
- [ ] Dropdown header has a "Mark all read" button calling `PUT /api/notifications/read-all`
- [ ] Footer "View all" link routes to a future `/notifications` page (out of scope for Phase 6; placeholder OK)
- [ ] Poll `/api/notifications/unread-count` every 60s while the page is open
- [ ] Match dark theme styling (use existing CSS variables)

**Acceptance criteria:** Creating a notification via the API causes the badge to update within 60s. Clicking a notification marks it read and the badge decrements without a page refresh.

---

### 6.5 NotificationPreferences + Settings.razor

**Files to create:**
- `src/JobScout.Core/Models/NotificationPreferences.cs`
- `src/JobScout.Core/DTOs/NotificationPreferencesDto.cs`
- `src/JobScout.Api/Controllers/SettingsController.cs`
- `src/JobScout.Web/Pages/Settings.razor`
- `src/JobScout.Web/Pages/Settings.razor.css`
- EF Core migration: `AddNotificationPreferences`

**Files to modify:**
- `src/JobScout.Infrastructure/Data/JobScoutDbContext.cs`
- `src/JobScout.Web/Components/Sidebar.razor` (add "Settings" nav entry)

**Requirements:**
- [ ] `NotificationPreferences` entity (one row per `ApplicationUser`):
  - `string UserId` — PK, FK to `ApplicationUser`
  - `bool InAppNewStrongFit` (default true)
  - `bool InAppScoreUpdate` (default true)
  - `bool InAppIngestionComplete` (default true)
  - `bool InAppApplicationStatusChange` (default true)
  - `bool EmailDailyDigest` (default false)
  - `bool EmailWeeklySummary` (default false)
  - `bool EmailInstantStrongMatch` (default false) — fires when `Score >= 9`
  - `TimeOnly? QuietHoursStart`, `TimeOnly? QuietHoursEnd` (UTC; email skipped within window)
  - `string TimeZoneId` (IANA, default "UTC")
- [ ] Generate migration `AddNotificationPreferences`
- [ ] `GET /api/settings/notifications` — returns prefs (auto-creates default row on first read)
- [ ] `PUT /api/settings/notifications` — upsert
- [ ] `Settings.razor` page at `/settings`:
  - Toggle switches per in-app type
  - Toggle switches per email type
  - Quiet hours start/end pickers + timezone dropdown
  - Save button calls `PUT /api/settings/notifications`
- [ ] `NotificationService.CreateAsync` checks prefs before persisting
- [ ] Sidebar "Settings" link with `ti-settings` icon

**Acceptance criteria:** Toggling "In-app: New Strong Fit" off causes no notification rows to be created on subsequent high scores. Settings persist across logout/login.

---

### 6.6 Email Service Integration

**Files to create:**
- `src/JobScout.Core/Interfaces/IEmailSender.cs`
- `src/JobScout.Infrastructure/Email/SendGridEmailSender.cs`
- `src/JobScout.Infrastructure/Email/NullEmailSender.cs` — used when no API key is configured

**Files to modify:**
- `src/JobScout.Infrastructure/JobScout.Infrastructure.csproj` — add `SendGrid` package
- `src/JobScout.Api/Program.cs`
- `appsettings.json` / user secrets

**Requirements:**
- [ ] `IEmailSender.SendAsync(EmailMessage message, CancellationToken ct)` with `EmailMessage { ToAddress, ToName, Subject, HtmlBody, PlainTextBody }`
- [ ] Add `SendGrid` NuGet (latest 9.x) to `JobScout.Infrastructure.csproj`
- [ ] Store key under `SendGrid:ApiKey` and `SendGrid:FromAddress` / `SendGrid:FromName` in configuration (user secrets for dev)
- [ ] `SendGridEmailSender` constructs `SendGridClient` once (registered as singleton)
- [ ] If `SendGrid:ApiKey` is missing or empty, register `NullEmailSender` (logs the email at Information level and returns success)
- [ ] On non-2xx response, log the SendGrid error body and throw — caller decides whether to retry
- [ ] Honor quiet hours: `IEmailSender` should NOT itself check prefs — `NotificationService` is the gatekeeper

**Acceptance criteria:** With a real API key configured, an instant alert email arrives in the recipient inbox. With the key missing, the service starts cleanly and `NullEmailSender` is used.

---

### 6.7 HTML Email Templates

**Files to create:**
- `src/JobScout.Infrastructure/Email/Templates/InstantAlertTemplate.cs`
- `src/JobScout.Infrastructure/Email/Templates/DailyDigestTemplate.cs`
- `src/JobScout.Infrastructure/Email/Templates/WeeklySummaryTemplate.cs`

**Requirements:**
- [ ] Templates are static C# methods that return `(string Subject, string HtmlBody, string PlainTextBody)` — no Razor runtime needed for now
- [ ] Use inline CSS (no external stylesheets); honor the app's dark-mode palette but tested for white-background email clients (Gmail, Outlook)
- [ ] **Instant alert:** subject "New strong match: {Job.Title} at {Job.Company}". Body shows job card with score, top matched keywords, "Apply" CTA linking to `Job.SourceUrl`, and a "View in JobScout" CTA linking to the app
- [ ] **Daily digest:** subject "Your JobScout digest — {count} new matches". Lists up to 10 strong fits (score ≥ 8) from the last 24h, sorted by score desc
- [ ] **Weekly summary:** subject "JobScout: {totalJobs} jobs, {strongFits} strong fits this week". Includes pipeline summary (applied/interviewing/offered counts) and the top 5 jobs of the week
- [ ] Render a plain-text fallback for every template (accessibility + deliverability)
- [ ] Image-free (no remote-loaded assets) — use Unicode glyphs or HTML entities for icons

**Acceptance criteria:** Each template renders without errors against representative data and passes a manual Gmail + Outlook visual check.

---

### 6.8 Quiet Hours + Digest Scheduler

**Files to create:**
- `functions/JobScout.Functions/Functions/DailyDigestFunction.cs`
- `functions/JobScout.Functions/Functions/WeeklySummaryFunction.cs`

**Files to modify:**
- `src/JobScout.Infrastructure/Services/NotificationService.cs` (quiet-hours check before emails)
- `functions/JobScout.Functions/Program.cs` (DI for `IEmailSender`, `JobScoutDbContext`, templates)

**Requirements:**
- [ ] `DailyDigestFunction` — timer trigger `0 0 13 * * *` (13:00 UTC ≈ 9am US Eastern). For each user with `EmailDailyDigest = true`:
  - Convert "now" to user's `TimeZoneId`
  - Skip if currently within their quiet hours
  - Gather strong fits (score ≥ 8) from the last 24h scoped to the user's profiles
  - Skip if zero matches
  - Render `DailyDigestTemplate` and send via `IEmailSender`
- [ ] `WeeklySummaryFunction` — timer trigger `0 0 14 * * MON` (Monday 14:00 UTC). Same flow with weekly window and `EmailWeeklySummary`
- [ ] Instant alerts (`Score >= 9` + `EmailInstantStrongMatch = true`) are dispatched inline from `NotificationService.OnHighScoreCreatedAsync` — skip if within quiet hours
- [ ] Quiet hours helper: `bool IsWithinQuietHours(NotificationPreferences prefs, DateTimeOffset utcNow)` — handles wrap-around (e.g., 22:00–06:00)

**Acceptance criteria:** Running the digest function locally with a seeded user dispatches one email per qualifying user. Setting quiet hours that cover "now" suppresses the email.

---

### 6.9 End-to-End Verification

- [ ] Solution builds: 0 errors, 0 warnings across all projects
- [ ] All migrations apply cleanly on a fresh database
- [ ] Authenticated `GET /api/notifications` returns only the current user's rows
- [ ] Cross-tenant access (user A reads user B's notification by ID) returns 404
- [ ] Manual ingestion with new jobs creates exactly one `IngestionComplete` notification per profile
- [ ] Scoring a job with `Score >= 8` creates a `NewStrongFit` notification
- [ ] Application status transition creates an `ApplicationStatusChange` notification
- [ ] Bell icon badge reflects unread count within 60s of creation
- [ ] Marking a notification read decrements the badge without a page refresh
- [ ] `Settings.razor` toggle for an in-app type, when turned off, suppresses future rows of that type
- [ ] Email send path works with a real `SendGrid:ApiKey`; with no key, `NullEmailSender` logs the would-be email
- [ ] Quiet hours suppress the daily digest when "now" falls inside the window
- [ ] Daily digest contains only jobs from the last 24h with `Score >= 8`

---

### 6.10 Commit & PR

- [ ] Stage all Phase 6 changes
- [ ] Commit with descriptive message
- [ ] Push branch `phase6/notifications`
- [ ] Create PR targeting `main`
