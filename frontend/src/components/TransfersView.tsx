import type { Player, Squad } from "../types"
import { suggestTransfers } from "../logic/transfers"

interface Props {
  squad: Squad
  allPlayers: Player[]
}

export function TransfersView({ squad, allPlayers }: Props) {
  const suggestions = suggestTransfers(squad.picks, allPlayers, squad.bank)

  if (suggestions.length === 0) {
    return <p className="squad-summary">No affordable upgrades found for your current squad and bank.</p>
  }

  return (
    <div>
      <p className="squad-summary">
        Top {suggestions.length} suggested upgrade{suggestions.length > 1 ? "s" : ""}, ranked by projected points
        gained. Prices ignore FPL's sell-price tax, so treat costs as an estimate.
      </p>
      <table className="player-table">
        <thead>
          <tr>
            <th>Out</th>
            <th>In</th>
            <th>Position</th>
            <th>xP gain</th>
            <th>Cost change</th>
          </tr>
        </thead>
        <tbody>
          {suggestions.map((s) => (
            <tr key={`${s.out.id}-${s.in.id}`}>
              <td>
                {s.out.name} ({s.out.team})
              </td>
              <td>
                {s.in.name} ({s.in.team})
              </td>
              <td>{s.out.position}</td>
              <td className="positive">+{s.xpGain.toFixed(2)}</td>
              <td className={s.costChange > 0 ? "negative" : "positive"}>
                {s.costChange >= 0 ? "+" : ""}
                {s.costChange.toFixed(1)}m
              </td>
            </tr>
          ))}
        </tbody>
      </table>
    </div>
  )
}
