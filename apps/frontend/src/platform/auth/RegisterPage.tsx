import { useState, FormEvent } from 'react'
import { Link, Navigate } from 'react-router-dom'
import { useAuth } from './AuthContext'
import './auth.css'

export default function RegisterPage() {
  const { user, loading } = useAuth()

  const [displayName, setDisplayName] = useState('')
  const [email, setEmail] = useState('')
  const [password, setPassword] = useState('')
  const [submitting, setSubmitting] = useState(false)
  const [success, setSuccess] = useState(false)
  const [globalError, setGlobalError] = useState<string | null>(null)

  const [displayNameError, setDisplayNameError] = useState<string | null>(null)
  const [emailError, setEmailError] = useState<string | null>(null)
  const [passwordError, setPasswordError] = useState<string | null>(null)

  if (loading) return null
  if (user) return <Navigate to="/lobby" replace />

  function validate(): boolean {
    let valid = true
    setDisplayNameError(null)
    setEmailError(null)
    setPasswordError(null)

    if (!displayName.trim()) {
      setDisplayNameError('Display name is required.')
      valid = false
    }
    if (!email.trim()) {
      setEmailError('Email is required.')
      valid = false
    }
    if (!password) {
      setPasswordError('Password is required.')
      valid = false
    } else if (password.length < 8) {
      setPasswordError('Password must be at least 8 characters.')
      valid = false
    }
    return valid
  }

  async function handleSubmit(e: FormEvent) {
    e.preventDefault()
    if (!validate()) return

    setSubmitting(true)
    setGlobalError(null)

    try {
      const res = await fetch('/api/auth/register', {
        method: 'POST',
        credentials: 'include',
        headers: { 'Content-Type': 'application/json' },
        body: JSON.stringify({ displayName: displayName.trim(), email: email.trim(), password }),
      })

      if (res.ok || res.status === 201) {
        setSuccess(true)
        return
      }

      let message = 'Registration failed. Please try again.'
      try {
        const body = (await res.json()) as { title?: string; detail?: string; errors?: Record<string, string[]> }
        if (body.errors) {
          const firstKey = Object.keys(body.errors)[0]
          if (firstKey) message = body.errors[firstKey][0] ?? message
        } else if (body.detail) {
          message = body.detail
        } else if (body.title) {
          message = body.title
        }
      } catch {
        // use default message
      }
      setGlobalError(message)
    } catch {
      setGlobalError('Network error. Please check your connection and try again.')
    } finally {
      setSubmitting(false)
    }
  }

  if (success) {
    return (
      <main className="auth-page">
        <div className="auth-card">
          <Link to="/" className="auth-logo">MEEPLITON</Link>
          <p className="auth-success" role="status">
            Account created! Check your email to confirm your account before signing in.
          </p>
          <nav className="auth-links">
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
        <h1 className="auth-title">Create Account</h1>

        <form onSubmit={handleSubmit} className="auth-form" noValidate>
          <label>
            Display name
            <input
              type="text"
              value={displayName}
              onChange={e => { setDisplayName(e.target.value); setDisplayNameError(null) }}
              autoComplete="nickname"
              aria-invalid={displayNameError ? 'true' : 'false'}
              aria-describedby={displayNameError ? 'displayname-error' : undefined}
              disabled={submitting}
            />
            {displayNameError && <span id="displayname-error" className="auth-field-error">{displayNameError}</span>}
          </label>

          <label>
            Email
            <input
              type="email"
              value={email}
              onChange={e => { setEmail(e.target.value); setEmailError(null) }}
              autoComplete="email"
              aria-invalid={emailError ? 'true' : 'false'}
              aria-describedby={emailError ? 'reg-email-error' : undefined}
              disabled={submitting}
            />
            {emailError && <span id="reg-email-error" className="auth-field-error">{emailError}</span>}
          </label>

          <label>
            Password
            <input
              type="password"
              value={password}
              onChange={e => { setPassword(e.target.value); setPasswordError(null) }}
              autoComplete="new-password"
              aria-invalid={passwordError ? 'true' : 'false'}
              aria-describedby={passwordError ? 'reg-password-error' : undefined}
              disabled={submitting}
            />
            {passwordError && <span id="reg-password-error" className="auth-field-error">{passwordError}</span>}
          </label>

          {globalError && (
            <p className="auth-error" role="alert">{globalError}</p>
          )}

          <button type="submit" className="auth-submit" disabled={submitting}>
            {submitting ? 'Creating account…' : 'Create account'}
          </button>
        </form>

        <nav className="auth-links">
          <span>Already have an account? <Link to="/sign-in">Sign in</Link></span>
        </nav>
      </div>
    </main>
  )
}
