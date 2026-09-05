-- The Assistant Moneyger — multi-user platform schema (Neon / Postgres).
-- Idempotent: safe to re-run. Apply once via Neon's own SQL Editor
-- (or `psql $DATABASE_URL -f db/schema.sql` if you have psql installed).

CREATE TABLE IF NOT EXISTS users (
  id            SERIAL PRIMARY KEY,
  email         TEXT UNIQUE NOT NULL,
  password_hash TEXT NOT NULL,
  fpl_team_id   INTEGER,
  created_at    TIMESTAMPTZ NOT NULL DEFAULT now()
);

-- Session tokens are stored hashed (sha256), never in raw/usable form —
-- same principle as password storage, applied to auth tokens.
CREATE TABLE IF NOT EXISTS sessions (
  token_hash TEXT PRIMARY KEY,
  user_id    INTEGER NOT NULL REFERENCES users(id) ON DELETE CASCADE,
  created_at TIMESTAMPTZ NOT NULL DEFAULT now(),
  expires_at TIMESTAMPTZ NOT NULL
);
CREATE INDEX IF NOT EXISTS idx_sessions_user_id ON sessions(user_id);

-- Global, shared by every user — one row per player per gameweek.
-- Mirrors what used to be data/gw{N}_predictions.json.
CREATE TABLE IF NOT EXISTS predictions (
  gw                   INTEGER NOT NULL,
  player_id            INTEGER NOT NULL,
  name                 TEXT NOT NULL,
  position             TEXT NOT NULL,
  team                 TEXT NOT NULL,
  price                NUMERIC NOT NULL,
  recent_form          NUMERIC,
  xa_xg_per_90         NUMERIC,
  fixture_difficulty   NUMERIC,
  minutes_reliability  NUMERIC,
  xp                   NUMERIC NOT NULL,
  computed_at          TIMESTAMPTZ NOT NULL DEFAULT now(),
  PRIMARY KEY (gw, player_id)
);

-- Mirrors what used to be data/gw{N}_results.json.
CREATE TABLE IF NOT EXISTS results (
  gw                INTEGER NOT NULL,
  player_id         INTEGER NOT NULL,
  name              TEXT NOT NULL,
  position          TEXT NOT NULL,
  team              TEXT NOT NULL,
  predicted_points  NUMERIC NOT NULL,
  actual_points     NUMERIC NOT NULL,
  difference        NUMERIC NOT NULL,
  evaluated_at      TIMESTAMPTZ NOT NULL DEFAULT now(),
  PRIMARY KEY (gw, player_id)
);

-- Mirrors what used to be data/model_performance_log.csv — one row per gw.
CREATE TABLE IF NOT EXISTS model_performance_log (
  gw                   INTEGER PRIMARY KEY,
  evaluated_at         TIMESTAMPTZ NOT NULL,
  num_players          INTEGER NOT NULL,
  mean_absolute_error  NUMERIC NOT NULL
);

-- Answers "when was gw N last refreshed?" — and doubles as a free,
-- global rate limiter: every user's refresh button checks this before
-- doing any real work, so the expensive FPL pull + recompute can only
-- actually run once per staleness window, no matter how many users click.
CREATE TABLE IF NOT EXISTS prediction_runs (
  gw                 INTEGER PRIMARY KEY,
  last_refreshed_at  TIMESTAMPTZ NOT NULL,
  player_count       INTEGER NOT NULL
);
