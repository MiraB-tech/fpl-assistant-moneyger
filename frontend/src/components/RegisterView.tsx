import { useState } from "react"
import type { User } from "../types"
import { register } from "../data"

interface Props {
  onRegistered: (user: User) => void
  onSwitchToLogin: () => void
}

export function RegisterView({ onRegistered, onSwitchToLogin }: Props) {
  const [email, setEmail] = useState("")
  const [password, setPassword] = useState("")
  const [fplTeamId, setFplTeamId] = useState("")
  const [error, setError] = useState<string | null>(null)
  const [submitting, setSubmitting] = useState(false)

  async function handleSubmit(e: React.FormEvent) {
    e.preventDefault()
    setError(null)
    setSubmitting(true)
    try {
      const user = await register(email, password, fplTeamId ? Number(fplTeamId) : undefined)
      onRegistered(user)
    } catch (err) {
      setError((err as Error).message)
    } finally {
      setSubmitting(false)
    }
  }

  return (
    <form className="auth-form" onSubmit={handleSubmit}>
      <h2>Create an account</h2>
      {error && <p className="error">{error}</p>}
      <label>
        Email
        <input type="email" value={email} onChange={(e) => setEmail(e.target.value)} required />
      </label>
      <label>
        Password
        <input
          type="password"
          value={password}
          onChange={(e) => setPassword(e.target.value)}
          minLength={8}
          required
        />
      </label>
      <label>
        FPL team ID (optional, can add later)
        <input
          type="number"
          value={fplTeamId}
          onChange={(e) => setFplTeamId(e.target.value)}
          placeholder="e.g. 5254189"
        />
      </label>
      <button type="submit" disabled={submitting}>
        {submitting ? "Creating account..." : "Create account"}
      </button>
      <p>
        Already have an account?{" "}
        <button type="button" className="link-button" onClick={onSwitchToLogin}>
          Log in
        </button>
      </p>
    </form>
  )
}
