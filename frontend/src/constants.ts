import type { Position } from "./types"

// Top-to-bottom is Forward-to-Goalkeeper, matching how the rows are
// physically laid out on the pitch (see PITCH_ROWS below).
export const POSITION_LABELS: Record<Position, string> = {
  Forward: "FWD",
  Midfielder: "MID",
  Defender: "DEF",
  Goalkeeper: "GK",
}

// Bottom-to-top, same orientation as FPL's own "Pick Team" screen:
// goalkeeper nearest your own goal (bottom), attackers furthest away (top).
export const PITCH_ROWS: Position[] = ["Forward", "Midfielder", "Defender", "Goalkeeper"]
