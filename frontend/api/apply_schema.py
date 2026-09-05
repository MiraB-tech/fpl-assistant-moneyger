"""
One-off utility: applies db/schema.sql against whatever DATABASE_URL
points at. Safe to re-run (the schema itself is all `CREATE TABLE IF NOT
EXISTS`). Not part of the deployed app — just a local convenience so the
schema doesn't have to be pasted into Neon's web SQL Editor by hand.

Run it with:
    ./.venv/Scripts/python.exe apply_schema.py
"""

import os
from pathlib import Path

import psycopg
from dotenv import load_dotenv

load_dotenv()

SCHEMA_PATH = Path(__file__).parent.parent.parent / "db" / "schema.sql"


def main():
    raw_sql = SCHEMA_PATH.read_text(encoding="utf-8")
    # Strip full-line comments first — otherwise a statement preceded by a
    # comment line ends up looking like a chunk that "starts with --" and
    # gets skipped whole, comment and CREATE TABLE together.
    sql = "\n".join(line for line in raw_sql.splitlines() if not line.strip().startswith("--"))
    statements = [s.strip() for s in sql.split(";") if s.strip()]

    print(f"Connecting to {os.environ['DATABASE_URL'].split('@')[1]}...")
    conn = psycopg.connect(os.environ["DATABASE_URL"], connect_timeout=30)
    try:
        with conn.cursor() as cur:
            for i, statement in enumerate(statements, start=1):
                print(f"[{i}/{len(statements)}] {statement.splitlines()[0][:60]}...")
                cur.execute(statement)
        conn.commit()
    finally:
        conn.close()
    print("Schema applied successfully.")


if __name__ == "__main__":
    main()
