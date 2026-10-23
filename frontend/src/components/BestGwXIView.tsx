import { useMemo } from "react"
import type { Player } from "../types"
import { BUDGET, pickBestGwSquad } from "../logic/bestGwSquad"
import { PITCH_ROWS, POSITION_LABELS } from "../constants"
import { PlayerCard } from "./PitchView"
import { PitchMarkings } from "./PitchMarkings"

interface Props {
  allPlayers: Player[]
  nextGw: number
}

// The best team money can buy for one gameweek — picked from every
// player in the game, not just the ones you own. Useful as a benchmark
// for your own squad, or as a target when planning a wildcard.
export function BestGwXIView({ allPlayers, nextGw }: Props) {
  // The search takes a moment, so only redo it when the player data changes
  const squad = useMemo(() => pickBestGwSquad(allPlayers), [allPlayers])

  if (squad === null) {
    return <p className="squad-summary">Couldn't build a legal squad from GW{nextGw}'s predictions.</p>
  }

  const bench = [...squad.bench].sort((a, b) => (a.position === "Goalkeeper" ? -1 : b.position === "Goalkeeper" ? 1 : 0))

  return (
    <div className="view">
      <h2 className="gameweek-heading">Best possible XI for GW{nextGw}</h2>
      <p className="squad-total">{squad.projectedPoints.toFixed(1)} pts projected</p>

      <div className="stat-row">
        <div className="stat">
          <span className="stat-label">Squad value</span>
          <span className="stat-value">£{squad.squadValue.toFixed(1)}m</span>
        </div>
        <div className="stat">
          <span className="stat-label">Left in bank</span>
          <span className="stat-value">£{(BUDGET - squad.squadValue).toFixed(1)}m</span>
        </div>
        <div className="stat">
          <span className="stat-label">Formation</span>
          <span className="stat-value">{squad.formation}</span>
        </div>
      </div>

      <p className="squad-summary">
        Picked from every player in the game within FPL's rules: £{BUDGET}m for all 15, max 3 per club. The bench is
        kept as cheap as possible so the money goes into the XI. Captain's points are doubled.
      </p>

      <div className="pitch">
        <PitchMarkings />
        {PITCH_ROWS.map((position) => {
          const rowPlayers = squad.startingXI.filter((p) => p.position === position)
          if (rowPlayers.length === 0) return null

          return (
            <div className="pitch-row-wrapper" key={position}>
              <span className="pitch-row-label">{POSITION_LABELS[position]}</span>
              <div className="pitch-row">
                {rowPlayers.map((player) => {
                  const isCaptain = player.id === squad.captain.id
                  return (
                    <div className="planner-slot" key={player.id}>
                      <PlayerCard
                        player={player}
                        points={(player.xP * (isCaptain ? 2 : 1)).toFixed(1)}
                        isCaptain={isCaptain}
                      />
                      <span className="card-price">£{player.price.toFixed(1)}m</span>
                    </div>
                  )
                })}
              </div>
            </div>
          )
        })}
      </div>

      <div className="bench-box">
        <div className="bench-label">Bench</div>
        <div className="bench-row">
          {bench.map((player) => (
            <div className="planner-slot" key={player.id}>
              <PlayerCard player={player} points={player.xP.toFixed(1)} />
              <span className="card-price">£{player.price.toFixed(1)}m</span>
            </div>
          ))}
        </div>
      </div>
    </div>
  )
}
