"""
This is a deliberate duplicate of the prediction logic in pipeline/build_features.py,
pipeline/predict.py, and pipeline/evaluate.py — NOT an import of them.

Vercel only reliably bundles files that live under this function's own
directory (frontend/api/), so reaching across to pipeline/ with a relative
import would be fragile. Instead, the same tested formula/feature logic is
copied here, adapted to read/write Neon instead of local JSON files.

If the xP formula or its weights ever change, both copies need updating —
pipeline/ (the manual local CLI, still usable standalone) and this one
(what the deployed app actually runs). That tradeoff is intentional, not
an oversight.
"""

from datetime import datetime, timezone

import requests

BOOTSTRAP_URL = "https://fantasy.premierleague.com/api/bootstrap-static/"
FIXTURES_URL = "https://fantasy.premierleague.com/api/fixtures/"

FORM_WEIGHT = 0.40
XG_XA_WEIGHT = 0.25
FIXTURE_WEIGHT = 0.20
MINUTES_WEIGHT = 0.15

STALE_AFTER_HOURS = 6


# ---- Fetching from the official FPL API (same endpoints pull_data.py uses) ----

def fetch_json(url: str):
    response = requests.get(url, timeout=15)
    response.raise_for_status()
    return response.json()


def fetch_bootstrap():
    return fetch_json(BOOTSTRAP_URL)


def fetch_fixtures():
    return fetch_json(FIXTURES_URL)


def fetch_actual_points(target_gw: int) -> dict:
    """{player_id: points} for one finished gameweek."""
    data = fetch_json(f"https://fantasy.premierleague.com/api/event/{target_gw}/live/")
    return {e["id"]: e["stats"]["total_points"] for e in data["elements"]}


def fetch_squad_picks(team_id: int, target_gw: int) -> dict:
    """One FPL team's picks for a gameweek: which players, captain, bench order, bank/value."""
    return fetch_json(f"https://fantasy.premierleague.com/api/entry/{team_id}/event/{target_gw}/picks/")


def fetch_entry_info(team_id: int) -> dict:
    """Basic info for one FPL team ID — used only to check the ID actually exists before saving it."""
    return fetch_json(f"https://fantasy.premierleague.com/api/entry/{team_id}/")


# ---- Feature building (ported from pipeline/build_features.py) ----

def build_lookups(bootstrap):
    position_names = {et["id"]: et["singular_name"] for et in bootstrap["element_types"]}
    teams_by_id = {t["id"]: t for t in bootstrap["teams"]}
    return position_names, teams_by_id


def minutes_reliability(player):
    return player["minutes"] / 3420


def xg90_and_xa90(player):
    return player["expected_goals_per_90"] + player["expected_assists_per_90"]


def recent_form(player, previous_gw_points):
    if previous_gw_points is None:
        return float(player["points_per_game"])
    return previous_gw_points.get(player["id"], 0)


def fixture_difficulty(player, fixtures, target_gw):
    team = player["team"]
    for f in fixtures:
        if f["event"] == target_gw and f["team_h"] == team:
            return 6 - f["team_h_difficulty"]
        if f["event"] == target_gw and f["team_a"] == team:
            return 6 - f["team_a_difficulty"]


def build_player_features(player, position_names, teams_by_id, fixtures, target_gw, previous_gw_points):
    team_info = teams_by_id[player["team"]]
    return {
        "id": player["id"],
        "name": player["web_name"],
        "position": position_names[player["element_type"]],
        "team": team_info["short_name"],
        "price": player["now_cost"] / 10,
        "recent_form": recent_form(player, previous_gw_points),
        "xa_xg_per_90": xg90_and_xa90(player),
        "fixture_difficulty": fixture_difficulty(player, fixtures, target_gw),
        "minutes_reliability": minutes_reliability(player),
    }


def build_features(bootstrap, fixtures, target_gw, previous_gw_points):
    position_names, teams_by_id = build_lookups(bootstrap)
    return [
        build_player_features(player, position_names, teams_by_id, fixtures, target_gw, previous_gw_points)
        for player in bootstrap["elements"]
    ]


# ---- xP formula (ported from pipeline/predict.py) ----

def find_min_max(features, feature_name):
    values = [player[feature_name] for player in features]
    return min(values), max(values)


def rescale(value, minimum, maximum):
    return (value - minimum) / (maximum - minimum)


def calculate_xp(features):
    form_min, form_max = find_min_max(features, "recent_form")
    xga_min, xga_max = find_min_max(features, "xa_xg_per_90")
    fix_dif_min, fix_dif_max = find_min_max(features, "fixture_difficulty")
    mins_rel_min, mins_rel_max = find_min_max(features, "minutes_reliability")

    results = []
    for player in features:
        form_scaled = rescale(player["recent_form"], form_min, form_max)
        xga_scaled = rescale(player["xa_xg_per_90"], xga_min, xga_max)
        fix_dif_scaled = rescale(player["fixture_difficulty"], fix_dif_min, fix_dif_max)
        mins_rel_scaled = rescale(player["minutes_reliability"], mins_rel_min, mins_rel_max)

        xp = (
            form_scaled * FORM_WEIGHT
            + xga_scaled * XG_XA_WEIGHT
            + fix_dif_scaled * FIXTURE_WEIGHT
            + mins_rel_scaled * MINUTES_WEIGHT
        )

        results.append({**player, "xP": round(xp, 2)})

    return results


# ---- Evaluation (ported from pipeline/evaluate.py) ----

def compare(predictions, actual_points):
    results = []
    for player in predictions:
        actual = actual_points.get(player["id"], 0)
        difference = player["xP"] - actual
        results.append({
            "id": player["id"],
            "name": player["name"],
            "position": player["position"],
            "team": player["team"],
            "predicted_points": player["xP"],
            "actual_points": actual,
            "difference": difference,
        })
    return results


# ---- Neon reads/writes (replaces the old data/*.json files) ----

def save_predictions(conn, gw: int, predictions: list) -> None:
    with conn.cursor() as cur:
        cur.execute("DELETE FROM predictions WHERE gw = %s", (gw,))
        for p in predictions:
            cur.execute(
                """
                INSERT INTO predictions
                    (gw, player_id, name, position, team, price,
                     recent_form, xa_xg_per_90, fixture_difficulty, minutes_reliability, xp)
                VALUES (%s, %s, %s, %s, %s, %s, %s, %s, %s, %s, %s)
                """,
                (
                    gw, p["id"], p["name"], p["position"], p["team"], p["price"],
                    p["recent_form"], p["xa_xg_per_90"], p["fixture_difficulty"], p["minutes_reliability"], p["xP"],
                ),
            )
        cur.execute(
            """
            INSERT INTO prediction_runs (gw, last_refreshed_at, player_count)
            VALUES (%s, now(), %s)
            ON CONFLICT (gw) DO UPDATE SET last_refreshed_at = now(), player_count = EXCLUDED.player_count
            """,
            (gw, len(predictions)),
        )
    conn.commit()


def load_predictions(conn, gw: int) -> list:
    """Returns rows already shaped like the frontend's Player type (id, name, position, team, price, xP)."""
    with conn.cursor() as cur:
        cur.execute(
            """
            SELECT player_id AS id, name, position, team, price, xp AS "xP"
            FROM predictions WHERE gw = %s ORDER BY xp DESC
            """,
            (gw,),
        )
        return cur.fetchall()


def get_last_refresh(conn, gw: int):
    with conn.cursor() as cur:
        cur.execute("SELECT last_refreshed_at, player_count FROM prediction_runs WHERE gw = %s", (gw,))
        return cur.fetchone()


def is_stale(last_refresh) -> bool:
    if last_refresh is None:
        return True
    age = datetime.now(timezone.utc) - last_refresh["last_refreshed_at"]
    return age.total_seconds() > STALE_AFTER_HOURS * 3600


def has_been_evaluated(conn, gw: int) -> bool:
    with conn.cursor() as cur:
        cur.execute("SELECT 1 FROM model_performance_log WHERE gw = %s", (gw,))
        return cur.fetchone() is not None


def save_results(conn, gw: int, results: list) -> None:
    with conn.cursor() as cur:
        cur.execute("DELETE FROM results WHERE gw = %s", (gw,))
        for r in results:
            cur.execute(
                """
                INSERT INTO results (gw, player_id, name, position, team, predicted_points, actual_points, difference)
                VALUES (%s, %s, %s, %s, %s, %s, %s, %s)
                """,
                (gw, r["id"], r["name"], r["position"], r["team"], r["predicted_points"], r["actual_points"], r["difference"]),
            )
    conn.commit()


def log_performance(conn, gw: int, results: list) -> None:
    mean_abs_error = sum(abs(r["difference"]) for r in results) / len(results)
    with conn.cursor() as cur:
        cur.execute(
            """
            INSERT INTO model_performance_log (gw, evaluated_at, num_players, mean_absolute_error)
            VALUES (%s, now(), %s, %s)
            ON CONFLICT (gw) DO NOTHING
            """,
            (gw, len(results), round(mean_abs_error, 2)),
        )
    conn.commit()


def build_squad(conn, team_id: int, gw: int) -> dict:
    """Join one user's live FPL picks against this gameweek's cached predictions."""
    squad_data = fetch_squad_picks(team_id, gw)
    predictions_by_id = {p["id"]: p for p in load_predictions(conn, gw)}

    picks = []
    for pick in squad_data["picks"]:
        player = predictions_by_id.get(pick["element"])
        if player is None:
            continue
        picks.append({
            **player,
            "squad_position": pick["position"],
            "is_captain": pick["is_captain"],
            "is_vice_captain": pick["is_vice_captain"],
            "multiplier": pick["multiplier"],
        })

    return {
        "gameweek": gw,
        "bank": squad_data["entry_history"]["bank"] / 10,
        "team_value": squad_data["entry_history"]["value"] / 10,
        "picks": picks,
    }
