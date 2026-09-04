# The Assistant Moneyger — CLAUDE.md

A copilot (not autopilot) for Fantasy Premier League squad decisions. Full context: `doc/proposal.md` (locked Session 0 output) and `doc/solo_tri_sesh.pdf` (build methodology).

## Build methodology: Solo Tri-Sesh

Workload-bound, not time-boxed. Four sessions total:

| Session | Focus | Deliverable | Status |
|---|---|---|---|
| 0 | Scope, research, tech decisions | `doc/proposal.md` | ✅ done |
| 1 | Data & Prediction Engine — pipeline, features, xP formula v1, eval skeleton | working pipeline → real `gw{N}_predictions.json` | ✅ done |
| 2 | Frontend Build — React+TS, squad view, XI, transfers | working local frontend on real data | ✅ done |
| 3 | Integration, testing, Vercel deploy | live public URL | ✅ done — https://fpl-assistant-moneyger.vercel.app |

Rules: once a session starts it runs to completion (short breaks fine, long breaks only *between* sessions). A session ends when its deliverable is real and working, not on a clock. Don't start Session 2 work while Session 1's deliverable isn't real yet, and vice versa.

Note: the original target was live before the Fri 21 Aug 2026 GW1 deadline; the build ran behind that (GW1/GW2 had already started before Session 1 closed out). From partway through Session 2 onward, the user prioritized shipping speed over the learning goal — see the working-style note below.

## Working style — this is a learning project

The user is learning as they build, not just shipping. This changes how Claude should work here vs. a typical repo:

- **No throwaway inline probing.** Don't explore an API/library shape with a one-off `python -c "..."` command. Write a real script file, run it, keep it (or fold it straight into the real module it was exploring for). The exploration itself is part of what the user is learning from.
- **Sequential, one step at a time.** Build and explain one piece, pause, let the user look at it, before moving to the next. Don't chain several build steps together unprompted.
- **Comment code in plain, non-technical language.** Comments should explain *what's happening and why* in terms a non-engineer could follow, not just restate the code. This is the opposite of the terse/no-comments default — deliberate, for this project.
- **Tutor mode — Claude explains, the user writes.** Claude acts as an advanced tutor/textbook across every language involved: introduce the piece being built and why, then dictate what to write file-by-file, and within a file, piece-by-piece (imports, constants, then each method one at a time with its purpose). Pause after each piece for the user to write it themselves and report back before continuing. Claude does not use Write/Edit to author the substantive learning code itself — the user types it. After each script is written, tell the user the exact command to run it and what output to expect, then review what they get and correct as needed. Claude may still use Write/Edit directly for non-learning scaffolding (config, docs like this file, boilerplate the user didn't ask to be walked through).
  - **Exception, from mid-Session 2 onward: the React/TypeScript frontend is built directly by Claude, tutor mode off.** The user chose to learn React/TS in a separate project instead, due to time pressure (the season was already live). This applies to `frontend/` only — the Python pipeline side keeps the tutor-mode default if it's revisited.

## Git workflow

- `main` — production, auto-deploys to https://fpl-assistant-moneyger.vercel.app on every push
- `develop` — staging, auto-deploys to a Vercel preview URL (`fpl-assistant-moneyger-git-develop-mira-blox-tech.vercel.app`) on every push. Preview deployments sit behind Vercel's login wall (Deployment Protection) — only accessible to logged-in team members for now, by choice.
- Vercel project root directory is set to `frontend/` (the repo has other top-level folders); it still checks out the full repo, so `frontend/copy-data.js`'s `../data` read works at build time.
- Feature branches off `develop`, e.g. `feat/fpl-api-pipeline` → PR/merge to `develop` → test → merge to `main` → deploy. When opening a PR via GitHub's own suggested link, double check the base branch — it defaults to the repo's default branch (`main`), not `develop`.
- Remote: https://github.com/MiraB-tech/fpl-assistant-moneyger

## Modular layout — keep things in their own directory

```
the_assistant_moneyger/
├── data/
│   ├── raw/                     # untouched pulls from APIs (FPL, vaastav, Understat)
│   ├── gw{N}_predictions.json
│   ├── gw{N}_results.json
│   ├── my_squad.json            # your real current squad, joined with that GW's predictions
│   └── model_performance_log.csv
├── pipeline/                    # Python. Own venv at pipeline/.venv — never installed globally.
│   ├── .venv/                   # gitignored
│   ├── requirements.txt
│   ├── pull_data.py
│   ├── build_features.py
│   ├── predict.py               # applies xP formula
│   ├── evaluate.py              # predicted vs actual after a GW
│   ├── build_squad.py           # pulls your FPL squad (by team ID) for a GW, merges with predictions
│   └── run_gameweek.py          # the one script to run weekly — does all of the above in order
├── frontend/                    # React + TS (Vite). Own node_modules — Session 2, built directly (not tutor mode).
│   ├── copy-data.js             # copies data/*.json into public/data before dev/build (predev/prebuild hook)
│   ├── public/data/             # gitignored — regenerated by copy-data.js, not the source of truth
│   └── src/
│       ├── types.ts             # Player, SquadPick, Squad shapes matching the pipeline's JSON
│       ├── data.ts              # fetch() wrappers for public/data/*.json
│       ├── logic/                # pickBestXI (formation + captain picker), suggestTransfers (upgrade finder)
│       └── components/           # SquadView, BestXIView, TransfersView
├── doc/                         # proposal, methodology, this-session notes
└── CLAUDE.md
```

Rule of thumb: a directory's dependencies, env, and build artifacts stay inside that directory (`pipeline/.venv`, `frontend/node_modules`). Nothing installs globally. `data/` is the only cross-cutting directory — it's the contract between `pipeline/` (writer) and `frontend/` (reader).

## Pipeline environment

```bash
cd pipeline
./.venv/Scripts/python.exe -m pip install -r requirements.txt   # deps already installed at session start
```

**Weekly routine (the one command to run):**

```bash
./.venv/Scripts/python.exe run_gameweek.py <next_gw_number>
```

This evaluates the gameweek that just finished (if not already evaluated), pulls fresh FPL data, builds predictions for `<next_gw_number>`, and refreshes `data/my_squad.json` with your real squad for that gameweek — all in one run. After it finishes, commit the changed `data/*.json` files and push to `develop` (then merge to `main` when ready) so the deployed frontend picks up the new data.

The individual steps (`pull_data.py`, `build_features.py`, `predict.py`, `evaluate.py`, `build_squad.py`) still work standalone if you need to debug just one stage.

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
