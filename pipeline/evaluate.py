"""
compares predicted vs. actual points for a finished gameweek, 
logs how the formula performed
"""

import csv
import json
from datetime import datetime, timezone
from pathlib import Path

from pull_data import fetch_json, save_raw

DATA_DIR = Path(__file__).parent.parent / "data"
LOG_PATH = DATA_DIR / "model_performance_log.csv"

def load_predictions(target_gw):
    with open(DATA_DIR / f"gw{target_gw}_predictions.json", encoding="utf-8") as f:
        return json.load(f)

def fetch_actual_points(target_gw):
    url = f"https://fantasy.premierleague.com/api/event/{target_gw}/live/"
    data = fetch_json(url)
    save_raw(data, f"gw{target_gw}_live.json")
    return {e['id']: e['stats']['total_points'] for e in data['elements']}

def compare(predictions, actual_points):
    results = []
    for player in predictions:
        actual = actual_points.get(player['id'], 0)
        difference = player['xP'] - actual
        results.append({
            'id': player['id'],
            'name': player['name'],
            'position': player['position'],
            'team': player['team'],
            'predicted_points': player['xP'],
            'actual_points': actual,
            'difference': difference
        })
    return results

def save_results(results, target_gw):
    output_path = DATA_DIR / f"gw{target_gw}_results.json"
    with open(output_path, "w", encoding="utf-8") as f:
        json.dump(results, f, indent=2)

def log_performance(results, target_gw):
    mean_abs_error = sum(abs(r['difference']) for r in results) / len(results)

    file_exists = LOG_PATH.exists()
    with open(LOG_PATH, "a", encoding="utf-8", newline="") as f:
        writer = csv.writer(f)
        if not file_exists:
            writer.writerow(['gw', 'evaluated_at', 'num_players', 'mean_absolute_error'])
        writer.writerow([target_gw, datetime.now(timezone.utc).isoformat(), len(results), round(mean_abs_error, 2)])

if __name__ == "__main__":
    predictions = load_predictions(1)
    actual = fetch_actual_points(1)
    results = compare(predictions, actual)
    save_results(results, target_gw=1)
    log_performance(results, target_gw=1)
    print(f"Evaluated {len(results)} players for GW1.")