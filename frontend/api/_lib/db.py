"""
Neon (Postgres) connection helper for the API's serverless functions.

Vercel runs each request as a fresh, short-lived process, so we open a new
connection per request rather than keeping a long-lived pool in memory —
that's exactly what Neon's *pooled* connection string (the "-pooler" one)
is designed for: it hands the real connection-pooling responsibility to
Neon's own PgBouncer layer instead of our code.
"""

import os

import psycopg
from psycopg.rows import dict_row


def get_connection():
    """Open one connection to Neon for the current request."""
    database_url = os.environ["DATABASE_URL"]
    # dict_row makes every query return rows as {"column_name": value, ...}
    # dicts instead of plain tuples — easier to turn straight into JSON.
    # Neon's compute suspends when idle and can take a while to wake back
    # up. psycopg retries the pooler hostname's resolved IPs one at a time,
    # EACH with its own connect_timeout — with 3 IPs, a 30s timeout means
    # up to 90s worst case, which would blow past Vercel's own function
    # time limit. 10s per attempt keeps the real worst case bounded.
    return psycopg.connect(database_url, row_factory=dict_row, connect_timeout=10)
