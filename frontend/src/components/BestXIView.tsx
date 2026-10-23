import { useEffect, useState } from "react"
import type { Squad, SquadPick } from "../types"
import { pickBestXI } from "../logic/bestXI"
import { PITCH_ROWS, POSITION_LABELS } from "../constants"
import { PlayerCard } from "./PitchView"
import { PitchMarkings } from "./PitchMarkings"

interface Props {
  squad: Squad
  nextGw: number
}

// Interactive planner: starts from the algorithm's own suggestion, but
// lets the user sub bench players in and reassign captaincy, recomputing
// projected points live. Nothing here is saved anywhere — it's a
// what-if sandbox over data already loaded in the browser.
export function BestXIView({ squad, nextGw }: Props) {
  const [xi, setXi] = useState<SquadPick[]>([])
  const [bench, setBench] = useState<SquadPick[]>([])
  const [captainId, setCaptainId] = useState<number | null>(null)
  const [viceCaptainId, setViceCaptainId] = useState<number | null>(null)

  useEffect(() => {
    resetToSuggested()
    // Only the squad identity should trigger a reset — xi/bench/captain
    // are deliberately local state from here on, not derived every render.
    // eslint-disable-next-line react-hooks/exhaustive-deps
  }, [squad])

  function resetToSuggested() {
    const result = pickBestXI(squad.picks)
    setXi(result.startingXI)
    setBench(result.bench)
    setCaptainId(result.captain.id)
    setViceCaptainId(result.viceCaptain.id)
  }

  function swapIn(benchPlayer: SquadPick) {
    const samePosition = xi.filter((p) => p.position === benchPlayer.position)
    if (samePosition.length === 0) return
    const weakest = samePosition.reduce((min, p) => (p.xP < min.xP ? p : min))

    setXi((prev) => prev.map((p) => (p.id === weakest.id ? benchPlayer : p)))
    setBench((prev) => prev.map((p) => (p.id === benchPlayer.id ? weakest : p)))
    if (captainId === weakest.id) setCaptainId(benchPlayer.id)
    if (viceCaptainId === weakest.id) setViceCaptainId(benchPlayer.id)
  }

  function makeCaptain(playerId: number) {
    if (playerId === viceCaptainId) setViceCaptainId(captainId)
    setCaptainId(playerId)
  }

  function makeVice(playerId: number) {
    if (playerId === captainId) setCaptainId(viceCaptainId)
    setViceCaptainId(playerId)
  }

  if (xi.length === 0) {
    return <p className="squad-summary">Loading...</p>
  }

  if (squad.picks.length === 0) {
    return (
      <p className="squad-summary">
        Predictions haven't been generated for GW{nextGw} yet — click "Refresh predictions" above.
      </p>
    )
  }

  const totalPoints = xi.reduce((sum, p) => sum + p.xP * (p.id === captainId ? 2 : 1), 0)

  return (
    <div className="view">
      <h2 className="gameweek-heading">Your best team for GW{nextGw}</h2>
      <p className="squad-total">{totalPoints.toFixed(1)} pts projected</p>
      <p className="squad-summary">
        Captain's points are doubled. Tap a bench player to sub them in for the weakest starter in their position;
        tap C/V on any starter to change captaincy.{" "}
        <button type="button" className="link-button" onClick={resetToSuggested}>
          Reset to suggested XI
        </button>
      </p>

      <div className="pitch">
        <PitchMarkings />
        {PITCH_ROWS.map((position) => {
          const rowPlayers = xi.filter((p) => p.position === position)
          if (rowPlayers.length === 0) return null

          return (
            <div className="pitch-row-wrapper" key={position}>
              <span className="pitch-row-label">{POSITION_LABELS[position]}</span>
              <div className="pitch-row">
                {rowPlayers.map((player) => (
                  <div className="planner-slot" key={player.id}>
                    <PlayerCard
                      player={player}
                      points={(player.xP * (player.id === captainId ? 2 : 1)).toFixed(1)}
                      isCaptain={player.id === captainId}
                      isViceCaptain={player.id === viceCaptainId}
                    />
                    <div className="card-controls">
                      <button type="button" onClick={() => makeCaptain(player.id)} title="Make captain">
                        C
                      </button>
                      <button type="button" onClick={() => makeVice(player.id)} title="Make vice-captain">
                        V
                      </button>
                    </div>
                  </div>
                ))}
              </div>
            </div>
          )
        })}
      </div>

      <div className="bench-box">
        <div className="bench-label">Bench — tap to sub in</div>
        <div className="bench-row">
          {bench.map((player) => (
            <button
              type="button"
              key={player.id}
              className="card-button"
              onClick={() => swapIn(player)}
              title={`Sub in ${player.name}`}
            >
              <PlayerCard player={player} points={player.xP.toFixed(1)} />
            </button>
          ))}
        </div>
      </div>
    </div>
  )
}
