import type { SquadPick } from "../types"
import { PITCH_ROWS, POSITION_LABELS } from "../constants"
import { getContrastText, getTeamColor } from "../teamColors"
import { PitchMarkings } from "./PitchMarkings"

interface PitchViewProps {
  players: SquadPick[]
}

// Read-only view of the *actual* current squad — points shown are real
// points scored so far this gameweek, with the captain's total already
// doubled (FPL's own `multiplier` field is 2 for the captain, 1 for a
// normal starter, so `current_points * multiplier` handles that for free).
export function PitchView({ players }: PitchViewProps) {
  return (
    <div className="pitch">
      <PitchMarkings />
      {PITCH_ROWS.map((position) => {
        const rowPlayers = players.filter((p) => p.position === position)
        if (rowPlayers.length === 0) return null

        return (
          <div className="pitch-row-wrapper" key={position}>
            <span className="pitch-row-label">{POSITION_LABELS[position]}</span>
            <div className="pitch-row">
              {rowPlayers.map((player) => (
                <PlayerCard
                  key={player.id}
                  player={player}
                  points={String(player.current_points * player.multiplier)}
                  isCaptain={player.is_captain}
                  isViceCaptain={player.is_vice_captain}
                />
              ))}
            </div>
          </div>
        )
      })}
    </div>
  )
}

interface BenchRowProps {
  players: SquadPick[]
}

export function BenchRow({ players }: BenchRowProps) {
  return (
    <div className="bench-box">
      <div className="bench-label">Bench</div>
      <div className="bench-row">
        {players.map((player) => (
          <PlayerCard key={player.id} player={player} points={String(player.current_points)} />
        ))}
      </div>
    </div>
  )
}

interface PlayerCardProps {
  player: { name: string; team: string }
  points: string
  isCaptain?: boolean
  isViceCaptain?: boolean
}

export function PlayerCard({ player, points, isCaptain, isViceCaptain }: PlayerCardProps) {
  const background = getTeamColor(player.team)
  const color = getContrastText(background)

  return (
    <div className="player-card" style={{ background, color }}>
      {isCaptain && <span className="card-badge captain">C</span>}
      {isViceCaptain && <span className="card-badge vice-captain">V</span>}
      <div className="card-name">{player.name}</div>
      <div className="card-team-code">{player.team}</div>
      <div className="card-points">{points}</div>
    </div>
  )
}
