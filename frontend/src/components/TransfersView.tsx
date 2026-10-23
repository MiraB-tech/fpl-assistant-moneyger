import type { Player, Squad } from "../types"
import { suggestTransfers } from "../logic/transfers"

interface Props {
  squad: Squad
  allPlayers: Player[]
}

// +£1.5m when a transfer frees up money, −£0.5m when it costs extra
function formatMoney(amount: number): string {
  const sign = amount > 0 ? "+" : amount < 0 ? "−" : ""
  return `${sign}£${Math.abs(amount).toFixed(1)}m`
}

export function TransfersView({ squad, allPlayers }: Props) {
  const suggestions = suggestTransfers(squad.picks, allPlayers, squad.bank)

  if (suggestions.length === 0) {
    return <p className="squad-summary">No affordable upgrades found for your current squad and bank.</p>
  }

  return (
    <div className="view">
      <p className="squad-summary">
        Top {suggestions.length} suggested upgrade{suggestions.length > 1 ? "s" : ""}, ranked by projected points
        gained. Prices ignore FPL's sell-price tax, so treat costs as an estimate.
      </p>
      <div className="table-scroll">
        <table className="player-table">
          <thead>
            <tr>
              <th>Out</th>
              <th>In</th>
              <th>Position</th>
              <th>xP gain</th>
              <th>Value to team</th>
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
                <td className={s.valueToTeam > 0 ? "positive" : s.valueToTeam < 0 ? "negative" : ""}>
                  {formatMoney(s.valueToTeam)}
                </td>
              </tr>
            ))}
          </tbody>
        </table>
      </div>
    </div>
  )
}
