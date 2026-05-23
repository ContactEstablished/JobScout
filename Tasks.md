# Phase 2: Profile Management Enhancements

**Priority: HIGH** | **Branch:** `phase2/profile-management`

> Reference: [Roadmap.md](Roadmap.md) — Phase 2 (sections 2.1 through 2.5)

---

## Status Overview

| # | Task | Status |
|---|------|--------|
| 2.1 | SearchProfile schema expansion + migration | DONE |
| 2.2 | Update DTOs and API contracts | DONE |
| 2.3 | Job board clients use profile keywords | DONE |
| 2.4 | Ingestion service respects PreferredSources | DONE |
| 2.5 | Multi-step profile creation wizard | DONE |
| 2.6 | Resume skill persistence + manual editing | DONE |
| 2.7 | LinkedIn PDF import workflow | DONE |
| 2.8 | Profile clone endpoint + UI | DONE |
| 2.9 | End-to-end verification | DONE |
| 2.10 | Commit & PR | DONE |

---

## Completed Tasks

### 2.1 SearchProfile Schema Expansion + Migration

- [x] Added 7 new properties to `SearchProfile`: `SearchKeywords`, `PreferredSources`, `PreferredJobTypes`, `PreferredLocationTypes`, `LocationPreference`, `DetectedSkills`, `ProfileColor`
- [x] JSON column serialization in `JobScoutDbContext` with `ValueComparer` for all list properties
- [x] EF Core migration `AddProfileSearchPreferences` generated and applied cleanly

### 2.2 Update DTOs and API Contracts

- [x] `SearchProfileDto` — added all 7 Phase 2 fields
- [x] `CreateProfileRequest` — added keywords, sources, job types, location types, location, color
- [x] `UpdateProfileRequest` — added same Phase 2 fields
- [x] `UpdateSkillsRequest` — new DTO for skill editing
- [x] `MappingExtensions.ToDto()` — maps all new fields
- [x] `ProfilesController.Create` — maps new fields from request
- [x] `ProfilesController.Update` — maps new fields from request
- [x] `PUT /profiles/{id}/skills` — new endpoint for skill updates
- [x] `POST /profiles/{id}/clone` — new endpoint for deep-copy
- [x] `JsonStringEnumConverter` added to API JSON options

### 2.3 Job Board Clients Use Profile Keywords

- [x] Added `Source` property to `IJobBoardClient` interface
- [x] `AdzunaClient` — uses `profile.SearchKeywords` when non-empty
- [x] `SerpApiLinkedInClient` — uses `profile.SearchKeywords` + `profile.LocationPreference`
- [x] `TheMuseClient` — uses `profile.SearchKeywords` as category list
- [x] `RemoteOkClient` — uses `profile.SearchKeywords` for tag filtering

### 2.4 Ingestion Service Respects PreferredSources

- [x] `JobIngestionService` filters `IJobBoardClient` instances by `profile.PreferredSources`
- [x] Empty sources list = use all clients (backward compatible)

### 2.5 Multi-Step Profile Creation Wizard

- [x] 4-step wizard: Identity → Resume & Skills → Search Preferences → Job Boards
- [x] Step 1: Name, description, LinkedIn URL, profile color picker
- [x] Step 2: Resume/LinkedIn PDF upload, detected skills with add/remove
- [x] Step 3: Search keywords (comma-separated), location, job types, location types
- [x] Step 4: Job board source selection with "coming soon" for unimplemented sources
- [x] Wizard step navigation with numbered indicators
- [x] Edit mode pre-populates all fields from existing profile
- [x] CSS: wizard steps, color picker, chips, checkbox groups

### 2.6 Resume Skill Persistence + Manual Editing

- [x] `UploadResume` endpoint auto-persists `DetectedSkills` from `ResumeParser`
- [x] Wizard Step 2 shows detected skills with remove buttons
- [x] Manual skill add via text input + Enter key
- [x] `PUT /profiles/{id}/skills` endpoint for standalone updates

### 2.7 LinkedIn PDF Import Workflow

- [x] Resume upload accepts PDF (already supported by `ResumeParser`)
- [x] Wizard Step 2 updated with LinkedIn-specific UX guidance
- [x] Instructions for LinkedIn data export path

### 2.8 Profile Clone Endpoint + UI

- [x] `POST /profiles/{id}/clone` — deep copies name (with "(Copy)" suffix), description, resume, keywords, sources, job types, location types, location, skills, color
- [x] Does NOT copy scores, ratings, or metrics
- [x] Clone button added to profile cards in wizard UI

### 2.9 End-to-End Verification

- [x] Solution builds: 0 errors, 0 warnings across all 5 projects
- [x] Migration applies cleanly
- [x] Create profile with Phase 2 fields — all round-trip correctly
- [x] Update profile — modified fields persist
- [x] Update skills endpoint works
- [x] Clone profile — deep copies all fields
- [x] Get all profiles — correct count
- [x] JSON enum serialization (string ↔ enum) works

### 2.10 Commit & PR

- [x] PR #9: https://github.com/ContactEstablished/JobScout/pull/9
