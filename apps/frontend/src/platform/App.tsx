import { Routes, Route, Navigate, Link, useNavigate, useParams } from 'react-router-dom'
import { AuthProvider, useAuth } from './auth/AuthContext'
import { AppShell } from '@meepliton/ui'
import LoginPage from './auth/LoginPage'
import LobbyPage from './lobby/LobbyPage'
import RoomPage from './room/RoomPage'

function JoinRoute() {
  const { code } = useParams<{ code: string }>()
  const { user } = useAuth()
  if (user) return <RoomPage join />
  return <Navigate to={`/login?next=/join/${code ?? ''}`} replace />
}
        
/** react-router Link adapter for AppShell's logoLinkAs prop */
function RouterLink({
  href,
  className,
  'aria-label': ariaLabel,
  children,
}: {
  href: string
  className: string
  'aria-label': string
  children?: React.ReactNode
}) {
  return (
    <Link to={href} className={className} aria-label={ariaLabel}>
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
    <AppShell
      user={user}
      onSignOut={handleSignOut}
      logoLinkAs={user ? RouterLink : undefined}
    >
      <Routes>
        <Route path="/login" element={<LoginPage />} />
        <Route path="/lobby" element={user ? <LobbyPage /> : <Navigate to="/login" />} />
        <Route path="/room/:roomId" element={user ? <RoomPage /> : <Navigate to="/login" />} />
        <Route path="/join/:code" element={user ? <JoinRoute /> : <Navigate to="/login" />} />
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
