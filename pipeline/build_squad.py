"""
Pulls your actual FPL squad for a given gameweek (which 15 players you own,
who's captain/vice-captain, bench order, bank and team value) and merges it
with that gameweek's predictions, so the frontend gets one file with
everything it needs: name, position, team, price, xP, and squad role.

Run it with:
    ./.venv/Scripts/python.exe build_squad.py <gw>

Example: ./.venv/Scripts/python.exe build_squad.py 2
"""

import json
import sys
from pathlib import Path

from pull_data import fetch_squad

DATA_DIR = Path(__file__).parent.parent / "data"

# Your FPL team ID, from the URL when you view your team on the FPL site:
# fantasy.premierleague.com/entry/<this number>/event/...
TEAM_ID = 5254189


def load_predictions(target_gw):
    with open(DATA_DIR / f"gw{target_gw}_predictions.json", encoding="utf-8") as f:
        return json.load(f)


def build_squad(target_gw):
    squad_data = fetch_squad(TEAM_ID, target_gw)
    predictions_by_id = {player['id']: player for player in load_predictions(target_gw)}

    picks = []
    for pick in squad_data['picks']:
        player = predictions_by_id.get(pick['element'])
        if player is None:
            # Shouldn't normally happen — every owned player should be in
            # that gameweek's predictions — but skip rather than crash if
            # the data ever gets out of sync.
            continue
        picks.append({
            **player,
            'squad_position': pick['position'],
            'is_captain': pick['is_captain'],
            'is_vice_captain': pick['is_vice_captain'],
            'multiplier': pick['multiplier'],
        })

    # FPL reports bank/value in tenths of a million (e.g. 1003 = £100.3m).
    return {
        'gameweek': target_gw,
        'bank': squad_data['entry_history']['bank'] / 10,
        'team_value': squad_data['entry_history']['value'] / 10,
        'picks': picks,
    }


def save_squad(squad):
    output_path = DATA_DIR / "my_squad.json"
    with open(output_path, "w", encoding="utf-8") as f:
        json.dump(squad, f, indent=2)


if __name__ == "__main__":
    target_gw = int(sys.argv[1])
    squad = build_squad(target_gw)
    save_squad(squad)
    print(f"Saved squad for GW{target_gw}: {len(squad['picks'])} players.")
