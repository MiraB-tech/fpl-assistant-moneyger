import type { Squad } from "../types"

const POSITION_ORDER = ["Goalkeeper", "Defender", "Midfielder", "Forward"] as const

interface Props {
  squad: Squad
}

export function SquadView({ squad }: Props) {
  const starters = squad.picks.filter((p) => p.multiplier > 0)
  const bench = squad.picks
    .filter((p) => p.multiplier === 0)
    .sort((a, b) => a.squad_position - b.squad_position)

  return (
    <div>
      <p className="squad-summary">
        GW{squad.gameweek} · Team value £{squad.team_value.toFixed(1)}m · Bank £{squad.bank.toFixed(1)}m
      </p>

      <h3>Starting XI</h3>
      <table className="player-table">
        <thead>
          <tr>
            <th>Position</th>
            <th>Player</th>
            <th>Team</th>
            <th>Price</th>
            <th>xP</th>
            <th></th>
          </tr>
        </thead>
        <tbody>
          {POSITION_ORDER.flatMap((position) =>
            starters
              .filter((p) => p.position === position)
              .map((player) => (
                <tr key={player.id}>
                  <td>{player.position}</td>
                  <td>{player.name}</td>
                  <td>{player.team}</td>
                  <td>£{player.price.toFixed(1)}m</td>
                  <td>{player.xP.toFixed(2)}</td>
                  <td>
                    {player.is_captain && <span className="badge captain">C</span>}
                    {player.is_vice_captain && <span className="badge vice-captain">VC</span>}
                  </td>
                </tr>
              )),
          )}
        </tbody>
      </table>

      <h3>Bench</h3>
      <table className="player-table">
        <thead>
          <tr>
            <th>Position</th>
            <th>Player</th>
            <th>Team</th>
            <th>Price</th>
            <th>xP</th>
          </tr>
        </thead>
        <tbody>
          {bench.map((player) => (
            <tr key={player.id}>
              <td>{player.position}</td>
              <td>{player.name}</td>
              <td>{player.team}</td>
              <td>£{player.price.toFixed(1)}m</td>
              <td>{player.xP.toFixed(2)}</td>
            </tr>
          ))}
        </tbody>
      </table>
    </div>
  )
}
