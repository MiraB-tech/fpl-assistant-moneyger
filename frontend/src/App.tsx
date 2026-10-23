import { useEffect, useState } from "react"
import "./App.css"
import {
  advanceGameweek,
  getCurrentGameweek,
  getCurrentUser,
  loadAllPlayers,
  loadSquad,
  logout,
} from "./data"
import type { Player, Squad, User } from "./types"
import { SquadView } from "./components/SquadView"
import { BestXIView } from "./components/BestXIView"
import { TransfersView } from "./components/TransfersView"
import { LoginView } from "./components/LoginView"
import { RegisterView } from "./components/RegisterView"
import { TeamSetupView } from "./components/TeamSetupView"

type Tab = "squad" | "best-xi" | "transfers"
type AuthView = "login" | "register"

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
  const [tab, setTab] = useState<Tab>("squad")
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
    return <p className="squad-summary">Loading...</p>
  }

  if (user === null) {
    return (
      <div className="app">
        <h1>The Assistant Moneyger</h1>
        {authView === "login" ? (
          <LoginView onLoggedIn={setUser} onSwitchToRegister={() => setAuthView("register")} />
        ) : (
          <RegisterView onRegistered={setUser} onSwitchToLogin={() => setAuthView("login")} />
        )}
      </div>
    )
  }

  if (!user.fpl_team_id) {
    return (
      <div className="app">
        <h1>The Assistant Moneyger</h1>
        <TeamSetupView onTeamSet={setUser} />
      </div>
    )
  }

  const ready = currentSquad !== null && nextSquad !== null && nextAllPlayers !== null

  return (
    <div className="app">
      <div className="header-row">
        <h1>The Assistant Moneyger</h1>
        <div>
          <button onClick={handleRefresh} disabled={refreshing || nextGw === null}>
            {refreshing ? "Refreshing..." : "Refresh predictions"}
          </button>
          <button className="link-button" onClick={handleLogout}>
            Log out
          </button>
        </div>
      </div>

      {error && <p className="error">{error}</p>}

      {!ready ? (
        <p className="squad-summary">Loading...</p>
      ) : (
        <>
          <nav className="tabs">
            <button className={tab === "squad" ? "active" : ""} onClick={() => setTab("squad")}>
              Squad
            </button>
            <button className={tab === "best-xi" ? "active" : ""} onClick={() => setTab("best-xi")}>
              Best XI
            </button>
            <button className={tab === "transfers" ? "active" : ""} onClick={() => setTab("transfers")}>
              Transfers
            </button>
          </nav>

          {tab === "squad" && <SquadView squad={currentSquad} />}
          {tab === "best-xi" && nextGw !== null && <BestXIView squad={nextSquad} nextGw={nextGw} />}
          {tab === "transfers" && <TransfersView squad={nextSquad} allPlayers={nextAllPlayers} />}
        </>
      )}
    </div>
  )
}

export default App
