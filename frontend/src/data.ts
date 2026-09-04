// Small wrappers around fetch() for reading the static JSON files that
// copy-data.js drops into public/data. Vite serves everything in public/
// straight off the root URL, so "public/data/my_squad.json" is just
// fetched as "/data/my_squad.json".

import type { Player, Squad } from "./types"

export async function loadSquad(): Promise<Squad> {
  const response = await fetch("/data/my_squad.json")
  if (!response.ok) {
    throw new Error(`Failed to load squad (${response.status})`)
  }
  return response.json()
}

export async function loadAllPlayers(gameweek: number): Promise<Player[]> {
  const response = await fetch(`/data/gw${gameweek}_predictions.json`)
  if (!response.ok) {
    throw new Error(`Failed to load predictions for GW${gameweek} (${response.status})`)
  }
  return response.json()
}
