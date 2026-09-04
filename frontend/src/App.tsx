import { useEffect, useState } from "react"
import "./App.css"
import { loadAllPlayers, loadSquad } from "./data"
import type { Player, Squad } from "./types"
import { SquadView } from "./components/SquadView"
import { BestXIView } from "./components/BestXIView"
import { TransfersView } from "./components/TransfersView"

type Tab = "squad" | "best-xi" | "transfers"

function App() {
  const [squad, setSquad] = useState<Squad | null>(null)
  const [allPlayers, setAllPlayers] = useState<Player[] | null>(null)
  const [error, setError] = useState<string | null>(null)
  const [tab, setTab] = useState<Tab>("squad")

  useEffect(() => {
    loadSquad()
      .then((loadedSquad) => {
        setSquad(loadedSquad)
        return loadAllPlayers(loadedSquad.gameweek)
      })
      .then(setAllPlayers)
      .catch((err: Error) => setError(err.message))
  }, [])

  if (error) {
    return <p className="error">Couldn't load data: {error}</p>
  }

  if (!squad || !allPlayers) {
    return <p className="squad-summary">Loading...</p>
  }

  return (
    <div className="app">
      <h1>The Assistant Moneyger</h1>

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
    </div>
  )
}

export default App
