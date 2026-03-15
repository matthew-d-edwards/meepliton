import { ReactNode } from 'react'
import { Routes, Route, Navigate, Link, useNavigate } from 'react-router-dom'
import { AuthProvider, useAuth } from './auth/AuthContext'
import { AppShell } from '@meepliton/ui'
import SignInPage from './auth/SignInPage'
import RegisterPage from './auth/RegisterPage'
import ForgotPasswordPage from './auth/ForgotPasswordPage'
import ResetPasswordPage from './auth/ResetPasswordPage'
import LobbyPage from './lobby/LobbyPage'
import RoomPage from './room/RoomPage'

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

  if (loading) return null

  async function handleSignOut() {
    await logout()
    navigate('/sign-in')
  }

  function shell(page: ReactNode) {
    return user ? (
      <AppShell user={user} onSignOut={handleSignOut} logoLinkAs={logoLink}>
        {page}
      </AppShell>
    ) : (
      <Navigate to="/sign-in" replace />
    )
  }

  return (
    <Routes>
      <Route path="/sign-in" element={<SignInPage />} />
      <Route path="/register" element={<RegisterPage />} />
      <Route path="/forgot-password" element={<ForgotPasswordPage />} />
      <Route path="/reset-password" element={<ResetPasswordPage />} />
      <Route path="/lobby" element={shell(<LobbyPage />)} />
      <Route path="/room/:roomId" element={shell(<RoomPage />)} />
      <Route path="/join/:code" element={shell(<RoomPage join />)} />
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
