import type { Squad } from "../types"
import { pickBestXI } from "../logic/bestXI"

const POSITION_ORDER = ["Goalkeeper", "Defender", "Midfielder", "Forward"] as const

interface Props {
  squad: Squad
}

export function BestXIView({ squad }: Props) {
  const result = pickBestXI(squad.picks)
  const totalXP = result.startingXI.reduce((sum, p) => sum + p.xP, 0)

  return (
    <div>
      <p className="squad-summary">
        Suggested formation {result.formation} · Projected {totalXP.toFixed(2)} pts (before captain bonus)
      </p>
      <p className="squad-summary">
        Captain: <strong>{result.captain.name}</strong> · Vice-captain: <strong>{result.viceCaptain.name}</strong>
      </p>

      <h3>Starting XI</h3>
      <table className="player-table">
        <thead>
          <tr>
            <th>Position</th>
            <th>Player</th>
            <th>Team</th>
            <th>xP</th>
            <th></th>
          </tr>
        </thead>
        <tbody>
          {POSITION_ORDER.flatMap((position) =>
            result.startingXI
              .filter((p) => p.position === position)
              .map((player) => (
                <tr key={player.id}>
                  <td>{player.position}</td>
                  <td>{player.name}</td>
                  <td>{player.team}</td>
                  <td>{player.xP.toFixed(2)}</td>
                  <td>
                    {player.id === result.captain.id && <span className="badge captain">C</span>}
                    {player.id === result.viceCaptain.id && <span className="badge vice-captain">VC</span>}
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
            <th>xP</th>
          </tr>
        </thead>
        <tbody>
          {result.bench.map((player) => (
            <tr key={player.id}>
              <td>{player.position}</td>
              <td>{player.name}</td>
              <td>{player.team}</td>
              <td>{player.xP.toFixed(2)}</td>
            </tr>
          ))}
        </tbody>
      </table>
    </div>
  )
}
