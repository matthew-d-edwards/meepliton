import { ReactNode } from 'react'
import { Avatar } from './Avatar'

interface AppShellUser {
  displayName: string
  avatarUrl?: string | null
}

interface AppShellProps {
  /** The page content to render below the header */
  children: ReactNode
  /** Currently authenticated user — renders sign-out button when present */
  user: AppShellUser | null
  /**
   * Called when the user activates the sign-out button.
   * The caller is responsible for signing out and redirecting.
   */
  onSignOut: () => void
  /**
   * Slot for the logo link wrapper.
   * Provide a component that wraps its children in a navigation link.
   * If omitted, the logo renders as a plain <span>.
   *
   * Example:
   *   logoLinkAs={({ children, className }) => (
   *     <Link to="/lobby" className={className}>{children}</Link>
   *   )}
   */
  logoLinkAs?: (props: { children: ReactNode; className: string }) => ReactNode
  /**
   * Slot for the theme toggle button.
   * Story-011 will pass a wired <ThemeToggle /> here.
   * Until then, omit this prop and the slot is simply absent.
   */
  themeToggle?: ReactNode
  /**
   * Called when the user activates their avatar in the header.
   * The caller is responsible for navigating (e.g. to /account).
   * If omitted, the avatar renders without a click target.
   */
  onAvatarClick?: () => void
}

/**
 * AppShell — sticky platform header wrapping every page.
 *
 * Renders:
 *  - Meepliton logo (links to /lobby when logoLinkAs is provided)
 *  - Theme toggle slot (story-011 will fill this)
 *  - Sign-out icon button (when user is authenticated)
 *
 * Uses global class names from tokens.css — no CSS Modules needed here
 * because this is a platform chrome component, not a game component.
 *
 * Intentionally has no router dependency — callers pass logoLinkAs and
 * handle sign-out/redirect themselves.
 */
export function AppShell({
  children,
  user,
  onSignOut,
  logoLinkAs: LogoLink,
  themeToggle,
  onAvatarClick,
}: AppShellProps) {
  const logo = LogoLink ? (
    <LogoLink className="meepliton-logo">
      MEEPL<em>ITON</em>
    </LogoLink>
  ) : (
    <span className="meepliton-logo" aria-label="Meepliton">
      MEEPL<em>ITON</em>
    </span>
  )

  return (
    <>
      <header className="meepliton-header" role="banner">
        {logo}

        <nav className="meepliton-header-actions" aria-label="Platform actions">
          {/* Theme toggle slot — story-011 wires the logic */}
          {themeToggle}

          {/* Avatar — only shown when authenticated */}
          {user && onAvatarClick && (
            <button
              type="button"
              className="icon-btn"
              onClick={onAvatarClick}
              aria-label={`Account settings (${user.displayName})`}
              title={`Account settings (${user.displayName})`}
              style={{ padding: 0, lineHeight: 0, border: 'none', background: 'none', cursor: 'pointer' }}
            >
              <Avatar url={user.avatarUrl} displayName={user.displayName} size="sm" />
            </button>
          )}
          {user && !onAvatarClick && (
            <Avatar url={user.avatarUrl} displayName={user.displayName} size="sm" />
          )}

          {/* Sign-out — only shown when authenticated */}
          {user && (
            <button
              type="button"
              className="icon-btn"
              onClick={onSignOut}
              aria-label={`Sign out (${user.displayName})`}
              title={`Sign out (${user.displayName})`}
            >
              <svg
                xmlns="http://www.w3.org/2000/svg"
                width="16"
                height="16"
                viewBox="0 0 24 24"
                fill="none"
                stroke="currentColor"
                strokeWidth="2"
                strokeLinecap="round"
                strokeLinejoin="round"
                aria-hidden="true"
                focusable="false"
              >
                <path d="M9 21H5a2 2 0 0 1-2-2V5a2 2 0 0 1 2-2h4" />
                <polyline points="16 17 21 12 16 7" />
                <line x1="21" y1="12" x2="9" y2="12" />
              </svg>
            </button>
          )}
        </nav>
      </header>

      {children}
    </>
  )
}
