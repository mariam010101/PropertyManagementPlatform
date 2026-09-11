import { useState, type FormEvent } from 'react'
import { Link, useSearchParams } from 'react-router-dom'
import { api } from '../api/client'

/**
 * Public reset screen. Email and token arrive pre-filled from the recovery link
 * (and the token can be pasted when a real email transport exists).
 */
export default function ResetPasswordPage() {
  const [params] = useSearchParams()
  const [email, setEmail] = useState(params.get('email') ?? '')
  const [token, setToken] = useState(params.get('token') ?? '')
  const [password, setPassword] = useState('')
  const [confirm, setConfirm] = useState('')
  const [formError, setFormError] = useState('')
  const [confirmError, setConfirmError] = useState('')
  const [done, setDone] = useState(false)
  const [busy, setBusy] = useState(false)

  const submit = async (event: FormEvent) => {
    event.preventDefault()
    setFormError('')
    setConfirmError('')

    if (password.length < 8) {
      setFormError('Your new password must be at least 8 characters long.')
      return
    }
    if (password !== confirm) {
      setConfirmError('Passwords do not match.')
      return
    }

    setBusy(true)
    try {
      await api.resetPassword(email, token, password)
      setDone(true)
    } catch (err) {
      setFormError((err as Error).message)
    } finally {
      setBusy(false)
    }
  }

  if (done) {
    return (
      <div className="auth-card">
        <h1>Password updated</h1>
        <p className="muted">You can now sign in with your new password.</p>
        <Link className="btn btn--primary" to="/login">
          Sign in
        </Link>
      </div>
    )
  }

  return (
    <div className="auth-card">
      <h1>Choose a new password</h1>
      <form onSubmit={submit}>
        <label>
          Email
          <input type="email" value={email} onChange={(e) => setEmail(e.target.value)} required autoComplete="email" />
        </label>
        <label>
          Reset token
          <input value={token} onChange={(e) => setToken(e.target.value)} required />
        </label>
        <label>
          New password
          <input
            type="password"
            value={password}
            onChange={(e) => setPassword(e.target.value)}
            required
            minLength={8}
            autoComplete="new-password"
            aria-invalid={formError.includes('password')}
          />
        </label>
        <label>
          Confirm new password
          <input
            type="password"
            value={confirm}
            onChange={(e) => setConfirm(e.target.value)}
            required
            autoComplete="new-password"
            aria-invalid={confirmError.length > 0}
          />
        </label>
        {confirmError && <p className="field-error">{confirmError}</p>}
        {formError && <div className="error">{formError}</div>}
        <button type="submit" disabled={busy}>
          {busy ? 'Updating…' : 'Update password'}
        </button>
        <p className="muted">
          <Link to="/login">Back to sign in</Link>
        </p>
      </form>
    </div>
  )
}
