import { useState, FormEvent } from 'react'
import { Link } from 'react-router-dom'
import './auth.css'

export default function ForgotPasswordPage() {
  const [email, setEmail] = useState('')
  const [submitting, setSubmitting] = useState(false)
  const [submitted, setSubmitted] = useState(false)
  const [emailError, setEmailError] = useState<string | null>(null)

  function validate(): boolean {
    setEmailError(null)
    if (!email.trim()) {
      setEmailError('Email is required.')
      return false
    }
    return true
  }

  async function handleSubmit(e: FormEvent) {
    e.preventDefault()
    if (!validate()) return

    setSubmitting(true)
    try {
      await fetch('/api/auth/forgot-password', {
        method: 'POST',
        credentials: 'include',
        headers: { 'Content-Type': 'application/json' },
        body: JSON.stringify({ email: email.trim() }),
      })
    } catch {
      // intentionally swallow errors — always show success message
    } finally {
      setSubmitting(false)
      setSubmitted(true)
    }
  }

  return (
    <main className="auth-page">
      <div className="auth-card">
        <Link to="/" className="auth-logo">MEEPLITON</Link>
        <h1 className="auth-title">Reset Password</h1>

        {submitted ? (
          <>
            <p className="auth-success" role="status">
              If that email is registered, you&apos;ll receive a password reset link shortly.
            </p>
            <nav className="auth-links">
              <Link to="/sign-in">Back to sign in</Link>
            </nav>
          </>
        ) : (
          <>
            <form onSubmit={handleSubmit} className="auth-form" noValidate>
              <label>
                Email
                <input
                  type="email"
                  value={email}
                  onChange={e => { setEmail(e.target.value); setEmailError(null) }}
                  autoComplete="email"
                  aria-invalid={emailError ? 'true' : 'false'}
                  aria-describedby={emailError ? 'forgot-email-error' : undefined}
                  disabled={submitting}
                />
                {emailError && <span id="forgot-email-error" className="auth-field-error">{emailError}</span>}
              </label>

              <button type="submit" className="auth-submit" disabled={submitting}>
                {submitting ? 'Sending…' : 'Send reset link'}
              </button>
            </form>

            <nav className="auth-links">
              <Link to="/sign-in">Back to sign in</Link>
            </nav>
          </>
        )}
      </div>
    </main>
  )
}
