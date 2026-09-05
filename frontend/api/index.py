"""
The whole backend API, as one Flask app. Vercel deploys this file as a
single Python serverless function; frontend/vercel.json rewrites every
/api/* request to it, and Flask's own routing (below) picks the right
handler from the real path. One app instead of one function per endpoint
avoids a separate cold start and a separate DB-connection setup per route.
"""

from datetime import datetime, timezone

import requests
from flask import Flask, g, jsonify, make_response, request

from _lib import pipeline_core
from _lib.auth import (
    SESSION_COOKIE_NAME,
    clear_session_cookie,
    create_session,
    delete_session,
    hash_password,
    require_auth,
    set_session_cookie,
    verify_password,
)
from _lib.db import get_connection

app = Flask(__name__)


@app.before_request
def check_csrf():
    """
    Cheap defence against cross-site form/fetch submissions: for any
    request that changes something, the browser-sent Origin header must
    match this site. SameSite=Lax on the session cookie is the main
    defence; this is a second, low-cost check on top of it.
    """
    if request.method in ("POST", "PUT", "DELETE"):
        origin = request.headers.get("Origin")
        if origin is not None and origin != f"{request.scheme}://{request.host}":
            return jsonify({"error": "cross-site request blocked"}), 403


@app.route("/api/health", methods=["GET"])
def health():
    return jsonify({"status": "ok"})


# ---- Auth ----

@app.route("/api/auth/register", methods=["POST"])
def register():
    body = request.get_json(silent=True) or {}
    email = (body.get("email") or "").strip().lower()
    password = body.get("password") or ""
    fpl_team_id = body.get("fpl_team_id")

    if "@" not in email:
        return jsonify({"error": "a valid email is required"}), 400
    if len(password) < 8:
        return jsonify({"error": "password must be at least 8 characters"}), 400

    if fpl_team_id is not None:
        try:
            fpl_team_id = int(fpl_team_id)
        except (TypeError, ValueError):
            return jsonify({"error": "fpl_team_id must be a number"}), 400
        try:
            pipeline_core.fetch_entry_info(fpl_team_id)
        except requests.HTTPError:
            return jsonify({"error": "that FPL team ID doesn't seem to exist"}), 400

    conn = get_connection()
    try:
        with conn.cursor() as cur:
            cur.execute("SELECT 1 FROM users WHERE email = %s", (email,))
            if cur.fetchone() is not None:
                return jsonify({"error": "an account with that email already exists"}), 409

            cur.execute(
                "INSERT INTO users (email, password_hash, fpl_team_id) VALUES (%s, %s, %s) "
                "RETURNING id, email, fpl_team_id",
                (email, hash_password(password), fpl_team_id),
            )
            user = cur.fetchone()
        conn.commit()
        raw_token = create_session(conn, user["id"])
    finally:
        conn.close()

    response = jsonify(user)
    set_session_cookie(response, raw_token)
    return response, 201


@app.route("/api/auth/login", methods=["POST"])
def login():
    body = request.get_json(silent=True) or {}
    email = (body.get("email") or "").strip().lower()
    password = body.get("password") or ""

    conn = get_connection()
    try:
        with conn.cursor() as cur:
            cur.execute(
                "SELECT id, email, fpl_team_id, password_hash FROM users WHERE email = %s", (email,)
            )
            user = cur.fetchone()

        if user is None or not verify_password(password, user["password_hash"]):
            return jsonify({"error": "invalid email or password"}), 401

        raw_token = create_session(conn, user["id"])
    finally:
        conn.close()

    response = jsonify({"id": user["id"], "email": user["email"], "fpl_team_id": user["fpl_team_id"]})
    set_session_cookie(response, raw_token)
    return response


@app.route("/api/auth/logout", methods=["POST"])
@require_auth
def logout():
    raw_token = request.cookies.get(SESSION_COOKIE_NAME)
    conn = get_connection()
    try:
        delete_session(conn, raw_token)
    finally:
        conn.close()

    response = make_response("", 204)
    clear_session_cookie(response)
    return response


@app.route("/api/auth/me", methods=["GET"])
@require_auth
def me():
    return jsonify(g.user)


# ---- Team registration ----

@app.route("/api/team", methods=["PUT"])
@require_auth
def update_team():
    body = request.get_json(silent=True) or {}
    try:
        fpl_team_id = int(body.get("fpl_team_id"))
    except (TypeError, ValueError):
        return jsonify({"error": "fpl_team_id must be a number"}), 400

    try:
        pipeline_core.fetch_entry_info(fpl_team_id)
    except requests.HTTPError:
        return jsonify({"error": "that FPL team ID doesn't seem to exist"}), 400

    conn = get_connection()
    try:
        with conn.cursor() as cur:
            cur.execute(
                "UPDATE users SET fpl_team_id = %s WHERE id = %s RETURNING id, email, fpl_team_id",
                (fpl_team_id, g.user["id"]),
            )
            user = cur.fetchone()
        conn.commit()
    finally:
        conn.close()

    return jsonify(user)


# ---- Gameweek info ----

@app.route("/api/gameweek/current", methods=["GET"])
@require_auth
def gameweek_current():
    bootstrap = pipeline_core.fetch_bootstrap()
    events = bootstrap["events"]
    current_gw = next((e["id"] for e in events if e["is_current"]), None)
    next_gw = next((e["id"] for e in events if e["is_next"]), None)
    return jsonify({"current_gw": current_gw, "next_gw": next_gw})


# ---- Predictions ----

@app.route("/api/predictions", methods=["GET"])
@require_auth
def get_predictions():
    gw = request.args.get("gw", type=int)
    if gw is None:
        return jsonify({"error": "gw query parameter is required"}), 400

    conn = get_connection()
    try:
        predictions = pipeline_core.load_predictions(conn, gw)
    finally:
        conn.close()
    return jsonify(predictions)


def _refresh_predictions(conn, gw: int) -> dict:
    """Shared by /api/predictions/refresh and /api/gameweek/advance."""
    last_refresh = pipeline_core.get_last_refresh(conn, gw)
    if not pipeline_core.is_stale(last_refresh):
        return {
            "refreshed": False,
            "player_count": last_refresh["player_count"],
            "last_refreshed_at": last_refresh["last_refreshed_at"].isoformat(),
        }

    bootstrap = pipeline_core.fetch_bootstrap()
    fixtures = pipeline_core.fetch_fixtures()
    previous_gw = gw - 1
    previous_gw_points = pipeline_core.fetch_actual_points(previous_gw) if previous_gw >= 1 else None

    features = pipeline_core.build_features(bootstrap, fixtures, gw, previous_gw_points)
    predictions = pipeline_core.calculate_xp(features)
    pipeline_core.save_predictions(conn, gw, predictions)

    return {
        "refreshed": True,
        "player_count": len(predictions),
        "last_refreshed_at": datetime.now(timezone.utc).isoformat(),
    }


@app.route("/api/predictions/refresh", methods=["POST"])
@require_auth
def refresh_predictions():
    body = request.get_json(silent=True) or {}
    gw = body.get("gw")
    if not isinstance(gw, int):
        return jsonify({"error": "gw is required and must be an integer"}), 400

    conn = get_connection()
    try:
        result = _refresh_predictions(conn, gw)
    finally:
        conn.close()
    return jsonify(result)


def _evaluate(conn, gw: int) -> dict:
    """Shared by /api/evaluate and /api/gameweek/advance."""
    if pipeline_core.has_been_evaluated(conn, gw):
        return {"evaluated": False, "reason": "already evaluated"}

    predictions = pipeline_core.load_predictions(conn, gw)
    if not predictions:
        return {"evaluated": False, "reason": "no predictions saved for this gameweek yet"}

    actual_points = pipeline_core.fetch_actual_points(gw)
    results = pipeline_core.compare(predictions, actual_points)
    pipeline_core.save_results(conn, gw, results)
    pipeline_core.log_performance(conn, gw, results)
    return {"evaluated": True, "num_players": len(results)}


@app.route("/api/evaluate", methods=["POST"])
@require_auth
def evaluate():
    body = request.get_json(silent=True) or {}
    gw = body.get("gw")
    if not isinstance(gw, int):
        return jsonify({"error": "gw is required and must be an integer"}), 400

    conn = get_connection()
    try:
        result = _evaluate(conn, gw)
    finally:
        conn.close()
    return jsonify(result)


@app.route("/api/gameweek/advance", methods=["POST"])
@require_auth
def advance_gameweek():
    """The main 'press a button' action — evaluate the gw that just finished, then refresh the next one."""
    body = request.get_json(silent=True) or {}
    next_gw = body.get("next_gw")
    if not isinstance(next_gw, int):
        return jsonify({"error": "next_gw is required and must be an integer"}), 400

    conn = get_connection()
    try:
        if next_gw > 1:
            evaluation = _evaluate(conn, next_gw - 1)
        else:
            evaluation = {"evaluated": False, "reason": "no earlier gameweek"}
        refresh = _refresh_predictions(conn, next_gw)
    finally:
        conn.close()

    return jsonify({"gw": next_gw, "evaluation": evaluation, "refresh": refresh})


# ---- Squad ----

@app.route("/api/squad", methods=["GET"])
@require_auth
def get_squad():
    gw = request.args.get("gw", type=int)
    if gw is None:
        return jsonify({"error": "gw query parameter is required"}), 400
    if g.user["fpl_team_id"] is None:
        return jsonify({"error": "register your FPL team ID first"}), 400

    conn = get_connection()
    try:
        squad = pipeline_core.build_squad(conn, g.user["fpl_team_id"], gw)
    finally:
        conn.close()
    return jsonify(squad)


if __name__ == "__main__":
    # Local-only dev entrypoint — Vercel imports `app` directly and never
    # runs this file as a script. Loads frontend/.env (DATABASE_URL) since
    # there's no Vercel platform here to inject it for us.
    from dotenv import load_dotenv

    load_dotenv()
    # use_reloader=False: the auto-reloader restarts the whole process on
    # every file save, which kills any request that's mid-flight (e.g.
    # waiting on a slow Neon wake-up) — annoying while actively testing.
    # threaded=True: Flask's dev server is single-threaded by default, so
    # slow requests (Neon cold-starts, FPL API calls) would otherwise
    # queue up behind each other instead of running concurrently.
    app.run(port=5328, debug=True, use_reloader=False, threaded=True)
