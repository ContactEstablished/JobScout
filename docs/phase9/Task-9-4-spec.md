# Implementation Spec — Task 9.4: Search & Filtering Improvements

> **Scope contract:** `Task-9-4.md` (read that first — this spec implements it).
> **Status:** ✅ Reviewed & approved — ready to build.
> **Branch when building:** off `main`, e.g. `phase9/9.4-search-filtering`.

This spec turns the scope items — **full-text search (9.4.a)**, **sort-by dropdown (9.4.b)**,
**saved filter presets (9.4.c)**, and **filter-state refresh-persistence (9.4.d)** — into a
file-by-file build plan grounded in the current code.

---

## 0. Decisions (resolved during review)

1. **Salary sort — dropped.** `Job.Salary` is a raw `string?`. We do **not** add a numeric column
   and do **not** offer "Salary" as a sort option. Salary *fit* is already handled by the AI:
   `ClaudeAiScoringService` feeds `job.Salary` (`:345`) and the profile's `DesiredSalaryMin/Max`
   (`:299-306`) into the prompt and returns a `compensationFit` sub-score. Sort options are
   **AI Score · Posted Date · Company**.
2. **Freshness/recency ranking — split out to `Task 9.6`.** Not in 9.4. 9.4 sorts by the **raw** AI
   score; the ordering code is structured so Task 9.6 can later swap "AI Score" to a freshness-adjusted
   "effective score" in one place (see §2.2 note).
3. **Presets are per-profile.** `FilterPreset` carries a `ProfileId` FK to `SearchProfile`
   (cascade-delete), matching every other per-profile entity (`UserRating`, `AiScore`, `DailyMetric`,
   `JobApplication`, `CustomJobSource`). The presets dropdown shows the **active profile's** presets.
4. **Search backend: `LIKE`/`Contains`** (not FTS5) — right for local-first volumes.
5. **Refresh-persistence via `localStorage`** (not URL state). The current filter/search/sort snapshot
   is saved to `localStorage` and restored on load, using the existing raw `IJSRuntime` interop
   pattern (`JwtAuthenticationStateProvider.cs`) — no new NuGet package.

> **Pre-existing observation (out of scope):** `JobsController.GetJobs` requires `profileId` but
> doesn't verify it belongs to the current user. New preset endpoints WILL verify profile ownership
> via `IProfileRepository.GetByIdAsync(profileId, userId)`. Noting the existing gap, not widening it.

---

## 1. Approach at a glance

| Scope item | Layers touched | New schema? |
|---|---|---|
| 9.4.a Full-text search | Infrastructure (query predicate) | No |
| 9.4.b Sort-by dropdown | Core (enum), Infra (ordering), Api (param), Web (DTO/UI) | No |
| 9.4.c Saved presets (per-profile) | Core, Infra (+migration), Api, Web | **Yes** — `FilterPresets` |
| 9.4.d localStorage persistence | Web only | No |

Build order: **9.4.a + 9.4.b + 9.4.d together** (small, no migration), then **9.4.c**. Two PRs (§7).

---

## 2. Part A — Full-text search + Sort + Persistence (9.4.a / b / d)

### 2.1 Core — new sort enum
**New file:** `src/JobScout.Core/Enums/JobSortBy.cs`
```csharp
namespace JobScout.Core.Enums;

public enum JobSortBy
{
    AiScore,      // default — profile's AI fit score, desc
    PostedDate,   // PostedAt (fallback DiscoveredAt), desc
    Company       // company name, asc
}
```

**Edit:** `src/JobScout.Core/Interfaces/IJobRepository.cs` — add to `JobFilterOptions`:
```csharp
public JobSortBy SortBy { get; set; } = JobSortBy.AiScore;
```

### 2.2 Infrastructure — query changes
**Edit:** `src/JobScout.Infrastructure/Repositories/JobRepository.cs`, in `GetByProfileAsync`.

**(a) Broaden the search predicate** (replace the current title+company block, lines ~42-48):
```csharp
if (!string.IsNullOrWhiteSpace(filters.Query))
{
    var q = filters.Query.ToLower();
    query = query.Where(j =>
        j.Title.ToLower().Contains(q) ||
        j.Company.ToLower().Contains(q) ||
        j.Description.ToLower().Contains(q) ||
        j.Tags.ToLower().Contains(q) ||                    // Tags is JSON text; substring match is fine
        j.AiScores.Any(s => s.ProfileId == profileId &&
                            s.Reasoning.ToLower().Contains(q)));
}
```

**(b) Replace the hardcoded ordering** (line ~58) with a switch:
```csharp
query = (filters?.SortBy ?? JobSortBy.AiScore) switch
{
    JobSortBy.PostedDate => query.OrderByDescending(j => j.PostedAt ?? j.DiscoveredAt),
    JobSortBy.Company    => query.OrderBy(j => j.Company),
    _ /* AiScore */      => query.OrderByDescending(j => j.AiScores
                                .Where(s => s.ProfileId == profileId)
                                .Max(s => (decimal?)s.Score)),
};

// Stable, deterministic tie-breaker for every sort.
query = ((IOrderedQueryable<Job>)query).ThenByDescending(j => j.DiscoveredAt);
```
Notes:
- `Max((decimal?)s.Score)` over the profile-filtered scores is EF-translatable and matches the fit
  score the card shows. The existing `Where(j => j.AiScores.Any(profile))` guard guarantees it's
  never null in practice.
- Ordering is applied **before** `Skip/Take`, so pagination stays correct.
- **Task 9.6 hook:** the `AiScore` arm is the *single* place freshness ranking will later plug in —
  it'll become `OrderByDescending(effectiveScore)` where `effectiveScore = rawScore + freshnessBonus`.

### 2.3 Api — accept the sort param
**Edit:** `src/JobScout.Api/Controllers/JobsController.cs`, `GetJobs` signature + filter build:
```csharp
[FromQuery] JobSortBy sort = JobSortBy.AiScore,
...
var filters = new JobFilterOptions
{
    Source = source, MinScore = minScore, LocationType = locationType,
    JobType = jobType, Query = q, SortBy = sort
};
```
Enum binds by name from the query string (`?sort=PostedDate`); `JsonStringEnumConverter` is registered
and MVC query binding parses enum names out of the box.

### 2.4 Web — DTO, state, client, UI
- **`src/JobScout.Web/Services/JobsFilter.cs`** — add `public string? SortBy { get; set; }`.
- **`src/JobScout.Web/Services/FilterStateService.cs`**:
  - add `public string? SortBy { get; set; } = "AiScore";`
  - include it in `ToJobsFilter()` (`SortBy = SortBy`);
  - reset `SortBy = "AiScore"` in `Clear()`.
- **`src/JobScout.Web/Services/JobsService.cs`** — in `BuildJobsQuery`, append
  `if (filter?.SortBy is not null) parts.Add($"sort={Uri.EscapeDataString(filter.SortBy)}");`
- **`src/JobScout.Web/Pages/Home.razor`** — add a sort `<select>` in the `.toolbar` next to the
  search box, bound via a wrapper property that sets `Filters.SortBy` then calls `ReloadAsync()`
  (same pattern as `FilterPanel`'s `_minScoreStr`). Options: AI Score / Posted Date / Company.

### 2.5 Web — localStorage refresh-persistence (9.4.d)
Use the existing raw interop pattern (`_js.InvokeAsync<string?>("localStorage.getItem", key)` /
`InvokeVoidAsync("localStorage.setItem", key, json)`).

- **`FilterStateService`** — inject `IJSRuntime` (it's a WASM singleton; injection is fine) and add:
```csharp
private const string StorageKey = "jobscout_filter_state";

public async Task SaveAsync()
{
    var snapshot = new { SearchQuery, Source, MinScore, LocationType, JobType, SortBy };
    await _js.InvokeVoidAsync("localStorage.setItem", StorageKey,
        System.Text.Json.JsonSerializer.Serialize(snapshot));
}

public async Task LoadAsync()   // populate fields from storage; no event fire
{
    var json = await _js.InvokeAsync<string?>("localStorage.getItem", StorageKey);
    if (string.IsNullOrEmpty(json)) return;
    // deserialize into the same fields (guard against malformed JSON with try/catch)
}
```
- **`Home.razor`**:
  - in `OnAfterRenderAsync(firstRender)` → `await Filters.LoadAsync();` **before** the first
    `ReloadAsync()` (JS interop must run post-render, which is already where the first load happens);
  - in the `OnFiltersChanged` handler (and after sort changes) → `await Filters.SaveAsync();`.
- Snapshot is a single global "last view" (filters are profile-agnostic — no profile ids stored).
  Malformed/old JSON is ignored gracefully.

**Acceptance mapping (Part A):** 9.4.a → §2.2(a); 9.4.b options/persistence-across-paging →
§2.2(b)+§2.4; 9.4.d → §2.5.

---

## 3. Part B — Saved filter presets, per-profile (9.4.c)

Mirrors the per-profile entity pattern (`CustomJobSource`) and the repo/controller pattern
(`ProfileRepository` / `ProfilesController`).

### 3.1 Core
**New model:** `src/JobScout.Core/Models/FilterPreset.cs`
```csharp
using JobScout.Core.Enums;

namespace JobScout.Core.Models;

public class FilterPreset
{
    public Guid Id { get; set; }
    public Guid ProfileId { get; set; }
    public string Name { get; set; } = string.Empty;

    public JobSource? Source { get; set; }
    public decimal? MinScore { get; set; }
    public LocationType? LocationType { get; set; }
    public JobType? JobType { get; set; }
    public string? Query { get; set; }
    public JobSortBy SortBy { get; set; } = JobSortBy.AiScore;

    public DateTime CreatedAt { get; set; }
    public SearchProfile Profile { get; set; } = null!;
}
```
**Edit `SearchProfile.cs`** — add nav collection (next to `CustomSources`):
`public ICollection<FilterPreset> FilterPresets { get; set; } = [];`

**New DTOs:** `src/JobScout.Core/DTOs/FilterPresetDtos.cs` — `FilterPresetDto` (mirrors the model,
includes `ProfileId`) and `SaveFilterPresetRequest` (`ProfileId`, `Name`, the filter fields, `SortBy`).

**New interface:** `src/JobScout.Core/Interfaces/IFilterPresetRepository.cs`
```csharp
using JobScout.Core.Models;

namespace JobScout.Core.Interfaces;

public interface IFilterPresetRepository
{
    Task<IReadOnlyList<FilterPreset>> GetByProfileAsync(Guid profileId);
    Task<FilterPreset?> GetByIdAsync(Guid id);
    Task AddAsync(FilterPreset preset);
    Task UpdateAsync(FilterPreset preset);
    Task DeleteAsync(Guid id);
}
```
(Ownership is enforced at the controller via the profile check — see §3.3.)

### 3.2 Infrastructure
**Edit `JobScoutDbContext.cs`:**
- add `public DbSet<FilterPreset> FilterPresets => Set<FilterPreset>();`
- in `OnModelCreating`, add (modeled on the `CustomJobSource` block):
```csharp
modelBuilder.Entity<FilterPreset>(entity =>
{
    entity.HasKey(p => p.Id);
    entity.Property(p => p.Name).HasMaxLength(100);
    entity.Property(p => p.Source).HasConversion<string>();
    entity.Property(p => p.LocationType).HasConversion<string>();
    entity.Property(p => p.JobType).HasConversion<string>();
    entity.Property(p => p.SortBy).HasConversion<string>();
    entity.Property(p => p.MinScore).HasPrecision(4, 2);
    entity.HasIndex(p => new { p.ProfileId, p.Name }).IsUnique();   // one name per profile
    entity.HasOne(p => p.Profile)
          .WithMany(sp => sp.FilterPresets)
          .HasForeignKey(p => p.ProfileId)
          .OnDelete(DeleteBehavior.Cascade);
});
```

**New repo:** `src/JobScout.Infrastructure/Repositories/FilterPresetRepository.cs` — same shape as
`ProfileRepository` (filter reads by `profileId`, `SaveChangesAsync` after writes).

**Migration** (per `DEVELOPING.md`; auto-applies on startup):
```
cd src/JobScout.Infrastructure
dotnet ef migrations add AddFilterPresets --startup-project ../JobScout.Api
```
Verify the generated `Up()` creates only the `FilterPresets` table before committing.

### 3.3 Api
**New controller:** `src/JobScout.Api/Controllers/FilterPresetsController.cs` — `[ApiController]`,
`[Authorize]`, `[Route("api/[controller]")]`, inject `IFilterPresetRepository`, `IProfileRepository`,
`ICurrentUserService`. **Every action first verifies profile ownership:**
`if (await _profiles.GetByIdAsync(profileId, _currentUser.UserId) is null) return NotFound();`

| Verb | Route | Body | Notes |
|---|---|---|---|
| GET | `/api/filterpresets?profileId={guid}` | — | verify profile → return `FilterPresetDto[]` |
| POST | `/api/filterpresets` | `SaveFilterPresetRequest` (has `ProfileId`) | verify profile → 201 |
| PUT | `/api/filterpresets/{id}` | `SaveFilterPresetRequest` | load preset → verify its profile → 204/404 |
| DELETE | `/api/filterpresets/{id}` | — | load preset → verify its profile → 204/404 |

Add `ToDto(this FilterPreset)` to `MappingExtensions.cs`. On POST set `Id = Guid.NewGuid()`,
`CreatedAt = DateTime.UtcNow`. Catch the unique `(ProfileId, Name)` violation → `409 Conflict`.

**Edit `src/JobScout.Api/Program.cs`** — register alongside the other repos (~line 132):
```csharp
builder.Services.AddScoped<IFilterPresetRepository, FilterPresetRepository>();
```

### 3.4 Web
- **New client:** `src/JobScout.Web/Services/FilterPresetsService.cs` (primary-constructor style):
  `GetAsync(Guid profileId)`, `SaveAsync(SaveFilterPresetRequest)`, `UpdateAsync(id, req)`,
  `DeleteAsync(id)`.
- **Register in `src/JobScout.Web/Program.cs`** with the auth handler (mirror `JobsService`).
- **`FilterStateService`** — add `LoadFrom(FilterPresetDto p)` (hydrate fields + fire
  `OnFiltersChanged`) and `ToSaveRequest(Guid profileId, string name)`.
- **UI** — a "Presets" control in the `.toolbar` (or top of `FilterPanel`): dropdown of the **active
  profile's** presets (select → `LoadFrom` → reload), "Save current" (prompt name →
  `SaveAsync(Filters.ToSaveRequest(ProfileState.ActiveProfile.Id, name))` → `ToastService`), delete
  per preset. Reuse existing `.filter-field` / button styles. Reload presets when the active profile
  changes (subscribe to `ProfileStateService.OnChange`).

**Acceptance mapping (Part B):** save/recall → §3.3+§3.4; per-profile + survives restart → DB table
with `ProfileId` FK; rename/delete → PUT/DELETE + UI.

---

## 4. Testing plan

No `JobRepositoryTests` exists yet — the search/sort path is currently untested, so we add coverage.

**`tests/.../Infrastructure.Tests/Repositories/JobRepositoryTests.cs`** (new) — `SqliteFixture` +
`JobBuilder`/`AiScoreBuilder`:
- search matches by **description only** / by **AI reasoning** → returned; non-match excluded;
  case-insensitive; **composes with** another filter;
- sort by `AiScore` desc, `PostedDate` desc (null `PostedAt` → `DiscoveredAt`), `Company` asc;
  tie-breaker deterministic; pagination count correct while sorted.

**`tests/.../Infrastructure.Tests/Repositories/FilterPresetRepositoryTests.cs`** (new) — CRUD,
`GetByProfileAsync` returns only that profile's presets, unique-name-per-profile.

**`tests/.../Api.Tests/Controllers/FilterPresetsControllerTests.cs`** (new, mirror
`ProfilesControllerTests` + `JobScoutWebApplicationFactory`) — 401 without token; create→list→
update→delete round-trip; **cannot touch a preset on another user's profile** (404).

**`tests/.../Web.Tests/Components/`** (bUnit) — sort `<select>` change triggers reload; selecting a
preset hydrates `FilterStateService`. (localStorage persistence is verified manually — JS interop is
awkward to unit-test; keep it thin.)

Target: ~12–18 new tests, suite stays green.

---

## 5. File manifest

**New (11):** `Enums/JobSortBy.cs`, `Models/FilterPreset.cs`, `DTOs/FilterPresetDtos.cs`,
`Interfaces/IFilterPresetRepository.cs`, `Repositories/FilterPresetRepository.cs`,
`Controllers/FilterPresetsController.cs`, `Services/FilterPresetsService.cs` (Web), the
`AddFilterPresets` migration, + 3 test files.

**Edited (~11):** `IJobRepository.cs`, `JobRepository.cs`, `JobsController.cs`, `JobScoutDbContext.cs`,
`SearchProfile.cs`, `MappingExtensions.cs`, `Api/Program.cs`, `Web/Program.cs`, and Web
`JobsFilter.cs` / `FilterStateService.cs` / `JobsService.cs` / `Home.razor` (+ optional
`FilterPanel.razor`).

---

## 6. Migration & rollout

1. Add code + the `AddFilterPresets` migration.
2. `dotnet build` (expect 0/0) → `dotnet test` (all green).
3. Run the app; migration auto-applies on startup (`Program.cs` `MigrateAsync`). New table appears in
   `~/.jobscout/jobscout.db`; no backfill.
4. No config, secrets, or env changes.

---

## 7. Suggested PR breakdown

- **PR 1 — Search + Sort + Persistence (9.4.a/b/d):** Part A + tests. No migration, low risk,
  immediately useful. Delivers the highest-value piece first.
- **PR 2 — Saved presets (9.4.c):** Part B + migration + tests.

---

## 8. Out of scope / risks

- **Out:** numeric salary sort (salary fit handled by the AI `compensationFit` sub-score);
  **freshness/recency ranking → `Task 9.6`**; fuzzy/typo search; FTS5; URL-encoded view state
  (localStorage chosen instead); mobile layout of new controls (`§9.1`, deferred).
- **Risk — default-sort change:** the feed's default order changes from newest to highest-scored.
  Intended (matches the "scored and ranked" promise); user-visible, called out.
- **Risk — `Contains` on `Description`/`Reasoning`** is a table scan. Fine at local volumes; revisit
  FTS5 only if a real slowdown appears (tracked, not built).
