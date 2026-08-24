"""
Step 1 of the pipeline: go get the raw data we need and save it to disk.

Today this pulls straight from the official, free, no-login FPL API.
It doesn't try to be clever about the data yet — no maths, no picking
which stats matter. That comes later, in build_features.py. This
script's only job is "fetch what's out there right now and keep an
untouched copy of it", so that everything downstream always has a
known, saved starting point to work from (and we can re-run the rest
of the pipeline without hitting the API again).

Run it with:
    ./.venv/Scripts/python.exe pull_data.py
"""

import json
from datetime import datetime, timezone
from pathlib import Path

import requests

# The two FPL endpoints we need for now:
#  - bootstrap-static: every player, every team, every gameweek — the
#    "master list" the whole game is built on.
#  - fixtures: the full season's match schedule (who plays who, and when).
BOOTSTRAP_URL = "https://fantasy.premierleague.com/api/bootstrap-static/"
FIXTURES_URL = "https://fantasy.premierleague.com/api/fixtures/"

# Where the untouched downloads get saved. "raw" means: exactly what the
# API gave us, no edits — so if something looks wrong two steps later,
# we can always come back here and check whether the problem started
# with the source data or with our own code.
RAW_DATA_DIR = Path(__file__).parent.parent / "data" / "raw"


def fetch_json(url: str) -> dict | list:
    """Ask the API for data and hand back the parsed JSON, or blow up loudly if it fails."""
    response = requests.get(url, timeout=15)
    response.raise_for_status()  # if the API returned an error, stop here instead of saving junk
    return response.json()


def save_raw(data, filename: str) -> Path:
    """Write data to data/raw/<filename> as nicely formatted JSON."""
    RAW_DATA_DIR.mkdir(parents=True, exist_ok=True)
    filepath = RAW_DATA_DIR / filename
    with open(filepath, "w", encoding="utf-8") as f:
        json.dump(data, f, indent=2, ensure_ascii=False)
    return filepath


def main():
    pulled_at = datetime.now(timezone.utc).isoformat()
    print(f"Pulling FPL data at {pulled_at}...")

    bootstrap = fetch_json(BOOTSTRAP_URL)
    bootstrap_path = save_raw(bootstrap, "bootstrap_static.json")
    print(f"  Saved {len(bootstrap['elements'])} players, "
          f"{len(bootstrap['teams'])} teams, "
          f"{len(bootstrap['events'])} gameweeks -> {bootstrap_path}")

    fixtures = fetch_json(FIXTURES_URL)
    fixtures_path = save_raw(fixtures, "fixtures.json")
    print(f"  Saved {len(fixtures)} fixtures -> {fixtures_path}")

    # A tiny note-to-self file recording *when* this pull happened. Player
    # form, prices, and injury status all change day to day, so knowing
    # the age of our raw data matters just as much as the data itself.
    save_raw({"pulled_at": pulled_at}, "pull_metadata.json")

    print("Done.")


if __name__ == "__main__":
    main()
