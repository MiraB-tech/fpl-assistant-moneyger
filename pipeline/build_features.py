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

def recent_form(player):
    form = float(player['form'])
    if form > 0:
        return form
    
    return float(player['points_per_game'])

def fixture_difficulty(player, fixtures, target_gw):
    team = player['team']
    for f in fixtures:
        if f['event'] == target_gw and f['team_h'] == team:
            return 6 - f['team_h_difficulty']
        if f['event'] == target_gw and f['team_a'] == team:
            return 6 - f['team_a_difficulty']
    
def build_player_features(player, position_names, teams_by_id, fixtures, target_gw):
    team_info = teams_by_id[player['team']]

    return {
        'id': player['id'],
        'name': player['web_name'],
        'position': position_names[player['element_type']],
        'team': team_info['short_name'],
        'price': player['now_cost'] / 10,
        'recent_form': recent_form(player),
        'xa_xg_per_90': xg90_and_xa90(player),
        'fixture_difficulty': fixture_difficulty(player, fixtures, target_gw),
        'minutes_reliability': minutes_reliability(player),
    }


def build_features(target_gw):
    bootstrap = load("bootstrap_static.json")
    fixtures = load("fixtures.json")
    position_names, teams_by_id = build_lookups(bootstrap)

    features = []
    for player in bootstrap["elements"]:
        features.append(build_player_features(player, position_names, teams_by_id, fixtures, target_gw))

    return features


if __name__ == "__main__":
    features = build_features(target_gw=1)
    print(f"Built features for {len(features)} players.")
    print("Sample (first player):")
    print(features[0])