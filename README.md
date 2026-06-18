# JobScout

> AI-powered job search aggregator — pulls listings from multiple job boards, scores them against your resume with Claude, and runs entirely on your own machine.

---

## Quickstart

1. **Install [.NET 10 SDK](https://dotnet.microsoft.com/download)**
2. **Clone this repo**
3. **Run the launcher:**
   - **Windows:** `.\start.ps1`
   - **macOS / Linux:** `./start.sh`

That's it. The launcher restores packages, applies the database schema, starts the API (which also serves the UI), and opens `http://localhost:5000` in your browser. On the very first run you'll be walked through a three-step setup wizard: account, optional API keys, and your first search profile.

> **Using VS Code?** Open the folder and press `F5`. The launch config builds and runs in one step.

---

## What it does

Job hunting means checking five different sites, reading dozens of descriptions, and remembering which ones matched your experience. JobScout automates the scanning and lets Claude do the first pass so you can focus on the roles that actually matter.

- **Aggregates listings** from RemoteOK, Adzuna, The Muse, LinkedIn / Indeed / Google Jobs (via SerpAPI), Dice, Wellfound, plus any RSS / JSON feed you add
- **Scores each role** against your resume using the Claude API — overall 1–10 fit plus skills / experience / culture / compensation sub-scores
- **Learns your taste** — rate jobs 1–5 stars and the AI recalibrates future scoring to match your judgment
- **Tracks applications** — built-in Kanban (Applied → Interviewing → Offered → Accepted)
- **Notifies you** — bell-icon dropdown for new strong fits, ingestion summaries, and status changes; optional daily / weekly email digests
- **Supports multiple profiles** — run a "Senior Backend Roles" search and a "Data Science" search in parallel

---

## What you'll need

Only one thing is strictly required:

- **.NET 10 SDK** — [download](https://dotnet.microsoft.com/download)

Everything else is optional and can be configured later from the Settings page:

| Service | Why you'd want it |
|---|---|
| **Anthropic API key** | Powers AI scoring. Without it, every job gets a placeholder score and the AI features are inert. [Get one](https://console.anthropic.com/) |
| **SerpAPI key** | Unlocks LinkedIn, Indeed, and Google Jobs sources. [Get one](https://serpapi.com/) |
| **Adzuna credentials** | Unlocks the Adzuna source (app id + app key). [Get one](https://developer.adzuna.com/) |
| **SendGrid key** | Enables email digests. The in-app notifications work without it. [Get one](https://signup.sendgrid.com/) |

No service is required to get to a working UI — you can run the app with zero keys configured and use it as a notes-and-tracking tool, then add keys later as you decide which sources to enable.

---

## How it works

```
┌──────────────────────────────────────────────────────┐
│                   localhost:5000                     │
│                                                      │
│  ┌─────────────────┐   ┌──────────────────────────┐  │
│  │ Blazor WASM UI  │   │ ASP.NET Core API + auth  │  │
│  └─────────────────┘   └──────────────────────────┘  │
│           ▲                       │                  │
│           │ same-origin           ▼                  │
│           │              ┌─────────────────────┐     │
│           └──────────────│ SQLite + EF Core    │     │
│                          └─────────────────────┘     │
│                                   ▲                  │
│                                   │                  │
│         ┌─────────────────────────┴─────────────┐    │
│         │ BackgroundServices                    │    │
│         │   • Job ingestion every 4 hours       │    │
│         │   • Daily digest 13:00 UTC            │    │
│         │   • Weekly summary Mondays 14:00 UTC  │    │
│         └───────────────────────────────────────┘    │
└──────────────────────────────────────────────────────┘
```

One process, one port. No Azure, no Docker, no separate scheduler.

---

## Where your data lives

| What | Where |
|---|---|
| **Database (SQLite)** | `~/.jobscout/jobscout.db` (Unix) / `%LOCALAPPDATA%\JobScout\jobscout.db` (Windows) |
| **API keys & secrets** | Encrypted in the same `jobscout.db` file using ASP.NET Data Protection |
| **Encryption key ring** | `~/.jobscout/dpapi-keys/` |
| **JWT signing key** | `~/.jobscout/local.json` (auto-generated on first run) |

Everything lives on your machine. To completely uninstall, delete the JobScout folder above.

---

## Project layout

```
JobScout/
├── src/
│   ├── JobScout.Core/             # Domain models, DTOs, enums, interfaces
│   ├── JobScout.Infrastructure/   # EF, job board clients, scoring, schedulers
│   ├── JobScout.Api/              # ASP.NET Core API — also hosts the Blazor UI
│   └── JobScout.Web/              # Blazor WebAssembly client
├── tests/                         # xUnit + bUnit suites (run with `dotnet test`)
└── start.sh / start.ps1           # Cross-platform launcher scripts
```

---

## Developing

```bash
dotnet build       # build everything
dotnet test        # run the full test suite (~5 seconds, 93 tests)
dotnet watch run --project src/JobScout.Api    # hot-reload during development
```

See [DEVELOPING.md](DEVELOPING.md) for details on adding new migrations, registering new job board clients, and writing tests.

---

## License

[MIT](LICENSE)
