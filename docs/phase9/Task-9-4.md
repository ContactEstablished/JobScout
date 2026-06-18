# Task 9.4 — Search & Filtering Improvements

> **Roadmap:** §9.4 (Phase 9 — UX Polish & Accessibility)
> **Execution order:** 1st (highest daily-use payoff)
> **Status:** ✅ Scoped & spec approved — ready to build
> **Implementation spec:** [`Task-9-4-spec.md`](Task-9-4-spec.md)

---

## Goal

Make the job feed stay usable as it grows to hundreds of listings, by letting the user search the
full text of jobs, sort the feed deliberately, save filter combinations they reuse, and have the
current view survive a page refresh.

## Why

This is the single highest-value Phase 9 segment for a local-first daily driver: it's touched on
every visit. Today, search only matches job titles + company, the feed has a single fixed ordering,
and a refresh wipes the current filters.

---

## In scope

### 9.4.a — Full-text search
Search across **title, company, description, tags, and the profile's AI reasoning** — not just title.

- **Acceptance:** A query matching only a job's description (not its title) returns that job.
- **Acceptance:** A query matching only the AI reasoning text returns that job.
- **Acceptance:** Search is case-insensitive and matches partial words.
- **Acceptance:** Search composes with active filters (source, score, location, etc.) — it narrows
  within the current filter set rather than replacing it.

### 9.4.b — Sort-by dropdown
A sort control on the feed header.

- **Acceptance:** Options are **AI Score (default), Posted Date, Company Name**. *(Salary sort is
  intentionally excluded — salary fit is handled by the AI `compensationFit` sub-score, not sorting.)*
- **Acceptance:** Sort selection persists across pagination / "load more".
- **Acceptance:** `Posted Date` sorts by `PostedAt`, falling back to `DiscoveredAt` when null; ordering
  is deterministic via a stable tie-breaker.

### 9.4.c — Saved filter presets (per profile)
Name and recall a filter+search+sort combination, scoped to a search profile.

- **Acceptance:** The user can save the current filter/search/sort state under a name.
- **Acceptance:** Selecting a saved preset restores that exact state.
- **Acceptance:** Presets belong to the **active profile** and survive a restart.
- **Acceptance:** The user can rename and delete a preset; names are unique within a profile.

### 9.4.d — Filter-state refresh-persistence
The current (unsaved) filter/search/sort view survives a page refresh.

- **Acceptance:** After setting filters and pressing F5, the same view is restored.
- **Acceptance:** Implemented with `localStorage` (existing `IJSRuntime` interop pattern); malformed
  or absent state degrades gracefully to defaults.

---

## Out of scope (this task)

- **Freshness / recency ranking** (giving fresher jobs a score bump) — that's **`Task 9.6`**.
- **Numeric salary sorting** — salary fit is an AI concern (`compensationFit`), not a sort key.
- Fuzzy / typo-tolerant search and cross-source de-duplication (Phase 4 topics).
- Server-side full-text *index* (FTS5) — plain `LIKE`/`Contains` is fine at local-first volumes.
- URL-encoded / shareable view state — `localStorage` (9.4.d) covers refresh-persistence instead.
- Mobile-specific layout of the new controls (lands with §9.1, deferred).

---

## Likely affected areas

*(Made exact in [`Task-9-4-spec.md`](Task-9-4-spec.md).)*

- **API:** the jobs query endpoint (`sort` param) + a new `FilterPresets` controller.
- **Infrastructure:** the job repository query builder (search predicate + ordering); a `FilterPreset`
  entity + EF migration; a preset repository.
- **Core:** `JobSortBy` enum; preset DTOs/model/interface.
- **Web:** the job feed page, search input + sort dropdown, the filter panel, presets UI, the typed
  `JobsService` / new `FilterPresetsService`, and `localStorage` persistence in `FilterStateService`.

## Open questions

All resolved during spec review — see [`Task-9-4-spec.md`](Task-9-4-spec.md) §0 (salary dropped;
freshness → Task 9.6; presets per-profile; `LIKE` over FTS5; `localStorage` over URL state).

---

## Definition of done

- [ ] All acceptance criteria above pass.
- [ ] New/changed behavior covered by tests (repository query + API + a bUnit component test).
- [ ] `dotnet build` clean (0 warnings, 0 errors); full suite green.
- [ ] No regression to existing filter behavior.
