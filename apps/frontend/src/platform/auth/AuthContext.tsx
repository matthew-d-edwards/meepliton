import { createContext, useContext, useEffect, useState, ReactNode } from 'react'

export interface User {
  id: string
  displayName: string
  avatarUrl: string | null
  email: string
  theme: 'light' | 'dark' | 'system'
  isAdmin?: boolean
}

interface AuthContextValue {
  user: User | null
  loading: boolean
  login: (email: string, password: string) => Promise<void>
  logout: () => Promise<void>
}

const AuthContext = createContext<AuthContextValue | null>(null)

async function fetchMe(): Promise<User | null> {
  try {
    const r = await fetch('/api/auth/me', { credentials: 'include' })
    if (r.ok) return (await r.json()) as User
    return null
  } catch {
    return null
  }
}

export function AuthProvider({ children }: { children: ReactNode }) {
  const [user, setUser] = useState<User | null>(null)
  const [loading, setLoading] = useState(true)

  useEffect(() => {
    fetchMe()
      .then(data => setUser(data))
      .finally(() => setLoading(false))
  }, [])

  async function login(email: string, password: string): Promise<void> {
    const res = await fetch('/api/auth/login', {
      method: 'POST',
      credentials: 'include',
      headers: { 'Content-Type': 'application/json' },
      body: JSON.stringify({ email, password }),
    })

    if (!res.ok) {
      let message = 'Incorrect email or password.'
      try {
        const body = (await res.json()) as { code?: string; retryAfter?: string }
        if (res.status === 403 && body.code === 'unconfirmed') {
          message = '__unconfirmed__'
        } else if (res.status === 429) {
          const retryAt = body.retryAfter ?? ''
          message = `__lockout__:${retryAt}`
        }
      } catch {
        // use default message
      }
      throw new Error(message)
    }

    const me = await fetchMe()
    setUser(me)
  }

  async function logout() {
    await fetch('/api/auth/logout', { method: 'POST', credentials: 'include' })
    setUser(null)
  }

  return (
    <AuthContext.Provider value={{ user, loading, login, logout }}>
      {children}
    </AuthContext.Provider>
  )
}

export function useAuth() {
  const ctx = useContext(AuthContext)
  if (!ctx) throw new Error('useAuth must be used within AuthProvider')
  return ctx
}
