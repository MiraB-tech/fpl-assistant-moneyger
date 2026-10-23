import type { Squad } from "../types"
import { BenchRow, PitchView } from "./PitchView"

interface Props {
  squad: Squad
}

export function SquadView({ squad }: Props) {
  const starters = squad.picks.filter((p) => p.multiplier > 0)
  const bench = squad.picks
    .filter((p) => p.multiplier === 0)
    .sort((a, b) => a.squad_position - b.squad_position)

  // Real FPL scoring: only starters count, and the captain's points are
  // already doubled by multiplier (2 for captain, 1 for everyone else).
  const totalPoints = starters.reduce((sum, p) => sum + p.current_points * p.multiplier, 0)

  return (
    <div className="view">
      <h2 className="gameweek-heading">Gameweek {squad.gameweek}</h2>
      <p className="squad-total">{totalPoints} pts</p>
      <p className="squad-summary">
        Team value £{squad.team_value.toFixed(1)}m · Bank £{squad.bank.toFixed(1)}m
      </p>

      <PitchView players={starters} />
      <BenchRow players={bench} />
    </div>
  )
}
