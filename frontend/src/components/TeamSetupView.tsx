import { useState } from "react"
import type { User } from "../types"
import { setTeamId } from "../data"

interface Props {
  onTeamSet: (user: User) => void
}

export function TeamSetupView({ onTeamSet }: Props) {
  const [fplTeamId, setFplTeamId] = useState("")
  const [error, setError] = useState<string | null>(null)
  const [submitting, setSubmitting] = useState(false)

  async function handleSubmit(e: React.FormEvent) {
    e.preventDefault()
    setError(null)
    setSubmitting(true)
    try {
      const user = await setTeamId(Number(fplTeamId))
      onTeamSet(user)
    } catch (err) {
      setError((err as Error).message)
    } finally {
      setSubmitting(false)
    }
  }

  return (
    <form className="auth-form" onSubmit={handleSubmit}>
      <h2>Register your FPL team</h2>
      <p className="squad-summary">
        Find your team ID in the URL when you view your team on the FPL site: fantasy.premierleague.com/entry/
        <strong>this number</strong>/event/...
      </p>
      {error && <p className="error">{error}</p>}
      <label>
        FPL team ID
        <input
          type="number"
          value={fplTeamId}
          onChange={(e) => setFplTeamId(e.target.value)}
          placeholder="e.g. 5254189"
          required
        />
      </label>
      <button type="submit" disabled={submitting}>
        {submitting ? "Checking..." : "Save team"}
      </button>
    </form>
  )
}
