"""
A "look before you touch" tool. Before we start turning raw data into
features and predictions, it's worth actually seeing what pull_data.py
saved — how many players, what a player record looks like, what a
gameweek looks like, what a fixture looks like — so the choices we
make in build_features.py are based on real data, not guesses.

This script doesn't change anything. It only reads what's already in
data/raw/ and prints a human-readable summary.

Run it with:
    ./.venv/Scripts/python.exe peek_data.py
"""

import json
from pathlib import Path

RAW_DATA_DIR = Path(__file__).parent.parent / "data" / "raw"


def load(filename: str):
    with open(RAW_DATA_DIR / filename, encoding="utf-8") as f:
        return json.load(f)


def section(title: str):
    print(f"\n--- {title} ---")


def main():
    metadata = load("pull_metadata.json")
    bootstrap = load("bootstrap_static.json")
    fixtures = load("fixtures.json")

    section("When was this data pulled?")
    print(metadata["pulled_at"])

    # element_types are the 4 playing positions (Goalkeeper, Defender,
    # Midfielder, Forward). Every player has an element_type number
    # (1-4) instead of a spelled-out position, so we build a lookup
    # table to translate the numbers into names we can actually read.
    position_names = {et["id"]: et["singular_name"] for et in bootstrap["element_types"]}

    section("Gameweeks (events)")
    events = bootstrap["events"]
    current = next((e for e in events if e["is_current"]), None)
    next_gw = next((e for e in events if e["is_next"]), None)
    print(f"Total gameweeks in season: {len(events)}")
    current_label = current["name"] if current else "none yet - season hasn't started"
    print(f"Current gameweek: {current_label}")
    print(f"Next gameweek: {next_gw['name']}, deadline {next_gw['deadline_time']}")

    section("Teams")
    teams = bootstrap["teams"]
    print(f"{len(teams)} teams. First 5, with FPL's own strength ratings:")
    for t in teams[:5]:
        print(f"  {t['name']:<18} home strength={t['strength_overall_home']}  "
              f"away strength={t['strength_overall_away']}")

    section("Players")
    players = bootstrap["elements"]
    print(f"{len(players)} players total.")
    counts_by_position = {}
    for p in players:
        pos = position_names[p["element_type"]]
        counts_by_position[pos] = counts_by_position.get(pos, 0) + 1
    for pos, count in counts_by_position.items():
        print(f"  {pos}: {count}")

    print("\nOne real player record, with the fields our formula will care about:")
    sample = players[0]
    print(f"  Name: {sample['first_name']} {sample['second_name']} ({sample['web_name']})")
    print(f"  Position: {position_names[sample['element_type']]}")
    print(f"  Price: £{sample['now_cost'] / 10}m")
    print(f"  Minutes played (last season): {sample['minutes']}")
    print(f"  Form (FPL's own recent-form score): {sample['form']}  <- 0.0 because the new season hasn't kicked off yet")
    print(f"  Points per game (last season): {sample['points_per_game']}")
    print(f"  Expected goals per 90 mins: {sample['expected_goals_per_90']}")
    print(f"  Expected assists per 90 mins: {sample['expected_assists_per_90']}")

    section("Fixtures")
    print(f"{len(fixtures)} fixtures in the full season (20 teams x 38 gameweeks / 2).")
    gw1_fixtures = [f for f in fixtures if f["event"] == 1]
    team_names = {t["id"]: t["short_name"] for t in teams}
    print(f"Gameweek 1 has {len(gw1_fixtures)} matches:")
    for f in gw1_fixtures:
        home = team_names[f["team_h"]]
        away = team_names[f["team_a"]]
        print(f"  {home} vs {away}  (kickoff {f['kickoff_time']})")


if __name__ == "__main__":
    main()
