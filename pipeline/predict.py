"""
step 3 pipeline: 
takes the features from build_features.py, normalises them, 
applies the xP formula, saves the result.
"""

import json
from pathlib import Path
from build_features import build_features

DATA_DIR = Path(__file__).parent.parent / "data"

FORM_WEIGHT = 0.40
XG_XA_WEIGHT = 0.25
FIXTURE_WEIGHT = 0.20
MINUTES_WEIGHT = 0.15

def find_min_max(features, feature_name):
    values = [player[feature_name] for player in features]
    return min(values), max(values)

def rescale(value, minimum, maximum):
    return (value - minimum) / (maximum - minimum)

def calculate_xp(features):
    form_min, form_max = find_min_max(features, 'recent_form')
    xga_min, xga_max = find_min_max(features, 'xa_xg_per_90')
    fix_dif_min, fix_dif_max = find_min_max(features, 'fixture_difficulty')
    mins_rel_min, mins_rel_max = find_min_max(features, 'minutes_reliability')

    results = []
    for player in features:
        form_scaled = rescale(player['recent_form'],form_min, form_max)
        xga_scaled = rescale(player['xa_xg_per_90'],xga_min, xga_max)
        fix_dif_scaled = rescale(player['fixture_difficulty'],fix_dif_min, fix_dif_max)
        mins_rel_scaled = rescale(player['minutes_reliability'],mins_rel_min, mins_rel_max)

        xp = (form_scaled * FORM_WEIGHT
            + xga_scaled * XG_XA_WEIGHT
            + fix_dif_scaled * FIXTURE_WEIGHT
            + mins_rel_scaled * MINUTES_WEIGHT)

        results.append({
            'id': player['id'], 
            'name': player['name'], 
            'position': player['position'], 
            'team': player['team'], 
            'price': player['price'], 
            'xP': round(xp, 2)
        })

    return results

def save_predictions(predictions, target_gw):
    output_path = DATA_DIR / f"gw{target_gw}_predictions.json"
    with open(output_path, "w", encoding="utf-8") as f:
        json.dump(predictions, f, indent=2)

if __name__=="__main__":
    features = build_features(target_gw=1)
    predictions = calculate_xp(features)
    save_predictions(predictions, target_gw=1)
    print(f"Saved predictions for {len(predictions)} players")