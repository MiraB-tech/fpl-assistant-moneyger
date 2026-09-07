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
  const [squad, setSquad] = useState<Squad | null>(null)
  const [allPlayers, setAllPlayers] = useState<Player[] | null>(null)
  const [error, setError] = useState<string | null>(null)
  const [tab, setTab] = useState<Tab>("squad")
  const [refreshing, setRefreshing] = useState(false)

  useEffect(() => {
    getCurrentUser().then(setUser)
  }, [])

  // Figures out which gameweek to show, then loads that gameweek's squad
  // and full player pool together. Used both on first login and after a
  // manual refresh, so both paths always end up asking FPL for the
  // current state instead of guessing what changed.
  async function loadGameweekData() {
    const { current_gw, next_gw } = await getCurrentGameweek()
    setNextGw(next_gw)

    const displayGw = current_gw ?? next_gw
    if (displayGw === null) return

    const [s, p] = await Promise.all([loadSquad(displayGw), loadAllPlayers(displayGw)])
    setSquad(s)
    setAllPlayers(p)
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
    setSquad(null)
    setAllPlayers(null)
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

      {!squad || !allPlayers ? (
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

          {tab === "squad" && <SquadView squad={squad} />}
          {tab === "best-xi" && <BestXIView squad={squad} />}
          {tab === "transfers" && <TransfersView squad={squad} allPlayers={allPlayers} />}
        </>
      )}
    </div>
  )
}

export default App
