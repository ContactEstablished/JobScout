# Phase 5: AI Scoring & Intelligence Enhancements

**Priority: MEDIUM** | **Branch:** `phase5/ai-scoring-enhancements`

> Reference: [Roadmap.md](Roadmap.md) — Phase 5 (sections 5.1 through 5.3)

---

## Status Overview

| # | Task | Status |
|---|------|--------|
| 5.1 | Extend `SearchProfile` with `PreferredModel`, `DesiredSalaryMin`, `DesiredSalaryMax` | DONE |
| 5.2 | Extend `AiScore` with sub-scores, token usage, growth areas, cost | DONE |
| 5.3 | Migrate `ClaudeAiScoringService` to `Anthropic.SDK` with `tool_use` | DONE |
| 5.4 | Multi-dimensional scoring with salary + resume-gap analysis | DONE |
| 5.5 | Rating-informed few-shot examples in scoring prompt | DONE |
| 5.6 | Per-profile model selection support | DONE |
| 5.7 | Auto-recalibration trigger in `RatingsController` (every 20 ratings) | DONE |
| 5.8 | AI cost tracking endpoint + dashboard integration | DONE |
| 5.9 | EF migration `AddAiScoringEnhancements` + 0-error/0-warning build | DONE |
| 5.10 | Commit, push, open PR targeting `main` | DONE |

---

## Task Details

### 5.1 SearchProfile scoring preferences

- [x] Added `PreferredModel` (string?), `DesiredSalaryMin` (decimal?), `DesiredSalaryMax` (decimal?) to `SearchProfile`
- [x] Mirrored fields on `SearchProfileDto`, `CreateProfileRequest`, `UpdateProfileRequest`
- [x] Updated `ProfilesController` (Create, Update, Clone) and `MappingExtensions.ToDto` to round-trip the new fields

### 5.2 AiScore sub-scores, growth areas, cost

- [x] Added `SkillsMatchScore`, `ExperienceFitScore`, `CultureFitScore`, `CompensationFitScore` (nullable decimal)
- [x] Added `GrowthAreas`, `RedFlags` (JSON strings)
- [x] Added `InputTokens`, `OutputTokens`, `EstimatedCostUsd` for cost tracking
- [x] Mirrored fields on `AiScoreDto` and `MappingExtensions.ToDto(AiScore)`
- [x] Configured EF column types and precision in `JobScoutDbContext`

### 5.3 Anthropic.SDK + tool_use

- [x] Removed raw `HttpClient` / JSON regex parsing
- [x] `ClaudeAiScoringService` now uses `Anthropic.SDK.AnthropicClient` with `MessageParameters`
- [x] Forced structured output via `ToolChoice { Type = Tool, Name = "submit_job_match_score" }`
- [x] Reads `ToolUseContent.Input` (`JsonNode`) — no more freeform parsing
- [x] `IAiScoringService` signatures unchanged

### 5.4 Multi-dimensional scoring

- [x] Tool schema requires `score`, `skillsMatch`, `experienceFit`, `cultureFit`, `compensationFit`, `reasoning`
- [x] Optional `matchedKeywords`, `growthAreas`, `redFlags` arrays
- [x] System prompt instructs the model to compare posted salary against `DesiredSalaryMin/Max` when present
- [x] `growthAreas` captures resume-gap skills as positive opportunities rather than red flags

### 5.5 Rating-informed few-shot

- [x] On every scoring call, the service pulls the candidate's last 10 `UserRating`s (with `Include(r => r.Job)`)
- [x] Examples (title, company, stars, notes) are embedded in the system prompt under a "CALIBRATION" header
- [x] Examples are fetched once per `BatchScoreAsync` invocation to avoid per-job DB hits

### 5.6 Per-profile model selection

- [x] `ResolveModel(profile)` picks `profile.PreferredModel` → `config["Anthropic:Model"]` → `claude-haiku-4-5-20251001`
- [x] Selected model id is recorded on `AiScore.ModelVersion`
- [x] Cost is calculated via `MessageResponse.CalculateCost()` using SDK-built-in pricing when available

### 5.7 Auto-recalibration trigger

- [x] After saving each new rating in `RatingsController.Create`, the controller re-counts ratings for the profile
- [x] When count is a positive multiple of 20, fires `RecalibrateAsync(profileId, resetHistory: false)` in a background `Task.Run` with a fresh DI scope
- [x] Errors during background recalibration are logged but do not affect the client's response

### 5.8 Cost endpoint + dashboard

- [x] New DTOs: `AiCostSummaryDto`, `AiCostByModelDto`
- [x] `IMetricsService.GetAiCostSummaryAsync(Guid? profileId)` aggregates scores by model with totals
- [x] `GET /api/metrics/ai-costs?profileId=` returns the summary (omit `profileId` for org-wide totals)
- [x] `DashboardStatsDto` gains `AiCostUsdThisWeek` and `AiCostUsdAllTime`, populated by `MetricsService.GetDashboardStatsAsync`

### 5.9 Migration + build verification

- [x] Generated `AddAiScoringEnhancements` migration covering all new columns
- [x] Applied with `dotnet ef database update`
- [x] Full solution build: `0 Warning(s)`, `0 Error(s)`

### 5.10 Commit & PR

- [x] Branched from `main` as `phase5/ai-scoring-enhancements`
- [x] Single Phase 5 commit + push
- [x] PR opened against `main`
