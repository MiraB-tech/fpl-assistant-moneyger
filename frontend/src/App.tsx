import { useEffect, useState } from "react"
import "./App.css"
import { advanceGameweek, getCurrentGameweek, getCurrentUser, loadAllPlayers, loadSquad, logout } from "./data"
import type { Player, Squad, User } from "./types"
import { SquadView } from "./components/SquadView"
import { BestXIView } from "./components/BestXIView"
import { TransfersView } from "./components/TransfersView"
import { BestGwXIView } from "./components/BestGwXIView"
import { LoginView } from "./components/LoginView"
import { RegisterView } from "./components/RegisterView"
import { TeamSetupView } from "./components/TeamSetupView"

type Tab = "squad" | "best-xi" | "transfers" | "best-gw-xi"
type AuthView = "login" | "register"

const TABS: Tab[] = ["squad", "best-xi", "transfers", "best-gw-xi"]

// The open tab lives in the address bar (e.g. ...#best-xi), so reloading
// the page keeps you on the same tab instead of jumping back to Squad.
function tabFromUrl(): Tab {
  const hash = window.location.hash.slice(1)
  return TABS.includes(hash as Tab) ? (hash as Tab) : "squad"
}

// Logo mark + name, shown at the top of every screen
function Brand() {
  return (
    <div className="brand">
      <span className="brand-mark" aria-hidden="true">
        AM
      </span>
      <h1>The Assistant Moneyger</h1>
    </div>
  )
}

function App() {
  // undefined = still checking on load, null = not logged in, User = logged in
  const [user, setUser] = useState<User | null | undefined>(undefined)
  const [authView, setAuthView] = useState<AuthView>("login")

  const [nextGw, setNextGw] = useState<number | null>(null)
  // The gameweek that's live/just-finished — what the "Squad" tab shows,
  // with real points scored so far.
  const [currentSquad, setCurrentSquad] = useState<Squad | null>(null)
  // The upcoming gameweek — what "Best XI" and "Transfers" plan against,
  // since those are decisions you're making ahead of that deadline, not
  // ones already locked in.
  const [nextSquad, setNextSquad] = useState<Squad | null>(null)
  const [nextAllPlayers, setNextAllPlayers] = useState<Player[] | null>(null)

  const [error, setError] = useState<string | null>(null)
  const [tab, setTabState] = useState<Tab>(tabFromUrl)

  function setTab(next: Tab) {
    window.location.hash = next
    setTabState(next)
  }
  const [refreshing, setRefreshing] = useState(false)

  useEffect(() => {
    getCurrentUser().then(setUser)
  }, [])

  // Loads whatever's available for both the current and next gameweek.
  // Used on first login and after a manual refresh, so both paths always
  // reflect FPL's current state instead of guessing what changed.
  async function loadGameweekData() {
    const { current_gw, next_gw } = await getCurrentGameweek()
    setNextGw(next_gw)

    // Make sure next gameweek's predictions exist before loading anything
    // that depends on them — otherwise, after a gap where nobody opened
    // the app, Best XI and Transfers come up empty until someone thinks
    // to press "Refresh". Cheap to call every time: the backend only does
    // the real work if its predictions are over 6 hours old.
    if (next_gw !== null) {
      await advanceGameweek(next_gw)
    }

    await Promise.all([
      current_gw !== null ? loadSquad(current_gw).then(setCurrentSquad) : Promise.resolve(),
      next_gw !== null
        ? // FPL has no "picks" record for next_gw until its deadline
          // passes, so reuse current_gw's real picks (your actual owned
          // squad) joined against next_gw's predictions for planning.
          Promise.all([loadSquad(next_gw, current_gw ?? undefined), loadAllPlayers(next_gw)]).then(([s, p]) => {
            setNextSquad(s)
            setNextAllPlayers(p)
          })
        : Promise.resolve(),
    ])
  }

  useEffect(() => {
    if (!user || !user.fpl_team_id) return

    async function run() {
      try {
        await loadGameweekData()
      } catch (err) {
        setError((err as Error).message)
      }
    }
    run()
  }, [user])

  async function handleRefresh() {
    if (nextGw === null) return
    setRefreshing(true)
    setError(null)
    try {
      await advanceGameweek(nextGw)
      await loadGameweekData()
    } catch (err) {
      setError((err as Error).message)
    } finally {
      setRefreshing(false)
    }
  }

  async function handleLogout() {
    await logout()
    setUser(null)
    setCurrentSquad(null)
    setNextSquad(null)
    setNextAllPlayers(null)
    setNextGw(null)
  }

  if (user === undefined) {
    return <p className="loading">Loading...</p>
  }

  if (user === null) {
    return (
      <div className="app">
        <header className="topbar">
          <Brand />
        </header>
        <main className="content auth-page">
          <p className="tagline">Your FPL copilot — smarter line-ups, captains and transfers every gameweek.</p>
          {authView === "login" ? (
            <LoginView onLoggedIn={setUser} onSwitchToRegister={() => setAuthView("register")} />
          ) : (
            <RegisterView onRegistered={setUser} onSwitchToLogin={() => setAuthView("login")} />
          )}
        </main>
      </div>
    )
  }

  if (!user.fpl_team_id) {
    return (
      <div className="app">
        <header className="topbar">
          <Brand />
        </header>
        <main className="content auth-page">
          <TeamSetupView onTeamSet={setUser} />
        </main>
      </div>
    )
  }

  const ready = currentSquad !== null && nextSquad !== null && nextAllPlayers !== null

  return (
    <div className="app">
      <header className="topbar">
        <Brand />
        <div className="topbar-actions">
          <button className="button primary" onClick={handleRefresh} disabled={refreshing || nextGw === null}>
            {refreshing ? "Refreshing..." : "Refresh predictions"}
          </button>
          <button className="button ghost" onClick={handleLogout}>
            Log out
          </button>
        </div>
      </header>

      <main className="content">
        {error && <p className="error">{error}</p>}

        {!ready ? (
          <p className="loading">Loading your squad...</p>
        ) : (
          <>
            <nav className="tabs">
              <button className={tab === "squad" ? "active" : ""} onClick={() => setTab("squad")}>
                Squad
              </button>
              <button className={tab === "best-xi" ? "active" : ""} onClick={() => setTab("best-xi")}>
                Your Best XI
              </button>
              <button className={tab === "transfers" ? "active" : ""} onClick={() => setTab("transfers")}>
                Transfers
              </button>
              {nextGw !== null && (
                <button className={tab === "best-gw-xi" ? "active" : ""} onClick={() => setTab("best-gw-xi")}>
                  Best GW{nextGw} XI
                </button>
              )}
            </nav>

            {tab === "squad" && <SquadView squad={currentSquad} />}
            {tab === "best-xi" && nextGw !== null && <BestXIView squad={nextSquad} nextGw={nextGw} />}
            {tab === "transfers" && <TransfersView squad={nextSquad} allPlayers={nextAllPlayers} />}
            {tab === "best-gw-xi" && nextGw !== null && <BestGwXIView allPlayers={nextAllPlayers} nextGw={nextGw} />}
          </>
        )}
      </main>
    </div>
  )
}

export default App
