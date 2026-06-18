# Task 9.6 — Freshness-Aware Ranking & Job-Age Visibility

> **Roadmap:** §9.6 (Phase 9 — UX Polish & Accessibility) — *new segment added during 9.4 review.*
> **Execution order:** 2nd (right after 9.4 — it refines the default AI-Score ranking 9.4 introduces)
> **Status:** 📋 Scoped — implementation spec not yet written
> **Implementation spec:** `Task-9-6-spec.md` *(written on request, after this scope is approved)*
> **Depends on:** `Task-9-4` (the sort-by-AI-Score ordering this task adjusts)

---

## Goal

Bias the feed toward **fresher jobs** so the user applies early, and make a job's **age highly
visible**. A strong-but-older job should rank below a slightly-weaker brand-new one, because for many
roles "being early matters" — recruiters can't read hundreds of applications per posting, so an
8.5 posted today is often worth more than a 9.1 posted two days ago.

## Why

Application timing materially affects outcomes due to volume. The AI score measures *fit*; it says
nothing about *urgency*. This task adds urgency as a ranking signal without corrupting the AI's
quality judgment.

---

## In scope

### 9.6.a — Freshness-adjusted ranking
The default "AI Score" feed order ranks by an **effective score = raw AI score + freshness bonus**.

- **Acceptance:** Given an 8.5 posted today and a 9.1 posted 2 days ago, the 8.5 ranks higher
  (with the default tuning).
- **Acceptance:** The freshness bonus **decays with age** and reaches zero after a configurable
  window (starting point: bonus `B·max(0, 1 − ageDays/W)`, with `B`/`W` configurable; ~`B≈0.8–1.0`,
  `W≈3` days as a first cut, tuned against the example above).
- **Acceptance:** The bonus is computed **dynamically at query time** from `PostedAt`
  (fallback `DiscoveredAt`) — never stored on `AiScore` (it changes daily).
- **Acceptance:** The **displayed** AI score remains the raw score; only the *ordering* changes.
- **Acceptance:** Only affects the default (AI Score) sort; `Posted Date` / `Company` sorts unchanged.

### 9.6.b — Job-age visibility
Make recency obvious on the job card.

- **Acceptance:** Each card shows a clear posted-age indicator (e.g. "Posted today", "2 days ago"),
  with fresh jobs (≤24–48h) visually emphasized.
- **Acceptance:** Jobs with no `PostedAt` fall back to `DiscoveredAt` with appropriate wording
  ("Discovered today").

---

## Out of scope (this task)

- Per-profile tuning of the decay curve (global config is enough for v1).
- Surfacing the freshness bonus as a number in the UI (keep the displayed score = raw AI score).
- Re-scoring or any change to how the AI produces scores.

---

## Likely affected areas

*(To be confirmed in the implementation spec.)*

- **Infrastructure:** `JobRepository.GetByProfileAsync` — the `AiScore` ordering arm becomes an
  effective-score ordering (the §2.2 hook noted in `Task-9-4-spec.md`).
- **Core / config:** a freshness-tuning option (`B`, `W`) via the Options/config pattern.
- **Web:** `JobCard.razor` posted-age badge + emphasis styling.

## Open questions for the spec

1. Can the effective-score ordering be expressed in EF (correlated date math in `OrderBy`), or do we
   compute it in-memory per page (and what does that mean for pagination correctness)?
2. Exact decay shape — linear ramp vs exponential — and the default `B`/`W` values to ship.
3. "Fresh" threshold for the visual emphasis (24h? 48h?).

---

## Definition of done

- [ ] All acceptance criteria pass; the calibration example holds with shipped defaults.
- [ ] Ranking change covered by repository tests; age formatting covered by a component test.
- [ ] `dotnet build` clean; full suite green.
