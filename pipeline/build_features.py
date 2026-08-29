"""
Step 2 of the pipeline: take the raw data we pulled in pull_data.py and build a
set of features that we can use in the transparent weighted formula.
The features we make in build_features.py are based on real data, not guesses.

The script doesn't try to be clever about the data yet — no maths.

Run it with:
    ./.venv/Scripts/python.exe build_features.py
"""

import json
from pathlib import Path

from pull_data import fetch_actual_points

RAW_DATA_DIR = Path(__file__).parent.parent / "data" / "raw"

def load(filename: str):
    with open(RAW_DATA_DIR / filename, encoding="utf-8") as f:
        return json.load(f)

def build_lookups(bootstrap):
    position_names = {et["id"]: et["singular_name"] for et in bootstrap["element_types"]}
    teams_by_id = {t["id"]: t for t in bootstrap['teams']}
    return position_names, teams_by_id

def minutes_reliability(player):
    mins_reliability = player['minutes']/3420
    return mins_reliability

def  xg90_and_xa90(player):
    attacking_threat  = player['expected_goals_per_90'] + player['expected_assists_per_90']
    return attacking_threat

def recent_form(player, previous_gw_points):
    """
    How well has this player actually done lately? Based on real points
    scored in the most recently *finished* gameweek, not FPL's own live
    'form' field, which keeps updating mid-gameweek and would leak
    already-played matches from the gameweek we're trying to predict.

    previous_gw_points is a {player_id: points} lookup for that finished
    gameweek, or None if there isn't one yet (e.g. we're building
    features for GW1, before any gameweek has been played) — in that
    case we fall back to the player's season-long points-per-game average,
    the best guess available before any real form exists.
    """
    if previous_gw_points is None:
        return float(player['points_per_game'])

    return previous_gw_points.get(player['id'], 0)

def fixture_difficulty(player, fixtures, target_gw):
    team = player['team']
    for f in fixtures:
        if f['event'] == target_gw and f['team_h'] == team:
            return 6 - f['team_h_difficulty']
        if f['event'] == target_gw and f['team_a'] == team:
            return 6 - f['team_a_difficulty']
    
def build_player_features(player, position_names, teams_by_id, fixtures, target_gw, previous_gw_points):
    team_info = teams_by_id[player['team']]

    return {
        'id': player['id'],
        'name': player['web_name'],
        'position': position_names[player['element_type']],
        'team': team_info['short_name'],
        'price': player['now_cost'] / 10,
        'recent_form': recent_form(player, previous_gw_points),
        'xa_xg_per_90': xg90_and_xa90(player),
        'fixture_difficulty': fixture_difficulty(player, fixtures, target_gw),
        'minutes_reliability': minutes_reliability(player),
    }


def build_features(target_gw):
    bootstrap = load("bootstrap_static.json")
    fixtures = load("fixtures.json")
    position_names, teams_by_id = build_lookups(bootstrap)

    # recent_form needs to look at the gameweek right before the one we're
    # predicting — e.g. building GW2 features means checking GW1's real
    # results. If we're predicting GW1 itself, there's no earlier finished
    # gameweek to check yet, so previous_gw_points stays None.
    previous_gw = target_gw - 1
    previous_gw_points = fetch_actual_points(previous_gw) if previous_gw >= 1 else None

    features = []
    for player in bootstrap["elements"]:
        features.append(build_player_features(player, position_names, teams_by_id, fixtures, target_gw, previous_gw_points))

    return features


if __name__ == "__main__":
    features = build_features(target_gw=1)
    print(f"Built features for {len(features)} players.")
    print("Sample (first player):")
    print(features[0])