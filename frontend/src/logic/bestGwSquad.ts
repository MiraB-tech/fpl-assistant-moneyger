// Builds the best possible FPL squad for one gameweek from scratch — any
// player in the game, not just the ones you own — under FPL's real
// squad rules:
//   - £100m to spend on all 15 players
//   - exactly 2 goalkeepers, 5 defenders, 5 midfielders, 3 forwards
//   - at most 3 players from any one club
//
// Only the starting XI scores, so the money goes where the points are:
// the bench is filled with the cheapest legal players, and everything
// left over is spent on the strongest XI that fits.
//
// How it searches: for each legal formation, start with the highest-xP
// players, sell down the ones that give up the fewest points per £1m
// saved until the squad is under budget, then keep trying one- and
// two-player swaps that add points without breaking any rule. This
// finds a very strong team quickly, but it's a smart search rather than
// a mathematically guaranteed best — on rare occasions a slightly better
// combination may exist.

import type { Player, Position } from "../types"

export const BUDGET = 100
const MAX_PER_CLUB = 3
const SQUAD_SHAPE: Record<Position, number> = { Goalkeeper: 2, Defender: 5, Midfielder: 5, Forward: 3 }
const POSITIONS: Position[] = ["Goalkeeper", "Defender", "Midfielder", "Forward"]

export interface BestGwSquad {
  startingXI: Player[]
  bench: Player[]
  captain: Player
  formation: string
  squadValue: number
  // Starting XI's projected points with the captain's doubled
  projectedPoints: number
}

// Prices are worked in tenths of a million (6.5 → 65) so adding them up
// never hits floating-point rounding noise like 99.99999.
const tenths = (price: number) => Math.round(price * 10)
const BUDGET_TENTHS = BUDGET * 10

// Same formation rules as the "Your Best XI" picker: 1 GK, 3-5 DEF,
// 2-5 MID, 1-3 FWD, 11 players total.
const FORMATIONS: [number, number, number][] = []
for (let def = 3; def <= 5; def++) {
  for (let mid = 2; mid <= 5; mid++) {
    const fwd = 10 - def - mid
    if (fwd >= 1 && fwd <= 3) FORMATIONS.push([def, mid, fwd])
  }
}

// Throws out players nobody would ever pick: if 5+ others in the same
// position are both cheaper (or equal) and projected to score at least as
// much, this player can't make the best team. Keeping a few spare
// "nearly as good" options (rather than only the very best) leaves room
// to work around the 3-per-club rule. Shrinks ~650 players to a short
// list, which keeps the two-player swap search fast.
function shortlist(players: Player[]): Player[] {
  return players.filter((p) => {
    let betterAndCheaper = 0
    for (const other of players) {
      if (other === p) continue
      if (other.price <= p.price && other.xP >= p.xP && (other.price < p.price || other.xP > p.xP)) {
        betterAndCheaper++
        if (betterAndCheaper >= 5) return false
      }
    }
    return true
  })
}

function buildForFormation(
  formation: [number, number, number],
  byPosition: Record<Position, Player[]>,
  shortlisted: Record<Position, Player[]>,
): BestGwSquad | null {
  const [def, mid, fwd] = formation
  const xiShape: Record<Position, number> = { Goalkeeper: 1, Defender: def, Midfielder: mid, Forward: fwd }

  const inSquad = new Set<number>()
  const perClub = new Map<string, number>()
  const clubCount = (team: string) => perClub.get(team) ?? 0
  const add = (p: Player) => {
    inSquad.add(p.id)
    perClub.set(p.team, clubCount(p.team) + 1)
  }
  const remove = (p: Player) => {
    inSquad.delete(p.id)
    perClub.set(p.team, clubCount(p.team) - 1)
  }

  // Would the club limit still hold after swapping these players out for these ones?
  const clubsOkAfter = (out: Player[], incoming: Player[]) =>
    incoming.every((q) => {
      const count = clubCount(q.team) - out.filter((p) => p.team === q.team).length + incoming.filter((r) => r.team === q.team).length
      return count <= MAX_PER_CLUB
    })

  // 1. Bench: the cheapest legal players for whatever slots the XI leaves over
  const bench: Player[] = []
  for (const position of POSITIONS) {
    const needed = SQUAD_SHAPE[position] - xiShape[position]
    const cheapestFirst = [...byPosition[position]].sort((a, b) => a.price - b.price || b.xP - a.xP)
    for (const p of cheapestFirst) {
      if (bench.filter((b) => b.position === position).length === needed) break
      if (clubCount(p.team) >= MAX_PER_CLUB) continue
      bench.push(p)
      add(p)
    }
  }

  // 2. XI: the highest-xP players in each position, ignoring cost for now
  const xi: Player[] = []
  for (const position of POSITIONS) {
    const bestFirst = [...byPosition[position]].sort((a, b) => b.xP - a.xP)
    for (const p of bestFirst) {
      if (xi.filter((x) => x.position === position).length === xiShape[position]) break
      if (inSquad.has(p.id) || clubCount(p.team) >= MAX_PER_CLUB) continue
      xi.push(p)
      add(p)
    }
  }

  let cost = [...bench, ...xi].reduce((sum, p) => sum + tenths(p.price), 0)

  // 3. Over budget? Repeatedly make the downgrade that loses the fewest
  // points for each £1m it saves, until the squad fits.
  while (cost > BUDGET_TENTHS) {
    let best: { index: number; replacement: Player; pointsLostPerTenth: number } | null = null
    for (let index = 0; index < xi.length; index++) {
      const p = xi[index]
      for (const q of byPosition[p.position]) {
        const saving = tenths(p.price) - tenths(q.price)
        if (saving <= 0 || inSquad.has(q.id) || !clubsOkAfter([p], [q])) continue
        const pointsLostPerTenth = (p.xP - q.xP) / saving
        if (best === null || pointsLostPerTenth < best.pointsLostPerTenth) {
          best = { index, replacement: q, pointsLostPerTenth }
        }
      }
    }
    if (best === null) return null // no way to get this formation under budget
    const { index, replacement } = best
    cost += tenths(replacement.price) - tenths(xi[index].price)
    remove(xi[index])
    add(replacement)
    xi[index] = replacement
  }

  // 4. Keep making whichever swap (one player, or two at once — e.g. sell
  // a midfielder to fund a better forward) adds the most points, until no
  // legal swap improves the team.
  for (;;) {
    let bestGain = 1e-9
    let bestMove: { out: number[]; incoming: Player[] } | null = null

    xi.forEach((p1, i) => {
      for (const q1 of shortlisted[p1.position]) {
        if (inSquad.has(q1.id)) continue
        const costAfterOne = cost - tenths(p1.price) + tenths(q1.price)

        // Single swap
        const gain = q1.xP - p1.xP
        if (gain > bestGain && costAfterOne <= BUDGET_TENTHS && clubsOkAfter([p1], [q1])) {
          bestGain = gain
          bestMove = { out: [i], incoming: [q1] }
        }

        // Paired swap with a later XI player
        for (let j = i + 1; j < xi.length; j++) {
          const p2 = xi[j]
          for (const q2 of shortlisted[p2.position]) {
            if (q2.id === q1.id || inSquad.has(q2.id)) continue
            const pairGain = q1.xP + q2.xP - p1.xP - p2.xP
            if (pairGain <= bestGain) continue
            if (costAfterOne - tenths(p2.price) + tenths(q2.price) > BUDGET_TENTHS) continue
            if (!clubsOkAfter([p1, p2], [q1, q2])) continue
            bestGain = pairGain
            bestMove = { out: [i, j], incoming: [q1, q2] }
          }
        }
      }
    })

    if (bestMove === null) break
    const move: { out: number[]; incoming: Player[] } = bestMove
    move.out.forEach((index, k) => {
      const incoming = move.incoming[k]
      cost += tenths(incoming.price) - tenths(xi[index].price)
      remove(xi[index])
      add(incoming)
      xi[index] = incoming
    })
  }

  const captain = xi.reduce((top, p) => (p.xP > top.xP ? p : top))
  const xiPoints = xi.reduce((sum, p) => sum + p.xP, 0)

  return {
    startingXI: xi,
    bench,
    captain,
    formation: `${def}-${mid}-${fwd}`,
    squadValue: cost / 10,
    projectedPoints: xiPoints + captain.xP,
  }
}

export function pickBestGwSquad(allPlayers: Player[]): BestGwSquad | null {
  const byPosition = {} as Record<Position, Player[]>
  const shortlisted = {} as Record<Position, Player[]>
  for (const position of POSITIONS) {
    byPosition[position] = allPlayers.filter((p) => p.position === position)
    shortlisted[position] = shortlist(byPosition[position])
  }

  // Try every formation and keep whichever produces the most points
  let best: BestGwSquad | null = null
  for (const formation of FORMATIONS) {
    const squad = buildForFormation(formation, byPosition, shortlisted)
    if (squad && (best === null || squad.projectedPoints > best.projectedPoints)) {
      best = squad
    }
  }
  return best
}
