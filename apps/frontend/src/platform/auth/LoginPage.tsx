import { useState, FormEvent } from 'react'
import { useNavigate } from 'react-router-dom'
import { ThemeToggle } from '../theme/ThemeToggle'

export default function LoginPage() {
  const navigate = useNavigate()
  const [email, setEmail] = useState('')
  const [password, setPassword] = useState('')
  const [error, setError] = useState<string | null>(null)

  async function handleSubmit(e: FormEvent) {
    e.preventDefault()
    setError(null)
    const res = await fetch('/api/auth/login', {
      method: 'POST',
      credentials: 'include',
      headers: { 'Content-Type': 'application/json' },
      body: JSON.stringify({ email, password }),
    })
    if (res.ok) {
      navigate('/lobby')
    } else {
      setError('Invalid email or password.')
    }
  }

  return (
    <main className="auth-page">
      <div style={{ display: 'flex', justifyContent: 'flex-end', padding: 'var(--space-3)' }}>
        <ThemeToggle />
      </div>
      <h1>Meepliton</h1>
      <form onSubmit={handleSubmit} className="auth-form">
        <label>
          Email
          <input type="email" value={email} onChange={e => setEmail(e.target.value)} required />
        </label>
        <label>
          Password
          <input type="password" value={password} onChange={e => setPassword(e.target.value)} required />
        </label>
        {error && <p className="auth-error">{error}</p>}
        <button type="submit">Sign in</button>
      </form>
      <a href="/api/auth/google">Sign in with Google</a>
    </main>
  )
}
