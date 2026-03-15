import { useEffect, useState } from 'react'
import { Link, useSearchParams } from 'react-router-dom'
import './auth.css'

type Status = 'loading' | 'success' | 'error'

export default function ConfirmEmailPage() {
  const [searchParams] = useSearchParams()
  const [status, setStatus] = useState<Status>('loading')

  const userId = searchParams.get('userId') ?? ''
  const token = searchParams.get('token') ?? ''

  useEffect(() => {
    if (!userId || !token) {
      setStatus('error')
      return
    }

    fetch('/api/auth/confirm-email', {
      method: 'POST',
      credentials: 'include',
      headers: { 'Content-Type': 'application/json' },
      body: JSON.stringify({ userId, token }),
    })
      .then(r => setStatus(r.ok || r.status === 204 ? 'success' : 'error'))
      .catch(() => setStatus('error'))
  }, [userId, token])

  return (
    <main className="auth-page">
      <div className="auth-card">
        <Link to="/" className="auth-logo">MEEPLITON</Link>
        <h1 className="auth-title">Email Confirmation</h1>

        {status === 'loading' && (
          <p className="auth-info">Confirming your email…</p>
        )}

        {status === 'success' && (
          <>
            <p className="auth-success" role="status">
              Your email has been confirmed. You can now sign in.
            </p>
            <nav className="auth-links">
              <Link to="/sign-in">Sign in</Link>
            </nav>
          </>
        )}

        {status === 'error' && (
          <>
            <p className="auth-error" role="alert">
              This confirmation link is invalid or has expired.
            </p>
            <nav className="auth-links">
              <Link to="/sign-in">Back to sign in</Link>
            </nav>
          </>
        )}
      </div>
    </main>
  )
}
