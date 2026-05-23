# Phase 4: Job Board Expansion

**Priority: MEDIUM** | **Branch:** `phase4/job-board-expansion`

> Reference: [Roadmap.md](Roadmap.md) — Phase 4 (sections 4.1 through 4.7)

---

## Status Overview

| # | Task | Status |
|---|------|--------|
| 4.1 | IndeedClient via SerpAPI | DONE |
| 4.2 | DiceClient via public search/RSS | DONE |
| 4.3 | WellfoundClient via GraphQL | DONE |
| 4.4 | GlassdoorClient or Google Jobs fallback | DONE |
| 4.5 | Custom source support (entity + generic client) | DONE |
| 4.6 | Cross-source fuzzy deduplication | DONE |
| 4.7 | Canonical job linking | DONE |
| 4.8 | End-to-end verification | DONE |
| 4.9 | Commit & PR | TODO |

---

## Existing Infrastructure

The following already exist and should be leveraged:

- **`IJobBoardClient` interface** — `src/JobScout.Core/Interfaces/IJobBoardClient.cs`
  - `JobSource Source { get; }`
  - `Task<IReadOnlyList<Job>> FetchJobsAsync(SearchProfile profile, CancellationToken ct = default)`
- **Four live client implementations** — `src/JobScout.Infrastructure/ExternalServices/`
  - `SerpApiLinkedInClient.cs` — reference implementation for the SerpAPI pattern
  - `AdzunaClient.cs`, `RemoteOkClient.cs`, `TheMuseClient.cs`
- **`JobSource` enum** — `src/JobScout.Core/Enums/JobSource.cs`
  - Already defines: `LinkedIn`, `Indeed`, `Glassdoor`, `Dice`, `Wellfound`, `RemoteOK`, `Adzuna`, `TheMuse`
  - No new enum values needed — all target sources are pre-declared
- **`Job` entity source fields** — `src/JobScout.Core/Models/Job.cs`
  - `ExternalId` (string), `Source` (JobSource), `SourceUrl` (string)
- **`JobIngestionService`** — `src/JobScout.Infrastructure/Services/JobIngestionService.cs`
  - Discovers all registered `IJobBoardClient` implementations via DI
  - Current deduplication: exact match on `(ExternalId, Source)` only
- **`Program.cs`** — clients are registered individually; each new client needs its own `AddScoped<IJobBoardClient, XyzClient>()` registration

---

## Task Details

### 4.1 IndeedClient via SerpAPI

**Files to create:**
- `src/JobScout.Infrastructure/ExternalServices/SerpApiIndeedClient.cs`

**Files to modify:**
- `src/JobScout.Api/Program.cs` (DI registration)

**Requirements:**
- [ ] Create `SerpApiIndeedClient : IJobBoardClient` with `Source => JobSource.Indeed`
- [ ] Model the implementation after `SerpApiLinkedInClient` — reuse SerpAPI base URL and API key config (`SerpApi:ApiKey`)
- [ ] Set `engine=indeed` in the query string parameters
- [ ] Build query from `profile.SearchKeywords` (joined with space) and `profile.PreferredLocations` if available
- [ ] Map Indeed response fields to the `Job` entity:
  - `job_id` → `ExternalId`
  - `title` → `Title`
  - `company_name` → `Company`
  - `location` → `Location`
  - `description` → `Description`
  - `detected_extensions.salary` → `SalaryRange` (nullable)
  - `detected_extensions.job_type` → `JobType`
  - `date_posted` → `PostedAt`
  - `related_links[0].link` → `SourceUrl`
- [ ] Set `Source = JobSource.Indeed` on every mapped job
- [ ] Register in `Program.cs`: `builder.Services.AddScoped<IJobBoardClient, SerpApiIndeedClient>()`
- [ ] Add null/missing-field guards for all optional response properties

**Acceptance criteria:** `SerpApiIndeedClient` fetches jobs and returns a non-empty list against the live SerpAPI endpoint. Jobs appear in the feed with `Source = Indeed`.

---

### 4.2 DiceClient via Public Search Endpoint

**Files to create:**
- `src/JobScout.Infrastructure/ExternalServices/DiceClient.cs`

**Files to modify:**
- `src/JobScout.Api/Program.cs` (DI registration)
- `appsettings.json` / user secrets (if Dice requires an API key)

**Requirements:**
- [ ] Create `DiceClient : IJobBoardClient` with `Source => JobSource.Dice`
- [ ] Use Dice's public search API endpoint. Dice exposes a REST search endpoint at `https://job-search-api.skopenow.com` or the legacy `https://www.dice.com/jobs/q-{keyword}-jobs.rss` RSS feed — prefer REST if accessible without auth, fall back to RSS
- [ ] If using REST: build query params from `profile.SearchKeywords`, map JSON response to `Job` entity
- [ ] If using RSS/Atom: parse `<item>` elements using `System.ServiceModel.Syndication.SyndicationFeed` or a lightweight XML parser:
  - `<title>` → `Title`
  - `<author>` or `<dice:company>` → `Company`
  - `<location>` or `<dice:location>` → `Location`
  - `<description>` → `Description` (strip HTML tags)
  - `<link>` → `SourceUrl`
  - `<pubDate>` → `PostedAt`
  - Generate `ExternalId` from a stable hash of `(Title + Company + SourceUrl)` if no explicit ID is in the feed
- [ ] Register in `Program.cs`: `builder.Services.AddScoped<IJobBoardClient, DiceClient>()`

**Acceptance criteria:** `DiceClient` returns mapped `Job` objects with `Source = Dice`. Build succeeds with 0 errors.

---

### 4.3 WellfoundClient via GraphQL

**Files to create:**
- `src/JobScout.Infrastructure/ExternalServices/WellfoundClient.cs`

**Files to modify:**
- `src/JobScout.Api/Program.cs` (DI registration)
- `appsettings.json` / user secrets (`Wellfound:AccessToken`)

**Requirements:**
- [ ] Create `WellfoundClient : IJobBoardClient` with `Source => JobSource.Wellfound`
- [ ] Wellfound uses a GraphQL API at `https://wellfound.com/graphql`. Authentication requires a bearer token obtained via Wellfound's developer portal
- [ ] Store the access token under `Wellfound:AccessToken` in configuration
- [ ] Build a GraphQL query for job listings filtered by keywords from `profile.SearchKeywords`:
  ```graphql
  query JobSearch($query: String!) {
    jobListings(query: $query, first: 50) {
      edges {
        node {
          id
          title
          description
          jobType
          locationNames
          salary
          applyUrl
          createdAt
          startup {
            name
          }
        }
      }
    }
  }
  ```
- [ ] Send as a `POST` request with `Content-Type: application/json` and `Authorization: Bearer {token}`
- [ ] Map GraphQL response to `Job` entity:
  - `node.id` → `ExternalId`
  - `node.title` → `Title`
  - `node.startup.name` → `Company`
  - `node.locationNames[0]` → `Location`
  - `node.description` → `Description`
  - `node.salary` → `SalaryRange`
  - `node.jobType` → `JobType`
  - `node.applyUrl` → `SourceUrl`
  - `node.createdAt` → `PostedAt`
- [ ] If GraphQL schema differs, adapt field names accordingly — document any discrepancies in code comments
- [ ] Register in `Program.cs`: `builder.Services.AddScoped<IJobBoardClient, WellfoundClient>()`

**Acceptance criteria:** `WellfoundClient` authenticates and returns startup-focused job listings with `Source = Wellfound`.

---

### 4.4 GlassdoorClient or Google Jobs Fallback

**Files to create:**
- `src/JobScout.Infrastructure/ExternalServices/SerpApiGoogleJobsClient.cs` (fallback, preferred if Glassdoor partner access is unavailable)
- _or_ `src/JobScout.Infrastructure/ExternalServices/GlassdoorClient.cs` (if partner API approved)

**Files to modify:**
- `src/JobScout.Api/Program.cs` (DI registration)

**Requirements:**
- [ ] **Preferred path — Google Jobs via SerpAPI:** Create `SerpApiGoogleJobsClient : IJobBoardClient` with `Source => JobSource.Glassdoor`
  - Use `engine=google_jobs` SerpAPI parameter
  - Build query from `profile.SearchKeywords`
  - Map `google_jobs_listing` response fields to `Job`:
    - `job_id` → `ExternalId`
    - `title` → `Title`
    - `company_name` → `Company`
    - `location` → `Location`
    - `description` → `Description`
    - `detected_extensions.salary` → `SalaryRange`
    - `detected_extensions.schedule_type` → `JobType`
    - `detected_extensions.posted_at` → parse to `PostedAt`
    - `related_links[0].link` → `SourceUrl`
  - Reuse SerpAPI key from existing configuration
- [ ] **Alternative path — Glassdoor partner API:** If partner access is obtained, create `GlassdoorClient` using their REST API. Store `Glassdoor:PartnerId` and `Glassdoor:PartnerKey` in configuration
- [ ] Register whichever client is implemented in `Program.cs`

**Acceptance criteria:** Client returns jobs attributed to `JobSource.Glassdoor`. SerpAPI key config is shared with existing LinkedIn client.

---

### 4.5 Custom Source Support

**Files to create:**
- `src/JobScout.Core/Models/CustomJobSource.cs`
- `src/JobScout.Infrastructure/ExternalServices/CustomSourceClient.cs`
- EF Core migration: `AddCustomJobSource`

**Files to modify:**
- `src/JobScout.Infrastructure/Data/JobScoutDbContext.cs`
- `src/JobScout.Core/Enums/JobSource.cs`
- `src/JobScout.Api/Program.cs`

**Requirements:**
- [ ] Add `Custom` value to `JobSource` enum
- [ ] Create `CustomJobSource` entity:
  - `Guid Id`
  - `string Name` — display name for the source
  - `string FeedUrl` — the RSS/Atom or JSON endpoint URL
  - `FeedFormat Format` (new enum: `Rss`, `Atom`, `Json`)
  - `string? JsonJobsPath` — dot-notation path to the jobs array in a JSON response (e.g., `"results.jobs"`)
  - `string? JsonTitleField`, `JsonCompanyField`, `JsonLocationField`, `JsonDescriptionField`, `JsonUrlField`, `JsonPostedAtField` — field name mappings for JSON sources
  - `string ProfileId` — FK to `SearchProfile` (custom sources are per-profile)
  - `bool IsActive`
  - `DateTimeOffset CreatedAt`
- [ ] Register `DbSet<CustomJobSource>` in `JobScoutDbContext`
- [ ] Configure FK: `CustomJobSource.ProfileId` → `SearchProfile.Id` (cascade delete)
- [ ] Generate migration `AddCustomJobSource`
- [ ] Create `CustomSourceClient : IJobBoardClient`:
  - `Source => JobSource.Custom`
  - Accepts `IEnumerable<CustomJobSource>` via DI or fetches from repository
  - **RSS/Atom path:** Use `System.ServiceModel.Syndication.SyndicationFeed.Load()` — map `<item>` elements to `Job`
  - **JSON path:** Use `System.Text.Json` with `JsonPath`-style traversal to locate the jobs array, then map fields using the configured `JsonXxxField` properties
  - Set `ExternalId` = stable hash of `(Name + SourceUrl)` for feeds without explicit IDs
  - Filter active custom sources belonging to the active profile before fetching
- [ ] Add CRUD endpoints to an appropriate controller (or new `CustomSourcesController`):
  - `GET /api/custom-sources?profileId=`
  - `POST /api/custom-sources`
  - `DELETE /api/custom-sources/{id}`

**Acceptance criteria:** A user can add an RSS feed URL, and jobs from that feed appear in the job feed under `Source = Custom`. Migration applies cleanly.

---

### 4.6 Cross-Source Fuzzy Deduplication

**Files to create:**
- `src/JobScout.Infrastructure/Services/DeduplicationService.cs`
- `src/JobScout.Core/Interfaces/IDeduplicationService.cs`

**Files to modify:**
- `src/JobScout.Infrastructure/Services/JobIngestionService.cs`
- `src/JobScout.Infrastructure/Data/JobScoutDbContext.cs` (potential duplicate flag on `Job`)
- `src/JobScout.Core/Models/Job.cs`

**Requirements:**
- [ ] Add `bool IsPotentialDuplicate` and `Guid? DuplicateOfJobId` (nullable) fields to the `Job` entity
- [ ] Generate migration `AddDuplicateFields`
- [ ] Create `IDeduplicationService` with:
  - `NormalizeTitle(string title) → string` — lowercase, strip punctuation, collapse whitespace, remove common stopwords (Sr., Junior, Remote, etc.)
  - `NormalizeCompany(string company) → string` — lowercase, strip legal suffixes (Inc., LLC, Ltd.), collapse whitespace
  - `Task<Guid?> FindDuplicateAsync(Job candidate, Guid profileId)` — returns the ID of an existing job if a fuzzy match is found, otherwise null
- [ ] Fuzzy match criteria: `NormalizeTitle` match AND `NormalizeCompany` match AND same `Location` (normalized) across different `Source` values
- [ ] Integrate into `JobIngestionService`:
  - After the existing exact-match dedup check passes, run fuzzy duplicate check via `IDeduplicationService`
  - If fuzzy match found: set `IsPotentialDuplicate = true` and `DuplicateOfJobId = matchedJob.Id` on the incoming job, then save it (do not discard — let the user review)
  - Track fuzzy duplicate count separately in `IngestionResult`
- [ ] Register `IDeduplicationService` in `Program.cs`

**Acceptance criteria:** Jobs from two different sources with identical normalized titles and companies are flagged as potential duplicates. Exact-match deduplication still works unchanged.

---

### 4.7 Canonical Job Linking

**Files to modify:**
- `src/JobScout.Core/Models/Job.cs`
- `src/JobScout.Infrastructure/Data/JobScoutDbContext.cs`
- `src/JobScout.Api/Controllers/JobsController.cs`

**Requirements:**
- [ ] Add `ICollection<string> AlternateSourceUrls` to `Job`, stored as JSON — collects additional source URLs when duplicates are confirmed
- [ ] Generate migration `AddAlternateSourceUrls`
- [ ] Add endpoint `POST /api/jobs/{id}/confirm-duplicate` — body: `{ duplicateJobId: Guid }`:
  - Copies `duplicateJob.SourceUrl` into `primaryJob.AlternateSourceUrls`
  - Sets `duplicateJob.IsPotentialDuplicate = true` and `duplicateJob.DuplicateOfJobId = id`
  - Does not delete the secondary listing
- [ ] Add endpoint `POST /api/jobs/{id}/dismiss-duplicate`:
  - Clears `IsPotentialDuplicate` and `DuplicateOfJobId` on the specified job
- [ ] Expose `IsPotentialDuplicate` and `AlternateSourceUrls` in the `JobDto`

**Acceptance criteria:** Confirming a duplicate links the two records. The primary job's `AlternateSourceUrls` includes the secondary listing's URL. Dismissing clears the flag.

---

### 4.8 End-to-End Verification

- [ ] Solution builds: 0 errors, 0 warnings across all projects
- [ ] All migrations apply cleanly on a fresh database
- [ ] `SerpApiIndeedClient` returns jobs when triggered via manual ingestion; jobs appear with `Source = Indeed`
- [ ] `DiceClient` returns jobs; jobs appear with `Source = Dice`
- [ ] `WellfoundClient` authenticates and returns jobs with `Source = Wellfound`
- [ ] `SerpApiGoogleJobsClient` (or `GlassdoorClient`) returns jobs with `Source = Glassdoor`
- [ ] Custom RSS source: add a sample RSS feed URL, trigger ingestion, jobs appear with `Source = Custom`
- [ ] Custom JSON source: add a sample JSON endpoint with field mappings, jobs appear correctly mapped
- [ ] Fuzzy deduplication: ingest two sources for the same job posting — second ingestion sets `IsPotentialDuplicate = true`
- [ ] Exact deduplication still works — re-ingesting the same `(ExternalId, Source)` does not create a duplicate
- [ ] `POST /api/jobs/{id}/confirm-duplicate` links records and populates `AlternateSourceUrls`
- [ ] `POST /api/jobs/{id}/dismiss-duplicate` clears the flag
- [ ] AI scoring works for jobs from all new sources
- [ ] `JobSource` enum values for all new sources are serialized correctly in API responses

---

### 4.9 Commit & PR

- [ ] Stage all Phase 4 changes
- [ ] Commit with descriptive message
- [ ] Create PR targeting `main`
