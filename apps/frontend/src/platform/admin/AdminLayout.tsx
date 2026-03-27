import { ReactNode } from 'react'
import { Link, NavLink, NavLinkRenderProps, useNavigate } from 'react-router-dom'
import { AppShell, ThemeToggle } from '@meepliton/ui'
import { useAuth } from '../auth/AuthContext'

interface Props {
  children: ReactNode
}

function logoLink({ children, className }: { children: ReactNode; className: string }) {
  return (
    <Link to="/lobby" className={className}>
      {children}
    </Link>
  )
}

export default function AdminLayout({ children }: Props) {
  const { user, logout } = useAuth()
  const navigate = useNavigate()

  async function handleSignOut() {
    await logout()
    navigate('/sign-in')
  }

  return (
    <AppShell
      user={user ? { displayName: user.displayName, avatarUrl: user.avatarUrl } : null}
      onSignOut={handleSignOut}
      logoLinkAs={logoLink}
      themeToggle={<ThemeToggle />}
      onAvatarClick={() => navigate('/account')}
    >
      <div style={{ minHeight: '100vh', background: 'var(--surface-base)' }}>
        {/* Tab nav */}
        <nav
          aria-label="Admin sections"
          style={{
            background: 'var(--surface-raised)',
            borderBottom: '1px solid var(--edge-subtle)',
            padding: '0 var(--space-6)',
            display: 'flex',
            alignItems: 'center',
            gap: 'var(--space-1)',
          }}
        >
          <Link
            to="/lobby"
            style={{
              fontFamily: 'var(--font-mono)',
              fontSize: '.72rem',
              color: 'var(--text-muted)',
              textDecoration: 'none',
              padding: 'var(--space-3) var(--space-3)',
              marginRight: 'var(--space-4)',
              letterSpacing: '.5px',
              whiteSpace: 'nowrap',
            }}
          >
            &larr; Lobby
          </Link>
          {[
            { to: '/admin/users', label: 'Users' },
            { to: '/admin/rooms', label: 'Rooms' },
          ].map(tab => (
            <NavLink
              key={tab.to}
              to={tab.to}
              style={({ isActive }: NavLinkRenderProps) => ({
                fontFamily: 'var(--font-display)',
                fontSize: '.72rem',
                fontWeight: 700,
                letterSpacing: '1.5px',
                textTransform: 'uppercase' as const,
                textDecoration: 'none',
                padding: 'var(--space-3) var(--space-4)',
                color: isActive ? 'var(--accent)' : 'var(--text-muted)',
                borderBottom: isActive ? '2px solid var(--accent)' : '2px solid transparent',
                transition: 'color var(--dur-base), border-color var(--dur-base)',
                whiteSpace: 'nowrap' as const,
              })}
            >
              {tab.label}
            </NavLink>
          ))}
        </nav>

        {/* Page content */}
        <main
          style={{
            maxWidth: '1100px',
            margin: '0 auto',
            padding: 'var(--space-6)',
          }}
        >
          {children}
        </main>
      </div>
    </AppShell>
  )
}
