// A simple, transparent transfer-upgrade finder. For each player you own,
// look across the entire player pool for the single best replacement:
// same position, not already yours, affordable, and projected to score
// more than the player it would replace.
//
// Simplification worth knowing: this compares against your player's
// current price plus your bank, ignoring FPL's sell-price "profit tax"
// (you only get half of any price rise back when you sell). A suggested
// transfer might cost a little more in real life than shown here.

import type { Player, SquadPick } from "../types"

export interface TransferSuggestion {
  out: SquadPick
  in: Player
  xpGain: number
  costChange: number
}

export function suggestTransfers(
  squad: SquadPick[],
  allPlayers: Player[],
  bank: number,
  maxSuggestions = 5,
): TransferSuggestion[] {
  const ownedIds = new Set(squad.map((p) => p.id))
  const suggestions: TransferSuggestion[] = []

  for (const owned of squad) {
    const budget = owned.price + bank
    let bestReplacement: Player | null = null

    for (const candidate of allPlayers) {
      if (ownedIds.has(candidate.id)) continue
      if (candidate.position !== owned.position) continue
      if (candidate.price > budget) continue
      if (candidate.xP <= owned.xP) continue
      if (bestReplacement === null || candidate.xP > bestReplacement.xP) {
        bestReplacement = candidate
      }
    }

    if (bestReplacement !== null) {
      suggestions.push({
        out: owned,
        in: bestReplacement,
        xpGain: bestReplacement.xP - owned.xP,
        costChange: bestReplacement.price - owned.price,
      })
    }
  }

  return suggestions.sort((a, b) => b.xpGain - a.xpGain).slice(0, maxSuggestions)
}
