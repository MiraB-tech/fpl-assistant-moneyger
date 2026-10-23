// Shared shapes for the data coming out of the backend API (backend/).

export interface User {
  id: number
  email: string
  fpl_team_id: number | null
}

export type Position = "Goalkeeper" | "Defender" | "Midfielder" | "Forward"

export interface Player {
  id: number
  name: string
  position: Position
  team: string
  price: number
  xP: number
}

export interface SquadPick extends Player {
  current_points: number
  squad_position: number
  is_captain: boolean
  is_vice_captain: boolean
  multiplier: number
}

export interface Squad {
  gameweek: number
  bank: number
  team_value: number
  picks: SquadPick[]
}
