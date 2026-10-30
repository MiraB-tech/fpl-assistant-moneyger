# How The Assistant Moneyger Works

A guide for someone who didn't build it. No coding background assumed — every technical word is explained the first time it shows up, and there's a short glossary at the end.

---

## 0. The big picture

The app answers one question every gameweek: **"What should I do with my FPL team?"** — who to start, who to captain, who to transfer in.

It has four parts. Think of a club's matchday operation:

| Part | Football version | What it really is | Where it lives |
|---|---|---|---|
| **FPL API** | The league's official stats feed | Fantasy Premier League's public website data, which any program can read | The internet (we don't own it) |
| **Backend** | The analysts' room | A program written in C# that fetches stats, runs the prediction model, and answers questions | `backend/` |
| **Database** | The analysts' filing cabinet | PostgreSQL — software that stores data in organised sheets (like spreadsheet tabs) | A Docker container on your PC |
| **Frontend** | The manager's tablet | The website you see, written in React/TypeScript. It only *displays* things and *asks* the backend | `frontend/` |

```
 You (browser)  ──asks──►  Backend  ──fetches──►  FPL API
   frontend     ◄─answers─    │      ◄─────────
                              ▼▲  saves / reads
                          Database (PostgreSQL)
```

**The golden rule:** the frontend never talks to FPL directly. Everything goes through the backend, which is the only part that knows how to get data and make predictions.

> There's also an older, retired part — the **v1 pipeline** (Python scripts in `pipeline/` writing files into `data/`). It was the first version of the app. It's covered in section 2 because the current backend is a rewrite of it, but nothing live uses it anymore.

---

## 1. The data

### 1.1 What FPL gives us

FPL's website is built on top of a set of public addresses ("endpoints") that return raw data. We use five:

| Endpoint | What it contains | Used for |
|---|---|---|
| `bootstrap-static/` | **Every player** (~670): name, team, position, price, minutes played, injury status, expected goals/assists per 90 minutes. Plus every team and every gameweek. | The raw material for every prediction |
| `fixtures/` | Every match of the season: who plays whom, which gameweek, and FPL's **difficulty rating** (1 = easiest, 5 = hardest) for each side | How hard each player's next match is |
| `event/{gw}/live/` | Real points every player scored in a gameweek | Grading our predictions; showing live points |
| `entry/{team_id}/event/{gw}/picks/` | One manager's 15 players for a gameweek, captain, bank balance | Your squad |
| `entry/{team_id}/` | Basic info about a manager's team | Checking a team ID is real when you sign up |

Two things worth knowing:
- **Picks only exist after a deadline.** FPL won't tell anyone your GW9 team until the GW9 deadline passes. So to plan GW9, the app takes your GW8 squad (your real current players) and pairs it with GW9 predictions.
- **The data arrives as JSON** — plain text in a structured format, e.g. `{"web_name": "Haaland", "now_cost": 157}`. Note FPL stores prices in tenths: `157` means £15.7m.

### 1.2 What we store (the database)

The database is a set of sheets, each with fixed columns. The ones that matter:

| Sheet | One row is... | Key columns |
|---|---|---|
| `Predictions` | One player's forecast for one gameweek | Gw, PlayerId, Name, Position, Team, Price, StartProbability, CleanSheetProbability, ExpectedGoals, ExpectedAssists, **Xp** |
| `PredictionRuns` | "When were gameweek N's predictions last built?" | Gw, LastRefreshedAt, PlayerCount |
| `Results` | One player's forecast vs. what really happened | Gw, PlayerId, PredictedPoints, ActualPoints, Difference |
| `ModelPerformanceLog` | One gameweek's overall accuracy score | Gw, NumPlayers, MeanAbsoluteError |
| `AspNetUsers` | One user account | Email, scrambled password, FplTeamId |

(The other `AspNet...` sheets come with the login system and are unused. `__EFMigrationsHistory` records which database changes have been applied — see Migrations in section 3.)

**Why store predictions at all, instead of computing them each time?** Two reasons:
1. **Speed and politeness.** Building predictions downloads FPL's whole player list. If 5 users each opened the app 10 times a day, that's 50 downloads — instead we do it at most once every 6 hours and everyone reads the saved copy.
2. **Honest grading.** To judge the model fairly, we need the prediction *as it was before the deadline*. Rebuilding it afterwards would use data that already knows what happened. **A missed gameweek can't be honestly back-filled** — this matters for hosting (section 5).

### 1.3 The `data/` folder (historical)

Files written by the old v1 pipeline: `gw1_predictions.json`, `gw1_results.json`, `model_performance_log.csv`, `my_squad.json`, and `raw/` (saved copies of FPL's responses). Nothing reads or writes these anymore. They're kept as a record of the first version.

---

## 2. The v1 pipeline (Python, historical)

Seven Python scripts that you ran by hand once a week. The backend now does all of this automatically, but reading them is the easiest way to see the *steps* of making a prediction, because each step is its own file.

| File | What it does |
|---|---|
| `pull_data.py` | Downloads `bootstrap-static` and `fixtures` from FPL and saves them into `data/raw/`. Also fetches live points and your squad. |
| `peek_data.py` | A look-around script: prints a sample of the downloaded data so you can see its shape. Not part of the weekly run. |
| `build_features.py` | Turns raw data into four numbers per player ("features"): **recent form** (points last gameweek), **attacking threat** (xG + xA per 90), **fixture ease** (6 − difficulty, so higher = easier), **minutes reliability** (minutes played ÷ 3420, a full season). |
| `predict.py` | The v1 formula. Squashes each feature onto a 0–1 scale, then blends them: `form×0.40 + threat×0.25 + fixture×0.20 + minutes×0.15`. Saves `gw{N}_predictions.json`. |
| `evaluate.py` | After a gameweek, compares predictions with real points and adds one line to `model_performance_log.csv`. |
| `build_squad.py` | Fetches your 15 picks and attaches each player's prediction. Saves `my_squad.json` for the old frontend. |
| `run_gameweek.py` | The one-button weekly run: evaluate last week → pull fresh data → build features → predict → refresh squad. |

**Why v1 was replaced:** its "xP" wasn't really *points* — it was a 0–1 ranking score. That's fine for ordering players, but you can't say "Haaland will score 5.4 points", and you can't compare it fairly against real points. The current model (next section) fixes that.

---

## 3. The backend (C#)

### 3.1 How it's organised

**C#** is a programming language; **ASP.NET Core** is Microsoft's toolkit for building web backends with it. The code is organised by **feature** ("Vertical Slice Architecture"): everything for one job — say, "show my squad" — sits in one file, instead of being spread across many technical layers. To change how something works, you find its folder under `Features/`.

The backend is a **web API**: it sits waiting for requests at addresses like `/api/squad`, does some work, and replies with JSON. Each address + its code is called an **endpoint**.

```
backend/
├── docker-compose.yml          the database's setup
├── src/AssistantMoneyger.Api/
│   ├── Program.cs              start-up: wires everything together
│   ├── appsettings*.json       settings (database address, allowed website)
│   ├── Fpl/                    talking to FPL
│   ├── Data/                   talking to the database
│   ├── Migrations/             history of database layout changes
│   └── Features/               one folder per job the app does
└── tests/                      automatic checks for the prediction maths
```

### 3.2 Start-up and settings

| File | What it does |
|---|---|
| `Program.cs` | Runs first. Switches on the login system, connects to the database and FPL, adds two safety checks (below), then registers every endpoint. If you add a new feature, you register it here. |
| `appsettings.json` / `appsettings.Development.json` | Settings kept out of the code: the database's address and password (throwaway local ones — fine to commit), and `AllowedOrigin` (which website may send changes). |
| `docker-compose.yml` | Recipe for the local database. `docker compose up -d` starts PostgreSQL in a **container** (a sealed mini-computer on your PC) on port 5433. |

The two safety checks in `Program.cs`:
- **Login cookie.** When you log in, the backend gives your browser a **cookie** — a small pass the browser shows on every later request, like a season ticket at the turnstile. It lasts 30 days.
- **Origin check.** Any request that *changes* something must come from `AllowedOrigin` (`http://localhost:5173` in development). This stops another website from secretly pressing buttons on your behalf. It's also why the app broke when the frontend silently moved to port 5174.

### 3.3 `Fpl/` — talking to FPL

| File | What it does |
|---|---|
| `FplApiClient.cs` | One method per FPL endpoint from section 1.1. Keeps a 5-minute copy of `bootstrap-static` in memory so the big player list isn't re-downloaded on every page load. |
| `Dtos/*.cs` | "Data Transfer Objects" — descriptions of FPL's JSON shape, so C# knows `now_cost` is a whole number, `web_name` is text, and so on. Only the fields we use are listed. |

### 3.4 `Data/` — talking to the database

| File | What it does |
|---|---|
| `Entities/*.cs` | One file per database sheet from section 1.2 (`Prediction`, `Result`, `ModelPerformanceLogEntry`, `PredictionRun`), describing its columns. |
| `ApplicationUser.cs` | A user account: the login system's standard fields plus our one extra, `FplTeamId`. |
| `AppDbContext.cs` | The gateway to the database. Code writes `db.Predictions.Where(p => p.Gw == 9)` and this translates it into a database query. (The translator is called **Entity Framework**, or **EF**.) |
| `PredictionWriteLock.cs` | Makes the "grade + rebuild predictions" job run **one request at a time**. Without it, two simultaneous refreshes both thought the work wasn't done, both saved, and one crashed (the 500 error). |

**Migrations** (`Migrations/`): when an entity changes — e.g. a column is renamed — EF writes a migration file describing the change, and `dotnet ef database update` applies it. It's version history for the database's layout.

### 3.5 `Features/` — what the app actually does

| Folder | Endpoint(s) | What happens |
|---|---|---|
| `Auth/` | `POST /api/auth/register`, `/login`, `/logout`, `GET /api/auth/me` | Create account, log in (get the cookie), log out, "who am I?". Passwords are stored scrambled — even we can't read them. |
| `Team/` | `PUT /api/team` | Save your FPL team ID — after checking with FPL that it's a real team. |
| `Gameweek/` | `GET /api/gameweek/current`, `POST /api/gameweek/advance` | "Which gameweek is it?", and **advance** — the main job: grade last gameweek, then make sure next gameweek's predictions exist. Runs automatically every time the app loads. |
| `Predictions/` | `GET /api/predictions?gw=9`, `POST /api/predictions/refresh` | Return every player's saved prediction; rebuild them. Also home to **`XpEngine.cs` — the prediction model** (3.6). |
| `Evaluation/` | `POST /api/evaluate` | Grade one gameweek: prediction vs. real points for every player, plus one overall accuracy score (section 3.7). |
| `Squad/` | `GET /api/squad?gw=9&picksGw=8` | Your 15 players from FPL, with each one's prediction attached. |

### 3.6 How a prediction is made (the heart of the app)

**Step by step — what happens when predictions are rebuilt** (`RefreshPredictions.cs` → `XpEngine.cs`):

1. **Is it needed?** Look in `PredictionRuns`. If this gameweek was built less than 6 hours ago, stop and reuse the saved predictions.
2. **Fetch** `bootstrap-static` (all players) and `fixtures` (all matches) from FPL.
3. **For each player:**
   - Find their match this gameweek. **No match (a blank gameweek)? Skip them.**
   - Calculate **start probability** — the chance they play 60+ minutes.
   - Calculate **clean-sheet probability** — the chance their team concedes nothing.
   - Turn those, plus their xG and xA, into **expected points (xP)**.
4. **Save:** delete the old rows for that gameweek, write the new ones, and stamp `PredictionRuns` with the time.

**The idea behind xP.** Instead of guessing "Haaland will score 2 goals", ask "on average, across every way this match could go, how many FPL points does he earn?" Then apply FPL's real scoring rules to each outcome. A 33% clean-sheet chance × 4 points = 1.32 expected points — not "4" or "0", but the honest average.

**The ingredients:**

| Ingredient | How it's calculated | Where the number comes from |
|---|---|---|
| **Start probability** | `fitness × reliability` | **Fitness:** FPL's injury flag (`chance_of_playing_next_round`: 75 → 0.75; blank = fit = 1.0). **Reliability:** share of available minutes played this season × 1.3, capped at 1 — so a regular starter counts as 100% even if he's missed the odd game. |
| **Clean-sheet probability** | `0.55 − (difficulty − 1) × 0.11`, kept between 0.05 and 0.55 | FPL's fixture difficulty. Easiest match (1) → 55%; hardest (5) → 11%. |
| **Expected goals / assists** | xG per 90 × start probability (same for xA) | FPL's own `expected_goals_per_90` / `expected_assists_per_90`. |

**The formula** (`CalculateXp`), using FPL's real points:

| Points source | Calculation | Applies to |
|---|---|---|
| Appearance | start × 2 | Everyone |
| Goals | xG/90 × start × (6 GK/DEF, 5 MID, 4 FWD) | Everyone |
| Assists | xA/90 × start × 3 | Everyone |
| Clean sheet | start × CS chance × (4 GK/DEF, 1 MID) | GK, DEF, MID |
| Goals conceded | − start × (1 − CS chance) × 1.6 ÷ 2 | GK, DEF (−1 per 2 conceded; assumes ~1.6 conceded when not clean) |
| Saves | start × (1 − CS chance) × 1.2 | GK only (more shots faced → more saves) |

**Worked example 1 — Haaland, GW9** (real numbers from the database): fit and a nailed starter (start = 1.0), xG/90 = 0.77, xA/90 = 0.10.

| | |
|---|---|
| Appearance | 1.0 × 2 = **2.00** |
| Goals | 0.77 × 1.0 × 4 = **3.08** |
| Assists | 0.10 × 1.0 × 3 = **0.30** |
| Clean sheet / conceded / saves | forwards get none |
| **xP** | **5.38** ✓ matches the app |

**Worked example 2 — Calafiori, GW9**: a defender with a 75% start chance (FPL injury flag), a hard-ish fixture (difficulty 4 → 22% clean-sheet chance), xG/90 ≈ 0.18, xA/90 ≈ 0.14.

| | |
|---|---|
| Appearance | 0.75 × 2 = **1.50** |
| Goals | 0.135 (xG already × start) × 6 = **0.81** |
| Assists | 0.105 × 3 = **0.32** |
| Clean sheet | 0.75 × 0.22 × 4 = **0.66** |
| Goals conceded | − 0.75 × 0.78 × 1.6 ÷ 2 = **−0.47** |
| **xP** | **2.82** ✓ matches the app |

Notice how the injury flag cuts *every* line by 25% — a doubtful player is penalised everywhere, which is exactly right.

**Known weaknesses** (the data scientist's to-do list — none are bugs, all are simplifications):
- The constants (0.55, 0.11, 1.3, 1.6, 1.2) are **hand-picked, not learned** from data. They're sensible starting guesses.
- **Bonus points, yellow/red cards, and defensive-contribution points are ignored.**
- **xG/90 is a season average**, so a striker who's just hit form (or lost his place) changes slowly.
- **Fixture difficulty is FPL's own 1–5 rating** — coarse, and the same for every player on a team.
- **Double gameweeks** (two matches) aren't handled — only the first match found is used.

### 3.7 How the model is graded

After a gameweek finishes, `EvaluateGameweek.cs` writes one `Results` row per player (predicted vs. actual) and one `ModelPerformanceLog` row with the **mean absolute error (MAE)** — "on average, how many points off were we?" Lower is better.

The record so far:

| GW | Players | MAE | Note |
|---|---|---|---|
| 3 | 652 | 0.38 | **v1 formula — not comparable.** v1's "xP" was a 0–1 score, so its error looks small for the wrong reason. |
| 8 | 668 | 1.10 | Current model. |

**Read the GW8 number carefully:** 317 of the 668 players were predicted near zero and scored zero — mostly players who never play. Those easy "wins" drag the average down. **Among players who actually scored, the MAE was 2.07.** That's the more honest figure to track and improve. (FPL points are very random — a single goal is 4–6 points — so no model gets close to zero.)

### 3.8 Tests

`tests/.../XpEngineTests.cs` holds 12 automatic checks on the prediction maths — e.g. "an injury doubt halves start probability", "a goalkeeper's xP matches a hand-worked answer", "players with no fixture are skipped". `dotnet test` runs them all in about a second. Run them after **any** change to `XpEngine.cs`.

---

## 4. The frontend (React + TypeScript)

**TypeScript** is JavaScript (the browser's language) with labels saying what kind of data each thing holds. **React** builds the page from **components** — reusable pieces, like a player card, used many times. **Vite** is the tool that runs it during development on `http://localhost:5173`, passing any `/api/...` request through to the backend on port 5251.

### 4.1 What happens when you open the app

1. `GET /api/auth/me` — are you logged in? If not, show the login screen.
2. `GET /api/gameweek/current` — e.g. current GW8, next GW9.
3. `POST /api/gameweek/advance` — make sure GW9 predictions exist (usually: "already done").
4. Three at once:
   - `/api/squad?gw=8` → your real squad and points (**Squad** tab)
   - `/api/squad?gw=9&picksGw=8` → same 15 players with GW9 predictions (**Your Best XI**, **Transfers**)
   - `/api/predictions?gw=9` → every player's GW9 prediction (**Transfers**, **Best GW9 XI**)
5. The tabs then do their thinking **in your browser** — the backend only supplies predictions; choosing line-ups is done by the files in `logic/`.

You can watch steps 1–4 happen: press F12 → **Network** → **Fetch/XHR** → reload.

### 4.2 The files

**Plumbing**

| File | What it does |
|---|---|
| `main.tsx` | Starts React and puts the app on the page. |
| `App.tsx` | The conductor: checks login, loads data (4.1), draws the header and tabs, remembers the open tab in the address bar (`#best-xi`). |
| `data.ts` | One small function per backend endpoint — the only file that talks to the backend. |
| `types.ts` | Describes the shape of the backend's replies (a `Player` has `name`, `position`, `price`, `xP`...). Must match the backend exactly. |
| `constants.ts` | Position labels (GK/DEF/MID/FWD) and the pitch's row order. |
| `teamColors.ts` | Each club's shirt colour for the player cards, plus picking black or white text so names stay readable. |
| `index.css`, `App.css` | The look: colours (light + dark mode) in `index.css`, layout of every piece in `App.css`. |

**The thinking (`logic/`)** — plain rules, no AI:

| File | What it does |
|---|---|
| `bestXI.ts` | **Your Best XI.** Tries every legal formation (3-4-3, 4-4-2, 5-3-2...). For each, takes your highest-xP keeper and your top players in each line; keeps the formation with the most total xP. Captain = highest xP, vice = second. |
| `transfers.ts` | **Transfers.** For each of your 15, finds the highest-xP player in the same position that you can afford (his price + your bank) and who'd score more. Shows the 5 biggest gains. Ignores FPL's sell-on tax, so costs are estimates. |
| `bestGwSquad.ts` | **Best GW XI.** Builds the best possible £100m squad from all ~670 players (2 GK, 5 DEF, 5 MID, 3 FWD, max 3 per club). Bench = cheapest legal players. Then: pick the top-xP XI ignoring price → sell down whoever loses the fewest points per £1m until it fits → keep trying 1- and 2-player swaps that add points. Takes ~25ms. Very good, but not a guaranteed perfect answer. |

**The screens (`components/`)**

| File | What it shows |
|---|---|
| `LoginView`, `RegisterView`, `TeamSetupView` | Log-in, sign-up, and "enter your FPL team ID" forms. |
| `SquadView` | **Squad** tab: your current gameweek's team with real points. |
| `BestXIView` | **Your Best XI** tab: the suggested line-up, with tap-to-sub and captain buttons. A what-if sandbox — nothing is saved. |
| `TransfersView` | **Transfers** tab: the upgrade suggestions, with "Value to team" (+ = frees money). |
| `BestGwXIView` | **Best GW{N} XI** tab: the best £100m squad, its value, bank, and formation. |
| `PitchView`, `PitchMarkings` | The pitch, its lines, and the player card used by every tab. |

---

## 5. Where the data lives — and what changes when we host

Right now, **everything runs on your PC**: the backend (`dotnet run`), the frontend (`npm run dev`), and the database (PostgreSQL inside Docker). Turn the PC off and the app is off.

The app **already uses PostgreSQL**, so hosting doesn't need a new kind of database — it needs a **PostgreSQL running on a server, alongside the hosted backend**, instead of on your PC. Points to plan for when we get there:

| Topic | Why it matters |
|---|---|
| **Which data is precious** | **User accounts** and **pre-deadline predictions** can't be recreated (section 1.2). Results can be re-graded from FPL. So: **automatic backups** from day one. |
| **Moving existing data** | Decide whether to copy your local database up (keeps GW3/GW8 history) or start fresh. |
| **Settings** | The database password and `AllowedOrigin` must change to real, secret, production values — never committed to GitHub. |
| **Migrations** | The same `dotnet ef database update` step must run against the hosted database whenever the layout changes. |
| **Nobody opens the app for a week** | Predictions are only built when someone opens the app. If no one does before a deadline, that gameweek's predictions never exist and can't be graded honestly. A hosted backend should build them **on a schedule** (e.g. daily) rather than waiting for a visitor. |
| **The frontend switches over** | Once the backend is live, the Vercel site can finally be updated to v2 (merging `develop` into `main`). |

---

## Glossary

| Word | Meaning |
|---|---|
| **API / endpoint** | A program's "front desk": an address you send a request to and get data back from. |
| **JSON** | Plain-text format for structured data: `{"name": "Haaland", "price": 15.7}`. |
| **Request / response** | The question your browser sends, and the answer it gets back. |
| **Status code** | A number on every response: **200** OK, **401** not logged in, **403** refused, **404** not found, **500** the backend crashed. |
| **Cookie** | A small pass your browser keeps and shows on every request — how the backend knows it's you. |
| **Database / PostgreSQL** | Software that stores data in organised sheets with fixed columns, and finds things fast. |
| **Docker / container** | A sealed mini-computer running inside yours, with one program set up in it (here, the database). |
| **Migration** | A recorded change to the database's layout, applied in order. |
| **Port** | A numbered door on a computer. Frontend = 5173, backend = 5251, database = 5433. |
| **xG / xA** | Expected goals / assists — how many a player "should" have, given the chances he got. |
| **xP** | Expected points — this app's forecast of a player's FPL points for one gameweek. |
| **MAE** | Mean absolute error — average size of the prediction mistakes, in points. |
| **Branch / merge / PR** | Git's way of working on a copy (branch), proposing it (pull request), and folding it into the main line (merge). |
