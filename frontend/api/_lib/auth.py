"""
Everything to do with passwords and logged-in sessions: hashing/checking
passwords, creating and looking up session tokens, and a decorator that
protects a Flask route so it only runs for a logged-in user.
"""

import hashlib
import os
import secrets
from datetime import datetime, timedelta, timezone
from functools import wraps

import bcrypt
from flask import g, jsonify, request

from _lib.db import get_connection

SESSION_COOKIE_NAME = "session"
SESSION_LIFETIME = timedelta(days=30)

# Vercel sets VERCEL=1 in its deployed environment. Locally (plain HTTP,
# no Vercel in front of us) a Secure cookie would be silently refused by
# the browser, so only require HTTPS-only cookies once actually deployed.
IS_DEPLOYED = os.environ.get("VERCEL") == "1"


def hash_password(password: str) -> str:
    """Turn a plain-text password into a bcrypt hash safe to store in the database."""
    return bcrypt.hashpw(password.encode("utf-8"), bcrypt.gensalt()).decode("utf-8")


def verify_password(password: str, password_hash: str) -> bool:
    """Check a plain-text password attempt against a stored bcrypt hash."""
    return bcrypt.checkpw(password.encode("utf-8"), password_hash.encode("utf-8"))


def _hash_token(raw_token: str) -> str:
    """
    We never store a session token in a form that could be used to log in
    if the database were ever read by someone who shouldn't — same idea
    as hashing passwords, applied to login tokens instead.
    """
    return hashlib.sha256(raw_token.encode("utf-8")).hexdigest()


def create_session(conn, user_id: int) -> str:
    """Create a new logged-in session for a user, returning the raw token to hand back as a cookie."""
    raw_token = secrets.token_urlsafe(32)
    expires_at = datetime.now(timezone.utc) + SESSION_LIFETIME

    with conn.cursor() as cur:
        cur.execute(
            "INSERT INTO sessions (token_hash, user_id, expires_at) VALUES (%s, %s, %s)",
            (_hash_token(raw_token), user_id, expires_at),
        )
    conn.commit()
    return raw_token


def delete_session(conn, raw_token: str) -> None:
    """Log a session out by removing it from the database — it stops working immediately."""
    with conn.cursor() as cur:
        cur.execute("DELETE FROM sessions WHERE token_hash = %s", (_hash_token(raw_token),))
    conn.commit()


def get_user_from_token(conn, raw_token: str) -> dict | None:
    """Look up which user (if any) a raw session token cookie belongs to."""
    with conn.cursor() as cur:
        cur.execute(
            """
            SELECT users.id, users.email, users.fpl_team_id
            FROM sessions
            JOIN users ON users.id = sessions.user_id
            WHERE sessions.token_hash = %s AND sessions.expires_at > now()
            """,
            (_hash_token(raw_token),),
        )
        return cur.fetchone()


def require_auth(view_function):
    """
    Wrap a Flask route so it 401s immediately if there's no valid session
    cookie, instead of the route having to check this itself every time.
    On success, the logged-in user's info is available as `g.user`.
    """

    @wraps(view_function)
    def wrapped(*args, **kwargs):
        raw_token = request.cookies.get(SESSION_COOKIE_NAME)
        if raw_token is None:
            return jsonify({"error": "not logged in"}), 401

        conn = get_connection()
        try:
            user = get_user_from_token(conn, raw_token)
        finally:
            conn.close()

        if user is None:
            return jsonify({"error": "session expired or invalid"}), 401

        g.user = user
        return view_function(*args, **kwargs)

    return wrapped


def set_session_cookie(response, raw_token: str) -> None:
    """Attach a session cookie to a Flask response — httpOnly and Secure so it can't be read or stolen by page scripts."""
    response.set_cookie(
        SESSION_COOKIE_NAME,
        raw_token,
        max_age=int(SESSION_LIFETIME.total_seconds()),
        httponly=True,
        secure=IS_DEPLOYED,
        samesite="Lax",
        path="/",
    )


def clear_session_cookie(response) -> None:
    """Remove the session cookie on logout."""
    response.delete_cookie(SESSION_COOKIE_NAME, path="/")
