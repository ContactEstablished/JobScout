# JobScout Development Roadmap
**Version 2.0 | Revised 17 June 2026 | Contact Established**

> **What changed in v2.0:** Phases 1–8 are complete and merged. The original Phase 8
> ("Production Deployment & Infrastructure" on Azure) was **deliberately superseded** by a
> **local-first** pivot — JobScout now runs entirely on the user's own machine, no cloud. This
> document has been rewritten to reflect the shipped state. The only remaining planned phase is
> **Phase 9 (UX Polish & Accessibility)**; work beyond Phase 9 is intentionally left open pending
> a full code review.

---

## Executive Summary

JobScout is an AI-powered job search aggregator that pulls listings from multiple job boards,
scores them against a user's resume using Claude, and presents a curated, ranked feed through a
Blazor WebAssembly frontend. It has grown from an MVP into a feature-complete, **local-first**
.NET 10 application: authentication, multi-profile management, application tracking, seven job
board integrations, multi-dimensional AI scoring, in-app and email notifications, and a full test
suite are all shipped.

The app runs as **a single local process** — one ASP.NET Core host serves the API, the Blazor
WASM client, and the scheduled background jobs, backed by a local SQLite database. There is no
Azure, no Docker, and no separate scheduler.

---

## Where We Are

### Shipped — Phases 1–8 (merged to `main`)

| Phase | Theme | Status |
|---|---|---|
| **1. Authentication & User Accounts** | ASP.NET Core Identity + JWT, per-user profile scoping, login/register UI | ✅ Done |
| **2. Profile Management** | Multi-profile workflow, per-profile search keywords & preferred sources | ✅ Done |
| **3. Application Tracking** | `JobApplication` pipeline + Kanban UI (Applied → Interviewing → Offered → Accepted/Rejected) | ✅ Done |
| **4. Job Board Expansion** | RemoteOK, Adzuna, The Muse, plus SerpAPI LinkedIn / Indeed / Google Jobs, Dice, Wellfound | ✅ Done |
| **5. AI Scoring Enhancements** | Structured Claude output, multi-dimensional sub-scores, rating-informed recalibration | ✅ Done |
| **6. Notifications & Alerts** | In-app bell dropdown + optional email digests (SendGrid) | ✅ Done |
| **7. Testing Strategy** | xUnit + bUnit across Core / Infrastructure / Api / Web (~93 tests, green) | ✅ Done |
| **8. Local-First Setup** | **Replaced the original Azure plan** — see below | ✅ Done |

> Each phase corresponds to a merged pull request (`#7`–`#15`). Refer to git history for the
> per-commit detail of any phase.

### Architecture (current)

```
┌──────────────────────── localhost:5000 ────────────────────────┐
│  Blazor WASM UI  ──same-origin──►  ASP.NET Core API + JWT auth   │
│                                          │                       │
│                                          ▼                       │
│                                  SQLite + EF Core 10             │
│                                          ▲                       │
│                       BackgroundServices │                       │
│                         • Job ingestion every 4h                 │
│                         • Daily digest / weekly summary          │
└──────────────────────────────────────────────────────────────────┘
```

- **One process, one port.** The API hosts the Blazor client and runs the schedulers in-process.
- **Local data** lives in `~/.jobscout` (Unix) / `%LOCALAPPDATA%\JobScout` (Windows): the SQLite
  database, the Data Protection key ring (encrypts API keys at rest), and the JWT signing key.
- **EF migrations auto-apply on startup** in every environment, so non-developers never run CLI tools.
- **Run it** with `./start.sh` / `.\start.ps1`, or `F5` in VS Code.

### Phase 8 — the local-first pivot (context)

The original roadmap's Phase 8 was *"Production Deployment & Infrastructure"*: Azure App Service,
Azure SQL, an Azure Function App, Key Vault, Bicep IaC, and GitHub Actions CD. That direction was
**deliberately replaced** on 17 June 2026 with a local-first build (PR #15), which:

- Deleted the `JobScout.Functions` Azure Functions project; scheduling moved to in-process
  `BackgroundService`s in the API.
- Hosts the Blazor WASM client from the API (single origin — no CORS in production).
- Stores secrets encrypted in the database via the Data Protection API (no Key Vault).
- Added a first-run setup wizard, sensible config defaults, and cross-platform launcher scripts.

**Azure / hosted deployment is out of scope** unless the project later chooses to offer a hosted
edition. If that happens, the old Azure plan is preserved in git history (`Roadmap.md` v1.0).

---

## Phase 9 — UX Polish & Accessibility (remaining planned work)

**Priority: MEDIUM** | Estimated effort: **1.5–2 weeks** | Dependencies: none (can start anytime)

The UI is functional and visually polished, but several areas need refinement for daily-driver
quality. Phase 9 is broken into five segments. Because the app is now **local-first** (reachable
only at `localhost`), the value ranking differs from v1.0 — see the recommended order below.

### Recommended execution order

1. **§9.4 Search & Filtering** — highest daily-use payoff; the feed must stay usable at scale.
2. **§9.6 Freshness-aware ranking** — refines the AI-Score ordering 9.4 introduces; apply-early bias.
3. **§9.3 Data Export (CSV)** — "own your data" win that fits the local-first ethos.
4. **§9.5 Light Theme** — cheap, pure preference.
5. **§9.2 Accessibility** — quality-of-life (keyboard nav, reduced-motion) for a single-user tool.
6. **§9.1 Mobile Responsiveness** — **deferred.** Lower value while the app is `localhost`-only and
   unreachable from a phone without extra LAN/tunnel setup.

> Each segment is planned as its own task file under `docs/phase9/` (`Task-9-1.md` … `Task-9-6.md`,
> numbered by roadmap section). A task file captures **scope, acceptance criteria, and affected
> areas**; the **detailed implementation spec** for each is written separately, on demand, as
> `Task-9-N-spec.md`. §9.6 was added during the §9.4 spec review.

### 9.1 Mobile Responsiveness *(deferred — see note above)*

- Responsive audit at mobile (375px), tablet (768px), desktop (1200px+) widths.
- Convert the left sidebar to a hamburger-triggered drawer on mobile.
- Ensure the slide-in filter panel works on touch without horizontal scroll.
- Stack job-card metadata vertically on mobile; ensure tap targets ≥ 44×44px.

### 9.2 Accessibility (WCAG 2.1 AA)

- Verify all text meets a 4.5:1 contrast ratio against the dark background.
- Ensure every interactive element is keyboard-focusable and operable, with visible focus rings.
- Add ARIA labels to the score ring, star rating, filter-panel toggle, and profile selector.
- Respect `prefers-reduced-motion` for animations and transitions.

### 9.3 Data Export

- **CSV export** of the current job feed (with active filters applied) for spreadsheet analysis.
- **PDF profile report** (lower priority): top-scored jobs, application pipeline, trends, and AI
  insights, generated with QuestPDF (MIT-licensed).

### 9.4 Search & Filtering Improvements

- **Full-text search** across descriptions, company names, tags, and AI reasoning — not just titles.
- **Saved filter presets** — name and quickly switch between filter combinations (e.g. "Remote Senior 8+").
- **Sort-by dropdown** — AI Score (default), Posted Date, Company Name, Salary.

### 9.5 Light Theme

- Add a light-mode set of values for the existing CSS custom properties.
- Add a sun/moon toggle to the TopBar; persist the choice in `localStorage`.
- Default to the user's OS-level `prefers-color-scheme`.

### 9.6 Freshness-Aware Ranking & Job-Age Visibility *(added during the §9.4 review)*

Bias the default AI-Score feed order toward fresher jobs (apply-early advantage) and make job age
highly visible. The displayed AI score stays the raw fit score; only the ordering changes, via an
**effective score = raw AI score + a freshness bonus that decays to zero over a few days** — computed
dynamically at query time, never stored. Example target: an 8.5 posted today outranks a 9.1 posted
two days ago. See [`docs/phase9/Task-9-6.md`](docs/phase9/Task-9-6.md).

### Phase 9 deliverables

| Work Item | Segment | Priority | Estimate |
|---|---|---|---|
| Full-text search expansion | 9.4 | High | 1 day |
| Sort-by dropdown (AI Score / Posted / Company) | 9.4 | Medium | 0.5 days |
| Saved filter presets (per profile) | 9.4 | Low | 1 day |
| Filter-state localStorage persistence | 9.4 | Low | 0.5 days |
| Freshness-adjusted ranking | 9.6 | Medium | 1 day |
| Job-age visibility on cards | 9.6 | Low | 0.5 days |
| CSV export for job feed | 9.3 | Medium | 1 day |
| PDF profile report | 9.3 | Low | 2 days |
| Light theme + toggle | 9.5 | Low | 1.5 days |
| WCAG 2.1 AA accessibility pass | 9.2 | Medium | 2 days |
| Mobile responsive audit + fixes | 9.1 | Low *(deferred)* | 2 days |
| Sidebar drawer for mobile | 9.1 | Low *(deferred)* | 1 day |

---

## Beyond Phase 9

Intentionally undefined. The next block of work will be scoped **after a full review of the
current codebase**. Candidate themes (recorded here as ideas, **not commitments**):

- **Resilience** — Polly retry / circuit-breaker / timeout policies around job-board and Claude
  API calls (`Microsoft.Extensions.Http.Resilience`). Still outstanding from the original tech-debt list.
- **Packaging & distribution** — single-file publish, an installer, or a tray app, so non-developers
  can run JobScout without the .NET SDK.
- **Data portability** — export/import of the whole local dataset; optional sync between machines.
- **Scoring depth** — per-profile model selection, cost tracking, resume gap analysis.

---

## Architectural Notes

### Technical debt — status

- **Hard-coded search terms** → addressed in Phase 2 (clients use profile keywords). *(verify against current code)*
- **Structured AI output** → addressed in Phase 5 (Claude structured output rather than regex parsing). *(verify)*
- **No retry/resilience** → **still open.** No Polly policies on outbound HTTP. Good candidate for post-Phase-9.
- **JSON serialization in entities** → `Tags` / `MatchedKeywords` stored as JSON strings; EF Core 10
  supports native JSON column mapping. *(likely still open — verify)*
- **Concurrency control** → no optimistic concurrency tokens on `SearchProfile` / `Job`. *(likely still open — verify)*

### Technology choices (still current)

- **Auth:** ASP.NET Core Identity + JWT — keeps the dependency graph simple and cost at zero.
- **Database:** SQLite for the local-first model. (Azure SQL is no longer in scope.)
- **Email:** SendGrid free tier (100 emails/day) for digests; in-app notifications work without it.
- **PDF generation (for §9.3):** QuestPDF (MIT) — fluent API, easy to maintain.

---

## Status Summary

| Phase | Priority | Estimate | Status |
|---|---|---|---|
| 1. Authentication & User Accounts | Critical | — | ✅ Done |
| 2. Profile Management | High | — | ✅ Done |
| 3. Application Tracking | High | — | ✅ Done |
| 4. Job Board Expansion | Medium | — | ✅ Done |
| 5. AI Scoring Enhancements | Medium | — | ✅ Done |
| 6. Notifications & Alerts | Medium | — | ✅ Done |
| 7. Testing Strategy | High | — | ✅ Done |
| 8. Local-First Setup | High | — | ✅ Done (replaced Azure plan) |
| **9. UX Polish & Accessibility** | **Medium** | **1.5–2 weeks** | ⏳ **Planned (next)** |
| Beyond Phase 9 | TBD | TBD | 🔭 To be scoped after code review |
