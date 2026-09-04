// Given the 15 players you actually own, work out which 11 should start,
// in which formation, and who should captain/vice-captain — all based on
// this gameweek's predicted points (xP). No new players considered here,
// just the best arrangement of the squad you already have.

import type { SquadPick } from "../types"

export interface BestXIResult {
  startingXI: SquadPick[]
  bench: SquadPick[]
  captain: SquadPick
  viceCaptain: SquadPick
  formation: string
}

// Every legal FPL formation: exactly 1 goalkeeper, plus 10 outfielders
// split across defence/midfield/attack within FPL's own limits
// (3-5 defenders, 2-5 midfielders, 1-3 forwards, adding up to 10).
const VALID_FORMATIONS: [defenders: number, midfielders: number, forwards: number][] = []
for (let def = 3; def <= 5; def++) {
  for (let mid = 2; mid <= 5; mid++) {
    const fwd = 10 - def - mid
    if (fwd >= 1 && fwd <= 3) {
      VALID_FORMATIONS.push([def, mid, fwd])
    }
  }
}

function byPredictedPointsDescending(a: SquadPick, b: SquadPick): number {
  return b.xP - a.xP
}

export function pickBestXI(picks: SquadPick[]): BestXIResult {
  const goalkeepers = picks.filter((p) => p.position === "Goalkeeper").sort(byPredictedPointsDescending)
  const defenders = picks.filter((p) => p.position === "Defender").sort(byPredictedPointsDescending)
  const midfielders = picks.filter((p) => p.position === "Midfielder").sort(byPredictedPointsDescending)
  const forwards = picks.filter((p) => p.position === "Forward").sort(byPredictedPointsDescending)

  const bestGoalkeeper = goalkeepers[0]

  let bestTotalXP = -Infinity
  let bestOutfield: SquadPick[] = []
  let bestFormation = ""

  for (const [defCount, midCount, fwdCount] of VALID_FORMATIONS) {
    if (defenders.length < defCount || midfielders.length < midCount || forwards.length < fwdCount) {
      continue // this squad doesn't have enough players in one line to fill this formation
    }

    const outfield = [
      ...defenders.slice(0, defCount),
      ...midfielders.slice(0, midCount),
      ...forwards.slice(0, fwdCount),
    ]
    const totalXP = bestGoalkeeper.xP + outfield.reduce((sum, player) => sum + player.xP, 0)

    if (totalXP > bestTotalXP) {
      bestTotalXP = totalXP
      bestOutfield = outfield
      bestFormation = `${defCount}-${midCount}-${fwdCount}`
    }
  }

  const startingXI = [bestGoalkeeper, ...bestOutfield]
  const startingIds = new Set(startingXI.map((p) => p.id))
  const bench = picks.filter((p) => !startingIds.has(p.id)).sort((a, b) => a.squad_position - b.squad_position)

  const byPoints = [...startingXI].sort(byPredictedPointsDescending)
  const captain = byPoints[0]
  const viceCaptain = byPoints[1]

  return { startingXI, bench, captain, viceCaptain, formation: bestFormation }
}
