import { Routes, Route, Navigate } from 'react-router-dom'
import { AuthProvider, useAuth } from './auth/AuthContext'
import LoginPage from './auth/LoginPage'
import LobbyPage from './lobby/LobbyPage'
import RoomPage from './room/RoomPage'
import './auth/auth.css'

function AppRoutes() {
  const { user, loading } = useAuth()

  if (loading) {
    return (
      <div className="auth-loading-gate" aria-live="polite" aria-label="Loading">
        <div className="auth-loading-spinner" />
      </div>
    )
  }

  return (
    <Routes>
      <Route path="/login" element={<LoginPage />} />
      <Route path="/lobby" element={user ? <LobbyPage /> : <Navigate to="/login" />} />
      <Route path="/room/:roomId" element={user ? <RoomPage /> : <Navigate to="/login" />} />
      <Route path="/join/:code" element={user ? <RoomPage join /> : <Navigate to="/login" />} />
      <Route path="*" element={<Navigate to={user ? '/lobby' : '/login'} />} />
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
