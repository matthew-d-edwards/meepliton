import { useState, FormEvent } from 'react'
import { Link, useSearchParams, useNavigate } from 'react-router-dom'
import './auth.css'

export default function ResetPasswordPage() {
  const [searchParams] = useSearchParams()
  const navigate = useNavigate()

  const token = searchParams.get('token') ?? ''
  const userId = searchParams.get('userId') ?? ''

  const [newPassword, setNewPassword] = useState('')
  const [confirmPassword, setConfirmPassword] = useState('')
  const [submitting, setSubmitting] = useState(false)
  const [expired, setExpired] = useState(false)

  const [newPasswordError, setNewPasswordError] = useState<string | null>(null)
  const [confirmPasswordError, setConfirmPasswordError] = useState<string | null>(null)

  // If the link is missing required params, treat as expired immediately
  const missingParams = !token || !userId

  function validate(): boolean {
    let valid = true
    setNewPasswordError(null)
    setConfirmPasswordError(null)

    if (!newPassword) {
      setNewPasswordError('Password is required.')
      valid = false
    } else if (newPassword.length < 8) {
      setNewPasswordError('Password must be at least 8 characters.')
      valid = false
    }

    if (!confirmPassword) {
      setConfirmPasswordError('Please confirm your password.')
      valid = false
    } else if (newPassword && newPassword !== confirmPassword) {
      setConfirmPasswordError('Passwords do not match.')
      valid = false
    }

    return valid
  }

  async function handleSubmit(e: FormEvent) {
    e.preventDefault()
    if (!validate()) return

    setSubmitting(true)
    try {
      const res = await fetch('/api/auth/reset-password', {
        method: 'POST',
        credentials: 'include',
        headers: { 'Content-Type': 'application/json' },
        body: JSON.stringify({ userId, token, newPassword }),
      })

      if (res.ok || res.status === 204) {
        navigate('/sign-in?reset=success', { replace: true })
        return
      }

      setExpired(true)
    } catch {
      setExpired(true)
    } finally {
      setSubmitting(false)
    }
  }

  if (missingParams || expired) {
    return (
      <main className="auth-page">
        <div className="auth-card">
          <Link to="/" className="auth-logo">MEEPLITON</Link>
          <h1 className="auth-title">Link expired</h1>
          <p className="auth-error" role="alert">
            This reset link has expired or is invalid — request a new one below.
          </p>
          <nav className="auth-links">
            <Link to="/forgot-password">Request a new reset link</Link>
            <Link to="/sign-in">Back to sign in</Link>
          </nav>
        </div>
      </main>
    )
  }

  return (
    <main className="auth-page">
      <div className="auth-card">
        <Link to="/" className="auth-logo">MEEPLITON</Link>
        <h1 className="auth-title">New password</h1>

        <form onSubmit={handleSubmit} className="auth-form" noValidate>
          <label>
            New password
            <input
              type="password"
              value={newPassword}
              onChange={e => { setNewPassword(e.target.value); setNewPasswordError(null) }}
              autoComplete="new-password"
              aria-invalid={newPasswordError ? 'true' : 'false'}
              aria-describedby={newPasswordError ? 'new-password-error' : undefined}
              disabled={submitting}
            />
            {newPasswordError && <span id="new-password-error" className="auth-field-error">{newPasswordError}</span>}
          </label>

          <label>
            Confirm password
            <input
              type="password"
              value={confirmPassword}
              onChange={e => { setConfirmPassword(e.target.value); setConfirmPasswordError(null) }}
              autoComplete="new-password"
              aria-invalid={confirmPasswordError ? 'true' : 'false'}
              aria-describedby={confirmPasswordError ? 'confirm-password-error' : undefined}
              disabled={submitting}
            />
            {confirmPasswordError && <span id="confirm-password-error" className="auth-field-error">{confirmPasswordError}</span>}
          </label>

          <button type="submit" className="auth-submit" disabled={submitting}>
            {submitting ? 'Setting password…' : 'Set new password'}
          </button>
        </form>

        <nav className="auth-links">
          <Link to="/sign-in">Back to sign in</Link>
        </nav>
      </div>
    </main>
  )
}
