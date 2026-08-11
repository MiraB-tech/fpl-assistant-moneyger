# The Assistant Moneyger — CLAUDE.md

A copilot (not autopilot) for Fantasy Premier League squad decisions. Full context: `doc/proposal.md` (locked Session 0 output) and `doc/solo_tri_sesh.pdf` (build methodology).

## Build methodology: Solo Tri-Sesh

Workload-bound, not time-boxed. Four sessions total:

| Session | Focus | Deliverable | Status |
|---|---|---|---|
| 0 | Scope, research, tech decisions | `doc/proposal.md` | ✅ done |
| 1 | Data & Prediction Engine — pipeline, features, xP formula v1, eval skeleton | working pipeline → real `gw{N}_predictions.json` | 🔵 in progress |
| 2 | Frontend Build — React+TS, squad view, XI, transfers | working local frontend on real data | not started |
| 3 | Integration, testing, Vercel deploy | live public URL before GW1 deadline (Fri 21 Aug 2026) | not started |

Rules: once a session starts it runs to completion (short breaks fine, long breaks only *between* sessions). A session ends when its deliverable is real and working, not on a clock. Don't start Session 2 work while Session 1's deliverable isn't real yet, and vice versa.

## Git workflow

- `main` — production, hooked to Vercel prod deploy (deploy wiring is a Session 3 concern, not yet configured)
- `develop` — test/staging, hooked to Vercel preview deploy
- Feature branches off `develop`, e.g. `feat/fpl-api-pipeline` → PR/merge to `develop` → test → merge to `main` → deploy
- Remote: https://github.com/MiraB-tech/fpl-assistant-moneyger

## Modular layout — keep things in their own directory

```
the_assistant_moneyger/
├── data/
│   ├── raw/                     # untouched pulls from APIs (FPL, vaastav, Understat)
│   ├── gw{N}_predictions.json
│   ├── gw{N}_results.json
│   └── model_performance_log.csv
├── pipeline/                    # Python. Own venv at pipeline/.venv — never installed globally.
│   ├── .venv/                   # gitignored
│   ├── requirements.txt
│   ├── pull_data.py
│   ├── build_features.py
│   ├── predict.py               # applies xP formula
│   └── evaluate.py              # predicted vs actual after a GW
├── frontend/                    # React + TS. Own node_modules — Session 2.
├── doc/                         # proposal, methodology, this-session notes
└── CLAUDE.md
```

Rule of thumb: a directory's dependencies, env, and build artifacts stay inside that directory (`pipeline/.venv`, `frontend/node_modules`). Nothing installs globally. `data/` is the only cross-cutting directory — it's the contract between `pipeline/` (writer) and `frontend/` (reader).

## Pipeline environment

```bash
cd pipeline
./.venv/Scripts/python.exe -m pip install -r requirements.txt   # deps already installed at session start
./.venv/Scripts/python.exe pull_data.py
./.venv/Scripts/python.exe build_features.py
./.venv/Scripts/python.exe predict.py
```

## Tech stack (locked in Session 0 — see proposal.md §3 for rationale)

- Prediction model v1: transparent weighted formula, not ML (explainable, tweakable)
- Storage: flat JSON/CSV, versioned in git — no database
- Pipeline execution: manual, run by user per GW — no scheduler/backend
- Frontend: React + TypeScript, reads static JSON directly, hosted on Vercel free tier
- Data sources: official FPL API (live), vaastav/Fantasy-Premier-League GitHub repo (historical), Understat (xG/xA)
- Total running cost: $0

## xP formula v1 (`doc/proposal.md` §5 — starting hypothesis, will be tuned via `model_performance_log.csv`)

```
xP = (recent_form × 0.40)
   + (xG90_and_xA90 × 0.25)
   + (fixture_difficulty × 0.20)
   + (minutes_reliability × 0.15)
```

## Data flow

```
FPL API + vaastav repo + Understat  →  pull_data.py  →  data/raw/
data/raw/  →  build_features.py  →  (in-memory features)
features  →  predict.py (xP formula)  →  data/gw{N}_predictions.json
data/gw{N}_predictions.json + actual GW results  →  evaluate.py  →  data/gw{N}_results.json, data/model_performance_log.csv
```
