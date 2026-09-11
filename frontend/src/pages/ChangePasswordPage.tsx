import { useState, type FormEvent } from 'react'
import { api } from '../api/client'

/**
 * Authenticated password change, reached from the user menu inside the shell.
 */
export default function ChangePasswordPage() {
  const [currentPassword, setCurrentPassword] = useState('')
  const [newPassword, setNewPassword] = useState('')
  const [confirm, setConfirm] = useState('')
  const [formError, setFormError] = useState('')
  const [confirmError, setConfirmError] = useState('')
  const [success, setSuccess] = useState('')
  const [busy, setBusy] = useState(false)

  const submit = async (event: FormEvent) => {
    event.preventDefault()
    setFormError('')
    setConfirmError('')
    setSuccess('')

    if (newPassword.length < 8) {
      setFormError('Your new password must be at least 8 characters long.')
      return
    }
    if (newPassword !== confirm) {
      setConfirmError('Passwords do not match.')
      return
    }

    setBusy(true)
    try {
      await api.changePassword(currentPassword, newPassword)
      setCurrentPassword('')
      setNewPassword('')
      setConfirm('')
      setSuccess('Password updated. Other signed-in sessions are not revoked automatically.')
    } catch (err) {
      setFormError((err as Error).message)
    } finally {
      setBusy(false)
    }
  }

  return (
    <div className="container">
      <h2>Change password</h2>
      <div className="panel">
        <form onSubmit={submit} className="stack">
          <label>
            Current password
            <input
              type="password"
              value={currentPassword}
              onChange={(e) => setCurrentPassword(e.target.value)}
              required
              autoComplete="current-password"
            />
          </label>
          <label>
            New password
            <input
              type="password"
              value={newPassword}
              onChange={(e) => setNewPassword(e.target.value)}
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
          {success && <div className="hint">{success}</div>}
          <button type="submit" disabled={busy}>
            {busy ? 'Updating…' : 'Update password'}
          </button>
        </form>
      </div>
    </div>
  )
}
