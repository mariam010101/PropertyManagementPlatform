import { useState, type FormEvent } from 'react'
import { Link } from 'react-router-dom'
import { api } from '../api/client'

/**
 * Public password-recovery entry point. Email delivery is not configured yet
 * (GAP-005), so when the API returns a reset token it is shown in a clearly marked
 * development panel instead of pretending an email was sent.
 */
export default function ForgotPasswordPage() {
  const [email, setEmail] = useState('')
  const [resetToken, setResetToken] = useState<string | null>(null)
  const [sent, setSent] = useState(false)
  const [error, setError] = useState('')
  const [busy, setBusy] = useState(false)

  const submit = async (event: FormEvent) => {
    event.preventDefault()
    setError('')
    setBusy(true)
    try {
      const result = await api.forgotPassword(email)
      setResetToken(result?.resetToken ?? null)
      setSent(true)
    } catch (err) {
      setError((err as Error).message)
    } finally {
      setBusy(false)
    }
  }

  return (
    <div className="auth-card">
      <h1>Reset your password</h1>
      {sent ? (
        <>
          <p className="muted">
            If <strong>{email}</strong> matches an account, a reset request has been created.
          </p>
          {resetToken && (
            <div className="dev-note">
              <strong>Development only</strong>
              <p className="muted">
                Email delivery isn’t configured yet, so the reset token is shown here instead.
              </p>
              <code className="dev-note__token">{resetToken}</code>
              <Link
                className="btn btn--primary"
                to={`/reset-password?email=${encodeURIComponent(email)}&token=${encodeURIComponent(resetToken)}`}
              >
                Continue to reset
              </Link>
            </div>
          )}
          <p className="muted">
            <Link to="/login">Back to sign in</Link>
          </p>
        </>
      ) : (
        <form onSubmit={submit}>
          <p className="muted">Enter the email address on your account.</p>
          <label>
            Email
            <input
              type="email"
              value={email}
              onChange={(e) => setEmail(e.target.value)}
              required
              autoFocus
              autoComplete="email"
            />
          </label>
          {error && <div className="error">{error}</div>}
          <button type="submit" disabled={busy}>
            {busy ? 'Working…' : 'Send reset link'}
          </button>
          <p className="muted">
            <Link to="/login">Back to sign in</Link>
          </p>
        </form>
      )}
    </div>
  )
}
