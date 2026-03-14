import { ReactNode } from 'react'
import { Routes, Route, Navigate, Link, useNavigate } from 'react-router-dom'
import { AuthProvider, useAuth } from './auth/AuthContext'
import LoginPage from './auth/LoginPage'
import LobbyPage from './lobby/LobbyPage'
import RoomPage from './room/RoomPage'
import { AppShell } from '@meepliton/ui'

function logoLink({ children, className }: { children: ReactNode; className: string }) {
  return (
    <Link to="/lobby" className={className}>
      {children}
    </Link>
  )
}

function AppRoutes() {
  const { user, logout } = useAuth()
  const navigate = useNavigate()

  async function handleSignOut() {
    await logout()
    navigate('/login')
  }

  return (
    <AppShell user={user} onSignOut={handleSignOut} logoLinkAs={logoLink}>
      <Routes>
        <Route path="/login" element={<LoginPage />} />
        <Route path="/lobby" element={user ? <LobbyPage /> : <Navigate to="/login" />} />
        <Route path="/room/:roomId" element={user ? <RoomPage /> : <Navigate to="/login" />} />
        <Route path="/join/:code" element={user ? <RoomPage join /> : <Navigate to="/login" />} />
        <Route path="*" element={<Navigate to={user ? '/lobby' : '/login'} />} />
      </Routes>
    </AppShell>
  )
}

export default function App() {
  return (
    <AuthProvider>
      <AppRoutes />
    </AuthProvider>
  )
}
