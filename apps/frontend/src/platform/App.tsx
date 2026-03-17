import { ReactNode } from 'react'
import { Routes, Route, Navigate, Link, useLocation, useNavigate } from 'react-router-dom'
import { AuthProvider, useAuth } from './auth/AuthContext'
import { AppShell, ThemeToggle } from '@meepliton/ui'
import SignInPage from './auth/SignInPage'
import RegisterPage from './auth/RegisterPage'
import ForgotPasswordPage from './auth/ForgotPasswordPage'
import ResetPasswordPage from './auth/ResetPasswordPage'
import ConfirmEmailPage from './auth/ConfirmEmailPage'
import LobbyPage from './lobby/LobbyPage'
import RoomPage from './room/RoomPage'
import RoomNotFoundPage from './room/RoomNotFoundPage'
import ProfilePage from './account/ProfilePage'

function logoLink({ children, className }: { children: ReactNode; className: string }) {
  return (
    <Link to="/lobby" className={className}>
      {children}
    </Link>
  )
}

function AppRoutes() {
  const { user, loading, logout } = useAuth()
  const navigate = useNavigate()
  const location = useLocation()

  if (loading) return null

  async function handleSignOut() {
    await logout()
    navigate('/sign-in')
  }

  /** Wrap a page in AppShell; redirect to /sign-in with ?next= if not authenticated. */
  function shell(page: ReactNode) {
    if (!user) {
      const next = location.pathname + location.search
      return <Navigate to={`/sign-in?next=${encodeURIComponent(next)}`} replace />
    }
    return (
      <AppShell user={user} onSignOut={handleSignOut} logoLinkAs={logoLink} themeToggle={<ThemeToggle />}>
        {page}
      </AppShell>
    )
  }

  return (
    <Routes>
      {/* Auth pages — no shell */}
      <Route path="/sign-in" element={<SignInPage />} />
      <Route path="/login" element={<Navigate to="/sign-in" replace />} />
      <Route path="/register" element={<RegisterPage />} />
      <Route path="/forgot-password" element={<ForgotPasswordPage />} />
      <Route path="/reset-password" element={<ResetPasswordPage />} />
      <Route path="/confirm-email" element={<ConfirmEmailPage />} />

      {/* Authenticated pages — wrapped in AppShell */}
      <Route path="/lobby" element={shell(<LobbyPage />)} />
      <Route path="/account" element={shell(<ProfilePage />)} />
      <Route path="/room/:roomId" element={shell(<RoomPage />)} />
      <Route path="/join/:code" element={shell(<RoomPage join />)} />
      <Route path="/room-not-found" element={shell(<RoomNotFoundPage />)} />

      {/* Fallback */}
      <Route path="*" element={<Navigate to={user ? '/lobby' : '/sign-in'} replace />} />
    </Routes>
  )
}

export default function App() {
  return (
    <AuthProvider>
      <AppRoutes />
    </AuthProvider>
  )
}
