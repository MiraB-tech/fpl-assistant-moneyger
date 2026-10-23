// Real club colors for the "jersey" stand-ins on the pitch view — no
// actual kit graphics, just a colored shape in roughly the right hue so
// squads are visually scannable at a glance, same as real shirts would be.
const TEAM_COLORS: Record<string, string> = {
  ARS: "#EF0107",
  AVL: "#95BFE5",
  BOU: "#DA291C",
  BRE: "#E30613",
  BHA: "#0057B8",
  BUR: "#6C1D45",
  CHE: "#034694",
  CRY: "#1B458F",
  EVE: "#003399",
  FUL: "#000000",
  IPS: "#0044A9",
  LEE: "#FFCD00",
  LEI: "#003090",
  LIV: "#C8102E",
  LUT: "#F78F1E",
  MCI: "#6CABDD",
  MUN: "#DA291C",
  NEW: "#241F20",
  NFO: "#DD0000",
  SHU: "#EE2737",
  SOU: "#D71920",
  SUN: "#EB172B",
  TOT: "#132257",
  WHU: "#7A263A",
  WOL: "#FDB913",
  HUL: "#F18A01",
  BLB: "#009EE0",
  MID: "#DC1E35",
  NOR: "#00A650",
  WBA: "#122F67",
  WAT: "#FBEE23",
}

const FALLBACK_PALETTE = ["#6b7280", "#0ea5e9", "#f97316", "#10b981", "#8b5cf6", "#ec4899"]

export function getTeamColor(code: string): string {
  const known = TEAM_COLORS[code.toUpperCase()]
  if (known) return known

  let hash = 0
  for (let i = 0; i < code.length; i++) {
    hash = (hash * 31 + code.charCodeAt(i)) >>> 0
  }
  return FALLBACK_PALETTE[hash % FALLBACK_PALETTE.length]
}

// Picks readable text (near-black or white) for a given jersey background.
export function getContrastText(hexColor: string): string {
  const hex = hexColor.replace("#", "")
  const r = parseInt(hex.substring(0, 2), 16)
  const g = parseInt(hex.substring(2, 4), 16)
  const b = parseInt(hex.substring(4, 6), 16)
  const luminance = (0.299 * r + 0.587 * g + 0.114 * b) / 255
  return luminance > 0.6 ? "#14171c" : "#ffffff"
}
