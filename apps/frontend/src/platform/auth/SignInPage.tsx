import { useState, FormEvent } from 'react'
import { Link, useNavigate, useSearchParams, Navigate } from 'react-router-dom'
import { useAuth } from './AuthContext'
import './auth.css'

function parseLoginError(message: string): { type: 'unconfirmed' | 'lockout' | 'generic'; lockoutTime?: string } {
  if (message === '__unconfirmed__') return { type: 'unconfirmed' }
  if (message.startsWith('__lockout__:')) {
    return { type: 'lockout', lockoutTime: message.slice('__lockout__:'.length) }
  }
  return { type: 'generic' }
}

export default function SignInPage() {
  const { user, loading, login } = useAuth()
  const navigate = useNavigate()
  const [searchParams] = useSearchParams()
  const next = searchParams.get('next') ?? '/lobby'

  const [email, setEmail] = useState('')
  const [password, setPassword] = useState('')
  const [submitting, setSubmitting] = useState(false)
  const [error, setError] = useState<string | null>(null)
  const [errorType, setErrorType] = useState<'unconfirmed' | 'lockout' | 'generic' | null>(null)
  const [resendSent, setResendSent] = useState(false)

  // Validation errors
  const [emailError, setEmailError] = useState<string | null>(null)
  const [passwordError, setPasswordError] = useState<string | null>(null)

  if (loading) return null

  if (user) return <Navigate to="/lobby" replace />

  function validate(): boolean {
    let valid = true
    setEmailError(null)
    setPasswordError(null)

    if (!email.trim()) {
      setEmailError('Email is required.')
      valid = false
    }
    if (!password) {
      setPasswordError('Password is required.')
      valid = false
    }
    return valid
  }

  async function handleSubmit(e: FormEvent) {
    e.preventDefault()
    if (!validate()) return

    setSubmitting(true)
    setError(null)
    setErrorType(null)
    setResendSent(false)

    try {
      await login(email, password)
      navigate(next, { replace: true })
    } catch (err) {
      const msg = err instanceof Error ? err.message : 'Incorrect email or password.'
      const parsed = parseLoginError(msg)
      setErrorType(parsed.type)
      if (parsed.type === 'lockout') {
        setError(
          parsed.lockoutTime
            ? `Too many attempts. Try again at ${parsed.lockoutTime}.`
            : 'Too many attempts. Please try again later.',
        )
      } else if (parsed.type === 'unconfirmed') {
        setError('Please confirm your email address before signing in.')
      } else {
        setError('Incorrect email or password.')
      }
    } finally {
      setSubmitting(false)
    }
  }

  async function handleResendConfirmation() {
    if (!email.trim()) return
    setResendSent(false)
    await fetch('/api/auth/resend-confirmation', {
      method: 'POST',
      credentials: 'include',
      headers: { 'Content-Type': 'application/json' },
      body: JSON.stringify({ email }),
    })
    setResendSent(true)
  }

  async function handleDevLogin() {
    setSubmitting(true)
    setError(null)
    setErrorType(null)
    try {
      await login('dev@meepliton.local', 'DevPass1')
      navigate(next, { replace: true })
    } catch {
      setError('Dev login failed — make sure the API is running.')
      setErrorType('generic')
    } finally {
      setSubmitting(false)
    }
  }

  return (
    <main className="auth-page">
      <div className="auth-card">
        <Link to="/" className="auth-logo">MEEPLITON</Link>
        <h1 className="auth-title">Sign In</h1>

        {import.meta.env.DEV && (
          <button
            type="button"
            onClick={handleDevLogin}
            disabled={submitting}
            className="auth-submit"
            style={{ marginBottom: '1rem', background: 'var(--color-surface-alt, #2a2a2a)', border: '1px dashed var(--accent)' }}
          >
            {submitting ? 'Signing in…' : 'Dev login (dev@meepliton.local)'}
          </button>
        )}

        <form onSubmit={handleSubmit} className="auth-form" noValidate>
          <label>
            Email
            <input
              type="email"
              value={email}
              onChange={e => { setEmail(e.target.value); setEmailError(null) }}
              autoComplete="email"
              aria-invalid={emailError ? 'true' : 'false'}
              aria-describedby={emailError ? 'email-error' : undefined}
              disabled={submitting}
            />
            {emailError && <span id="email-error" className="auth-field-error">{emailError}</span>}
          </label>

          <label>
            Password
            <input
              type="password"
              value={password}
              onChange={e => { setPassword(e.target.value); setPasswordError(null) }}
              autoComplete="current-password"
              aria-invalid={passwordError ? 'true' : 'false'}
              aria-describedby={passwordError ? 'password-error' : undefined}
              disabled={submitting}
            />
            {passwordError && <span id="password-error" className="auth-field-error">{passwordError}</span>}
          </label>

          {error && (
            <p className="auth-error" role="alert">
              {error}
              {errorType === 'unconfirmed' && !resendSent && (
                <>
                  {' '}
                  <button
                    type="button"
                    onClick={handleResendConfirmation}
                    style={{ background: 'none', border: 'none', color: 'var(--accent)', cursor: 'pointer', fontFamily: 'var(--font-mono)', fontSize: 'inherit', textDecoration: 'underline', padding: 0 }}
                  >
                    Resend confirmation email
                  </button>
                </>
              )}
              {errorType === 'unconfirmed' && resendSent && ' Confirmation email sent.'}
            </p>
          )}

          <button type="submit" className="auth-submit" disabled={submitting}>
            {submitting ? 'Signing in…' : 'Sign in'}
          </button>
        </form>

        <div className="auth-divider">or</div>

        <a href="/api/auth/google" className="auth-google-btn">
          Sign in with Google
        </a>

        <nav className="auth-links">
          <Link to="/forgot-password">Forgot password?</Link>
          <span>No account? <Link to="/register">Create one</Link></span>
        </nav>
      </div>
    </main>
  )
}
