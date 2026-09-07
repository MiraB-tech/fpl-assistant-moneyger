# The Assistant Moneyger — CLAUDE.md

A copilot (not autopilot) for Fantasy Premier League squad decisions. Full context: `doc/proposal.md` (locked Session 0 output) and `doc/solo_tri_sesh.pdf` (build methodology).

## Build methodology: Solo Tri-Sesh

Workload-bound, not time-boxed. Four sessions total:

| Session | Focus | Deliverable | Status |
|---|---|---|---|
| 0 | Scope, research, tech decisions | `doc/proposal.md` | ✅ done |
| 1 | Data & Prediction Engine — pipeline, features, xP formula v1, eval skeleton | working pipeline → real `gw{N}_predictions.json` | ✅ done |
| 2 | Frontend Build — React+TS, squad view, XI, transfers | working local frontend on real data | ✅ done |
| 3 | Integration, testing, Vercel deploy | live public URL | ✅ done — https://fpl-assistant-moneyger.vercel.app (still running the v1, single-user build — see below) |

Rules: once a session starts it runs to completion (short breaks fine, long breaks only *between* sessions). A session ends when its deliverable is real and working, not on a clock. Don't start Session 2 work while Session 1's deliverable isn't real yet, and vice versa.

Note: the original target was live before the Fri 21 Aug 2026 GW1 deadline; the build ran behind that (GW1/GW2 had already started before Session 1 closed out). From partway through Session 2 onward, the user prioritized shipping speed over the learning goal — see the working-style note below.

## v2: multi-user platform rewrite (post-Session-3, in progress)

The Solo Tri-Sesh sessions above delivered a real, working **single-user** v1 (personal use only, one hardcoded FPL team, static JSON files, no accounts). The user then decided to pilot the app with multiple people, which needed real accounts, a database, and the pipeline exposed as something a button click can trigger — none of which v1's architecture supports. This is being built as its own phase, not shoehorned into the session table above.

**A first attempt used Python/Flask + Vercel serverless + Neon (serverless Postgres).** It hit real friction — Neon's cold-start latency was extremely slow/flaky from local dev, and a Vercel Python packaging bug blocked the first deploy. The user then made a deliberate, informed decision (explicitly overruling a counter-recommendation to just fix those two bugs) to rewrite the backend from scratch:

- **C#/.NET (ASP.NET Core Web API, .NET 10), Vertical Slice Architecture** (organize by feature/use-case, not technical layer)
- **ASP.NET Core Identity** for auth — free, built into .NET, no external account needed
- **Docker Compose + local Postgres** for development (no more cloud-DB cold-starts while iterating)
- Everything the Python pipeline did (FPL data pulling, the xP formula, evaluation) is ported into the new backend

This lives entirely in `backend/` (see layout below) and is a clean rewrite, not an extension of the abandoned Flask attempt. As of this writing it works end-to-end locally (register/login, squad, Best XI, transfers, refresh-predictions button, all verified against the real frontend through a Vite dev proxy) but **is not yet deployed anywhere** — production hosting is an explicit open decision (not Vercel, since it doesn't run long-lived containers/processes the way this backend needs). The live Vercel URL above is still serving the old v1 single-user build until this is deployed and cut over.

## Working style — this is a learning project

The user is learning as they build, not just shipping. This changes how Claude should work here vs. a typical repo:

- **No throwaway inline probing.** Don't explore an API/library shape with a one-off `python -c "..."` command. Write a real script file, run it, keep it (or fold it straight into the real module it was exploring for). The exploration itself is part of what the user is learning from.
- **Sequential, one step at a time.** Build and explain one piece, pause, let the user look at it, before moving to the next. Don't chain several build steps together unprompted.
- **Comment code in plain, non-technical language.** Comments should explain *what's happening and why* in terms a non-engineer could follow, not just restate the code. This is the opposite of the terse/no-comments default — deliberate, for this project.
- **Tutor mode — Claude explains, the user writes.** Claude acts as an advanced tutor/textbook across every language involved: introduce the piece being built and why, then dictate what to write file-by-file, and within a file, piece-by-piece (imports, constants, then each method one at a time with its purpose). Pause after each piece for the user to write it themselves and report back before continuing. Claude does not use Write/Edit to author the substantive learning code itself — the user types it. After each script is written, tell the user the exact command to run it and what output to expect, then review what they get and correct as needed. Claude may still use Write/Edit directly for non-learning scaffolding (config, docs like this file, boilerplate the user didn't ask to be walked through).
  - **Exception, from mid-Session 2 onward: the React/TypeScript frontend is built directly by Claude, tutor mode off.** The user chose to learn React/TS in a separate project instead, due to time pressure (the season was already live). This applies to `frontend/` only — the Python pipeline side keeps the tutor-mode default if it's revisited.
  - **The v2 `backend/` (C#/.NET) is also built directly by Claude, tutor mode off** — same reasoning, extended to the new stack. The user is optimizing for a correct long-term architecture over learning C#/.NET in this project.

## Git workflow

- `main` — production, auto-deploys to https://fpl-assistant-moneyger.vercel.app on every push. **Still running v1** (single-user, static JSON) — v2's `backend/` is not wired into this deploy yet, since Vercel doesn't run long-lived containers/processes and a real hosting decision for `backend/` hasn't been made.
- `develop` — staging, auto-deploys to a Vercel preview URL, same v1 caveat as above.
- Feature branches off `develop`, e.g. `feat/dotnet-backend-rewrite` → PR/merge to `develop` → test → merge to `main` → deploy. When opening a PR via GitHub's own suggested link, double check the base branch — it defaults to the repo's default branch (`main`), not `develop`.
- Remote: https://github.com/MiraB-tech/fpl-assistant-moneyger

## Modular layout — keep things in their own directory

```
the_assistant_moneyger/
├── data/                        # HISTORICAL — v1 output, no longer written to by anything live
│   ├── raw/
│   ├── gw{N}_predictions.json
│   ├── gw{N}_results.json
│   ├── my_squad.json
│   └── model_performance_log.csv
├── pipeline/                    # HISTORICAL — v1's Python pipeline, superseded by backend/. Still
│   │                             runnable standalone (own venv at pipeline/.venv) if ever needed,
│   │                             but not part of the live app going forward.
│   ├── .venv/
│   ├── pull_data.py / build_features.py / predict.py / evaluate.py / build_squad.py / run_gameweek.py
├── backend/                      # v2 — C#/.NET (ASP.NET Core), Vertical Slice Architecture, built
│   │                              directly by Claude (tutor mode off, see above).
│   ├── AssistantMoneyger.sln
│   ├── docker-compose.yml        # local Postgres only — the API runs via `dotnet run`, not containerized
│   ├── src/AssistantMoneyger.Api/
│   │   ├── Program.cs            # DI, Identity, EF, endpoint mapping — the composition root
│   │   ├── appsettings.Development.json  # local Docker Postgres connection string (safe to commit — throwaway dev creds)
│   │   ├── Data/                 # ApplicationUser, AppDbContext, Entities/ (Prediction, Result, ModelPerformanceLogEntry, PredictionRun)
│   │   ├── Fpl/                  # FplApiClient + DTOs — shared FPL API access, not feature-specific
│   │   └── Features/             # one folder per use-case: Auth, Team, Gameweek, Predictions, Evaluation, Squad
│   └── tests/AssistantMoneyger.Api.Tests/
│       └── Features/Predictions/XpEngineTests.cs   # unit tests for the ported xP formula
├── frontend/                    # React + TS (Vite). Own node_modules — built directly (not tutor mode).
│   └── src/
│       ├── types.ts             # Player, SquadPick, Squad, User — matches backend/'s JSON exactly
│       ├── data.ts              # fetch() wrappers for backend/'s /api/* endpoints (credentials: include)
│       ├── logic/                # pickBestXI (formation + captain picker), suggestTransfers (upgrade finder) — unchanged by the backend rewrite
│       └── components/           # SquadView, BestXIView, TransfersView, LoginView, RegisterView, TeamSetupView
├── doc/                         # proposal, methodology, this-session notes
└── CLAUDE.md
```

Rule of thumb: a directory's dependencies, env, and build artifacts stay inside that directory (`pipeline/.venv`, `frontend/node_modules`, `backend/**/bin,obj`). Nothing installs globally. Previously `data/` was the cross-cutting contract between `pipeline/` and `frontend/`; in v2, `backend/`'s Postgres database plays that role instead — `frontend/` talks to `backend/` over HTTP, not shared files.

## Backend (v2) dev environment

```bash
cd backend
docker compose up -d                          # starts local Postgres on localhost:5433
cd src/AssistantMoneyger.Api
dotnet ef database update                     # applies migrations (only needed after a schema change)
dotnet run --launch-profile http               # plain HTTP on :5251 — avoids dev-cert friction
```

`frontend/vite.config.ts` proxies `/api/*` to `http://localhost:5251`, so running `npm run dev` in `frontend/` alongside the above gives you the full app at `http://localhost:5173` with working cookie-based auth (same-origin from the browser's point of view).

Run tests with `dotnet test` from `backend/`. If you change `Data/Entities/` or `AppDbContext`, generate a new migration with `dotnet ef migrations add <Name>` before `dotnet ef database update`.

## Pipeline environment (v1, historical — see the v2 section above)

```bash
cd pipeline
./.venv/Scripts/python.exe -m pip install -r requirements.txt
./.venv/Scripts/python.exe run_gameweek.py <next_gw_number>
```

This still works standalone (evaluates the last gameweek, pulls fresh FPL data, builds predictions, refreshes `data/my_squad.json`) but is no longer part of the live app's data flow — `backend/` does all of this itself now, per-request, against Postgres instead of flat files.

## Tech stack

**v1 (Sessions 0-3, locked — see proposal.md §3 for original rationale):** transparent weighted xP formula (not ML), flat JSON/CSV storage, manual pipeline execution, React+TS frontend reading static JSON, Vercel free tier, $0 running cost.

**v2 (in progress, see above):** same xP formula (ported, not changed), C#/.NET (ASP.NET Core, Vertical Slice Architecture) backend, ASP.NET Core Identity for auth, Postgres (local via Docker for dev; production host TBD), React+TS frontend unchanged except talking to the new API instead of static files.

Data sources (unchanged across v1/v2): official FPL API (live) — `bootstrap-static/`, `fixtures/`, `event/{gw}/live/`, `entry/{team_id}/event/{gw}/picks/`, `entry/{team_id}/`. (vaastav/Understat were considered in Session 0 but never actually integrated — FPL's own API already provides `expected_goals_per_90`/`expected_assists_per_90`.)

## xP formula v1 (`doc/proposal.md` §5 — starting hypothesis, will be tuned via `model_performance_log.csv`)

```
xP = (recent_form × 0.40)
   + (xG90_and_xA90 × 0.25)
   + (fixture_difficulty × 0.20)
   + (minutes_reliability × 0.15)
```

## Data flow

**v1 (historical):**
```
FPL API  →  pull_data.py  →  data/raw/
data/raw/  →  build_features.py  →  (in-memory features)
features  →  predict.py (xP formula)  →  data/gw{N}_predictions.json
data/gw{N}_predictions.json + actual GW results  →  evaluate.py  →  data/gw{N}_results.json, data/model_performance_log.csv
```

**v2 (current):**
```
FPL API  →  FplApiClient  →  XpEngine.BuildFeatures/CalculateXp  →  Predictions table (Postgres)
Predictions table + actual GW results (FplApiClient)  →  EvaluateGameweek  →  Results + ModelPerformanceLog tables
Predictions table + a user's live FPL picks  →  GetSquad  →  Squad JSON  →  frontend (pickBestXI/suggestTransfers run client-side on this)
```
All of this is triggered on-demand via `POST /api/gameweek/advance` (any logged-in user can press "Refresh predictions") rather than a script a human runs manually — `PredictionRun`'s staleness check (6hr) is what stops that from hammering the FPL API if many users click it.
