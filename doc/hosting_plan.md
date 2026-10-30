# Hosting Plan

Roadmap step 2: get the backend + database online so pilot players can use the app.
Paused 2026-10-30 (waiting on fibre for the large downloads). Pick up from **"Where we stopped"**.

---

## Where we stopped

- Branch: `feat/hosting-prep` (off `develop`)
- Written: `backend/Dockerfile`, `backend/.dockerignore`
- **Not yet done:** build + test the image locally (`docker build -t moneyger-api .` from `backend/`, ~few hundred MB first time — needs fibre)
- Budget: **$0 to start**

---

## Decision 1: where to host

| Option | Monthly cost | You manage | Good for | Main risks |
|---|---|---|---|---|
| **A. Free managed stack** — Render (backend) + Neon (DB) + GitHub Actions (daily job) | $0 | Almost nothing | Getting pilot players on fast, no money | Backend sleeps after 15 min idle (first visit waits ~30–60s); Neon deletes free projects idle 90+ days (daily job prevents it) |
| **B. Your own VPS** — one small server running backend + Postgres + HTTPS proxy | ~$4–7 (check current prices: Hetzner, DigitalOcean, Contabo) | Everything: security, updates, backups, uptime | Learning to run servers — **the foundation of a hosting business** | Costs money; a mistake (open DB port, no backups) can lose data or get hacked |
| **C. Oracle Cloud "Always Free" VM** — a real VPS, free forever | $0 | Everything (same as B) | B's learning at A's price | Needs a card to verify identity; free capacity is often "out of stock" in a region; Oracle can reclaim idle free VMs |
| **D. Paid managed** — e.g. Render paid web + Postgres | ~$13 | Almost nothing | Later, once there are users + budget | Cost |

### Recommendation

**Your VPS idea is sound** — if you want to sell hosting, running your own server for your own app is the right way to learn it, and you'd be your own first customer. Two honest caveats: a VPS isn't free (only C is), and the risks are real but manageable with the checklist below.

Suggested path:
1. **Try C first** (free VPS). If Oracle accepts your card verification and has capacity → you get the VPS learning at $0. Follow the **VPS track** below.
2. **If C fails, go A** to get pilot players on now, and practise the VPS track on the side.
3. **Move to B** when you have even $5/month — and treat it as the first server of the hosting business.

Everything is portable: Docker image + standard Postgres. Moving between A, B, C, D later = copy the database, change settings. No code rewrite.

---

## Phase 1 — Code prep (same for every option)

One step at a time, review between each.

| # | Step | Why |
|---|---|---|
| 1.1 | **Build + run the Docker image locally**, pointed at the local DB | Proves the recipe works before any server sees it |
| 1.2 | **Production settings from environment variables** — DB connection string, `AllowedOrigin`, job secret. Backend **refuses to start** if `AllowedOrigin` is missing in production | Secrets never go in GitHub. Today, a missing `AllowedOrigin` silently switches the cross-site check *off* — must fail loudly instead |
| 1.3 | **Apply database migrations on start-up** | New server = empty DB; it builds its own sheets on first boot |
| 1.4 | **Behind-a-proxy setup** — trust the host's forwarded headers, secure (HTTPS-only) cookies | Hosts put a proxy in front of the app; without this, login cookies break or are sent insecurely |
| 1.5 | **Daily job endpoint** — `POST /api/jobs/daily`, protected by a secret key: grade last GW + build next GW predictions | Predictions exist before every deadline even if nobody opens the app |
| 1.6 | **Scheduler** — A: GitHub Actions cron calls 1.5 daily. B/C: either the same, or a built-in timer inside the backend (the server never sleeps) | Triggers 1.5 |

## Phase 2 — Infrastructure

### Track A (free managed)
1. Neon: create project in the region nearest the Render region you'll pick (e.g. both Frankfurt) → copy connection string.
2. Render: new **Web Service** from the GitHub repo, root `backend/`, Docker, **free** instance. Set env vars from 1.2. Port 8080.
3. GitHub: add the job secret + backend URL as repo secrets; enable the 1.6 workflow.
4. **Never** use Render's own free Postgres — deleted after 30 days.

### Track B/C (your own VPS)
Security first — do these before running the app:
1. **Log in with SSH keys only**; turn off password login and root login.
2. **Firewall**: allow only ports 22 (SSH), 80, 443. **Postgres (5432) must never be open to the internet.**
3. **Automatic security updates** on.
4. Install Docker. Run backend + Postgres + **Caddy** (web server that gets free HTTPS certificates automatically) with one `docker-compose.prod.yml`.
5. **Backups**: nightly `pg_dump` → copied **off the server** (Backblaze B2 or Cloudflare R2 both have free tiers). **Test a restore once** — an untested backup isn't a backup.
6. **Uptime alert**: UptimeRobot (free) emails you if the site goes down.
7. A **domain name** (~$10/yr, also good for the brand) — or a free subdomain (DuckDNS) to start. HTTPS needs one.

**For the hosting business:** look at **Coolify** or **Dokploy** — free, open-source panels that make your own VPS work like Render (deploy from GitHub, HTTPS, databases, backups from a web page). Many small hosting resellers run on exactly this. Worth installing on the VPS from day one instead of wiring everything by hand.

## Phase 3 — Data

| Choice | How | Recommendation |
|---|---|---|
| Start fresh | Migrations (1.3) build empty sheets | Simplest |
| Copy local data | `pg_dump` local → `pg_restore` to hosted | Do this — keeps your account and the GW3/GW8 accuracy history. Delete the test accounts (`dotnet_test@`, `claude_probe@`) first |

## Phase 4 — Connect the frontend + go live

1. Vercel: **rewrite** `/api/*` → the hosted backend URL (`vercel.json`). Browser still sees one website → cookies work as locally.
2. Set backend `AllowedOrigin` = the Vercel address.
3. Merge `develop` → `main` (**only now** — before this, main would break the live site).
4. Check: register, log in, all four tabs, refresh, and that the daily job ran (`PredictionRuns` updated).

## Phase 5 — Onboard pilot players
Invite ~5 people, tell them about the first-visit wait (Track A), collect feedback → roadmap step 3.

---

## Data use (for limited internet)
- Local Docker image build (1.1): few hundred MB **first time only**, small after that.
- Track A: Render builds the image on **its** servers — your upload is just a `git push`.
- Track B/C: the VPS downloads images itself — again, not your connection.
- So only step 1.1 needs fibre. It can be skipped (let Render/the VPS do the first build) at the cost of debugging there instead of locally.
