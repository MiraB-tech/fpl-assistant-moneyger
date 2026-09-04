"""
run_gameweek.py — the one script you run at the end of each gameweek.

It does two jobs, one after the other:
  1. Evaluates the gameweek that just finished (compares what we
     predicted against what actually happened) — but only if we
     actually saved a prediction for it. If not, it skips this step
     instead of crashing.
  2. Pulls fresh data from the FPL API and generates predictions for
     the *next* gameweek.

Usage — pass in the number of the gameweek you want predictions for:
    ./.venv/Scripts/python.exe run_gameweek.py <next_gw_number>

Example: once GW1 has finished, run:
    ./.venv/Scripts/python.exe run_gameweek.py 2
This evaluates GW1 (using the gw1_predictions.json we made earlier)
and then pulls fresh data and builds predictions for GW2.
"""

import sys

# All of this is reuse — we're not teaching Python anything new here,
# just gathering functions we already wrote and tested in their own
# files into one place, so one script can call all of them in order.
from pull_data import main as pull_raw_data
from build_features import build_features
from predict import DATA_DIR, calculate_xp, save_predictions
from evaluate import load_predictions, fetch_actual_points, compare, save_results, log_performance


def evaluate_previous_gameweek(gw):
    """
    Grade the gameweek that just finished, if we made a prediction for it.

    `gw` is the gameweek number to check (e.g. 1). There are two cases
    where there's nothing to grade, and both are handled the same way —
    print a short note and return early instead of letting the script
    crash:
      - `gw` is less than 1 (there's no "gameweek 0" before the season
        starts, so the very first time you ever run this script there's
        nothing earlier to evaluate).
      - We never actually ran predict.py for that gameweek, so its
        predictions file doesn't exist on disk.
    """
    if gw < 1:
        print(f"No gameweek {gw} to evaluate — skipping.")
        return

    predictions_path = DATA_DIR / f"gw{gw}_predictions.json"
    if not predictions_path.exists():
        print(f"No saved predictions found for GW{gw} — skipping evaluation.")
        return

    # From here it's the exact same four steps evaluate.py's own
    # __main__ block runs — load what we predicted, fetch what actually
    # happened, compare them, then save the comparison and log a summary.
    predictions = load_predictions(gw)
    actual_points = fetch_actual_points(gw)
    results = compare(predictions, actual_points)
    save_results(results, target_gw=gw)
    log_performance(results, target_gw=gw)
    print(f"Evaluated {len(results)} players for GW{gw}.")


def predict_next_gameweek(gw):
    """
    Build fresh predictions for the upcoming gameweek `gw`.

    This re-runs the same three steps you've done by hand up to now:
    pull the latest data from the FPL API (form, prices, fixtures all
    change day to day, so we want a fresh pull, not the old saved copy),
    turn that raw data into features, then turn those features into an
    xP prediction for every player.
    """
    pull_raw_data()
    features = build_features(gw)
    predictions = calculate_xp(features)
    save_predictions(predictions, target_gw=gw)
    print(f"Saved predictions for {len(predictions)} players for GW{gw}.")


if __name__ == "__main__":
    # sys.argv is the list of words you typed on the command line.
    # sys.argv[0] is always the script's own name; sys.argv[1] is the
    # first thing you typed after it — e.g. the "2" in
    # "python.exe run_gameweek.py 2". It arrives as text ("2"), so
    # int() turns it into a real number before we do maths with it.
    next_gw = int(sys.argv[1])

    evaluate_previous_gameweek(next_gw - 1)
    predict_next_gameweek(next_gw)
