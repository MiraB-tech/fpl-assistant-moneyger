// Wrappers around fetch() for the backend API (frontend/api/). Every call
// sends credentials: "include" so the httpOnly session cookie goes along
// with same-origin requests automatically.

import type { Player, Squad, User } from "./types"

async function apiFetch<T>(path: string, options: RequestInit = {}): Promise<T> {
  const response = await fetch(path, {
    ...options,
    credentials: "include",
    headers: { "Content-Type": "application/json", ...options.headers },
  })

  if (!response.ok) {
    const body = await response.json().catch(() => ({}))
    throw new Error(body.error || `Request to ${path} failed (${response.status})`)
  }

  if (response.status === 204) {
    return undefined as T
  }
  return response.json()
}

export function register(email: string, password: string, fplTeamId?: number): Promise<User> {
  return apiFetch<User>("/api/auth/register", {
    method: "POST",
    body: JSON.stringify({ email, password, fpl_team_id: fplTeamId }),
  })
}

export function login(email: string, password: string): Promise<User> {
  return apiFetch<User>("/api/auth/login", {
    method: "POST",
    body: JSON.stringify({ email, password }),
  })
}

export function logout(): Promise<void> {
  return apiFetch<void>("/api/auth/logout", { method: "POST" })
}

export async function getCurrentUser(): Promise<User | null> {
  try {
    return await apiFetch<User>("/api/auth/me")
  } catch {
    return null
  }
}

export function setTeamId(fplTeamId: number): Promise<User> {
  return apiFetch<User>("/api/team", {
    method: "PUT",
    body: JSON.stringify({ fpl_team_id: fplTeamId }),
  })
}

export function getCurrentGameweek(): Promise<{ current_gw: number | null; next_gw: number | null }> {
  return apiFetch("/api/gameweek/current")
}

export function loadAllPlayers(gameweek: number): Promise<Player[]> {
  return apiFetch<Player[]>(`/api/predictions?gw=${gameweek}`)
}

export function loadSquad(gameweek: number): Promise<Squad> {
  return apiFetch<Squad>(`/api/squad?gw=${gameweek}`)
}

export interface AdvanceResult {
  gw: number
  evaluation: { evaluated: boolean; reason?: string; num_players?: number }
  refresh: { refreshed: boolean; player_count: number; last_refreshed_at: string }
}

export function advanceGameweek(nextGw: number): Promise<AdvanceResult> {
  return apiFetch<AdvanceResult>("/api/gameweek/advance", {
    method: "POST",
    body: JSON.stringify({ next_gw: nextGw }),
  })
}
