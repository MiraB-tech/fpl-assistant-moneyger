# The Assistant Moneyger — Project Proposal

**A MiraBloxTech (MBT) Project · "Let's Code Tomorrow"**

---

## 1. Vision

A **copilot, not an autopilot** for Fantasy Premier League squad decisions. It should surface data and predictions clearly enough that the user can make informed calls on:

1. **Squad evaluation** — score/rank the current 15 to spot underperformers
2. **Transfer suggestions** — surface good in-budget replacement candidates
3. **Starting XI / captaincy** — recommend the best 11 + captain per gameweek

The model should be **transparent and explainable** at every stage — no black boxes. The user should always be able to see *why* a player is rated the way they are.

Scope: personal use to start, possibly 1–2 friends later. This is v1 — a prototype, not a finished product — and it stays a work-in-progress across the whole season.

**Target: a usable prototype live before the GW1 deadline (Premier League season opens Friday 21 August 2026 — 17 days from this proposal).**

---

## 2. Build Methodology: Solo Tri-Sesh

This project is built using **Solo Tri-Sesh**, a workload-based (not time-based) framework for solo builders working with an AI copilot such as Claude Code. Full details, rules, and rationale are documented separately in **`solo_tri_sesh.pdf`**. In short:

- **Session 0** (this document): scope and lock the proposal — no code yet.
- **Sessions 1–3**: focused build blocks, each sized by workload rather than a calendar. Once a session starts, it runs to completion — short breaks are fine mid-session, longer breaks only happen *between* sessions.

---

## 3. Tech Stack Decisions (and why)

| Decision | Choice | Rationale |
|---|---|---|
| Prediction model (v1) | Transparent weighted formula, not ML | Explainable, tweakable, fits the "copilot" philosophy, realistic given the timeline |
| Model feedback loop | Log predicted vs. actual after each GW, don't auto-adjust yet | Builds a real labeled dataset over the season for a future ML model, without risking instability now |
| Data storage | Flat JSON/CSV files, versioned in git | No database server to manage; transparent and diffable; fits "learning as I go" |
| Data pipeline execution | Manual (run by user before/after each GW) | Full control while learning; automate later if desired |
| Backend server | None for v1 — React reads static JSON files directly | Removes an entire layer of complexity; revisit only if live/dynamic data becomes necessary |
| Frontend | React + TypeScript | TS adds structure/error-catching that pays off over a season-long project; learning curve mitigated by Claude Code assistance |
| Frontend hosting | **Vercel** (free tier, locked in) | Public URL, shareable with friends, zero cost, native fit for React |
| Historical data | vaastav/Fantasy-Premier-League GitHub repo | Free, cleaned, multi-season data for establishing baselines |
| Live data | Official FPL API (`fantasy.premierleague.com/api/`) | Free, no auth, current prices/points/fixtures/ownership |
| Underlying stats | Understat (xG/xA) | Free, more predictive signal than raw goals/assists |
| Code hosting | GitHub | Free, connects directly to Vercel |

**Total running cost: $0.**

---

## 4. Architecture

```
┌─────────────────────────┐
│   Data Sources (free)    │
│  - FPL official API      │
│  - vaastav GitHub repo   │
│  - Understat              │
└───────────┬───────────────┘
            │  (manual run)
            ▼
┌─────────────────────────┐
│  Python Data Pipeline     │
│  1. Pull & clean data     │
│  2. Feature engineering   │
│     (form, xG90, fixture  │
│      difficulty, mins)    │
│  3. Apply xP formula      │
│  → data/gw{N}_predictions.json │
└───────────┬───────────────┘
            │  (after GW finishes, manual run)
            ▼
┌─────────────────────────┐
│  Evaluation Script         │
│  - Pull actual GW results  │
│  - Compare vs predictions  │
│  → data/gw{N}_results.json │
│  → data/model_performance_log.csv │
└───────────┬───────────────┘
            │  (git commit/push)
            ▼
┌─────────────────────────┐
│  React + TS Frontend       │
│  - Reads JSON from /data   │
│  - Squad view               │
│  - Suggested XI + captain   │
│  - Transfer suggestions     │
│  - Model accuracy over time │
│  (deployed on Vercel, free) │
└─────────────────────────┘
```

---

## 5. Prediction Model (v1 formula — starting point, will be tuned)

```
xP = (recent_form × 0.40)
   + (xG90_and_xA90 × 0.25)
   + (fixture_difficulty × 0.20)
   + (minutes_reliability × 0.15)
```

Weights are a starting hypothesis, not gospel — expect to adjust them as `model_performance_log.csv` accumulates real accuracy data across gameweeks.

---

## 6. Suggested Folder Structure

```
assistant-moneyger/
├── data/
│   ├── raw/                     # untouched pulls from APIs
│   ├── gw1_predictions.json
│   ├── gw1_results.json
│   └── model_performance_log.csv
├── pipeline/
│   ├── pull_data.py
│   ├── build_features.py
│   ├── predict.py               # applies xP formula
│   └── evaluate.py              # predicted vs actual after GW
├── frontend/
│   ├── src/
│   ├── public/data/             # copies of JSON for React to read
│   └── package.json
└── README.md
```

---

## 7. Build Roadmap — Solo Tri-Sesh Sessions

Sessions are **workload-bound, not time-boxed** — each runs until its deliverable is done.

| Session | Focus | Deliverable |
|---|---|---|
| **Session 0** ✅ | Scope, research, tech decisions, this proposal | `proposal.md` / `.docx`, `solo_tri_sesh.pdf` |
| **Session 1** | Data & Prediction Engine — repo scaffold, FPL API pipeline, historical data import, feature engineering, xP formula v1, evaluation script skeleton | A working pipeline producing a real `gw{N}_predictions.json` |
| **Session 2** | Frontend Build — React + TS app scaffold, reads JSON data, Squad view, Suggested XI + captain, Transfer suggestions UI | A working local frontend rendering real prediction data |
| **Session 3** | Integration, Testing & Launch — wire pipeline output into `/public/data`, deploy to Vercel, test against the real squad, fix issues, ship | Live public URL, ready for GW1 |

**Target:** Session 3 complete before the GW1 deadline (Friday 21 August 2026).

---

## 8. Beyond GW1 (season-long evolution)

- Accumulate `model_performance_log.csv` every gameweek
- Once several weeks of predicted-vs-actual data exist, consider a proper ML model trained on it
- Revisit automation (e.g. scheduled pipeline runs) if manual running becomes tedious
- Revisit a live backend only if static JSON stops being sufficient (e.g. real-time price-change alerts)

---

## 9. Paid Alternatives Worth Knowing About (not needed now)

- **Fantasy Football Scout** — subscription, own xG/xMins projections; their free content is already solid
- **FPL Review** — free xPoints output, paid optimizer tier

Neither is necessary at this stage — flagged here only for future reference if the free data sources hit a real limitation.

---

*MiraBloxTech — Let's Code Tomorrow*
