import { ReactNode, ElementType } from 'react'

export interface AppShellUser {
  displayName: string
}

export interface AppShellProps {
  children: ReactNode
  /** Authenticated user — if null the sign-out button is hidden */
  user: AppShellUser | null
  /** Called when the user clicks the sign-out button */
  onSignOut: () => void
  /**
   * Component used to wrap the logo text so the platform can pass a
   * react-router `Link`. Receives an `href` prop pointing to `/lobby`.
   * Defaults to a plain `<a>` tag.
   */
  logoLinkAs?: ElementType<{ href: string; className: string; 'aria-label': string }>
  /**
   * Optional theme toggle element rendered in the header between the logo
   * and the sign-out button. Story-011 fills this slot.
   */
  themeToggle?: ReactNode
}

export function AppShell({
  children,
  user,
  onSignOut,
  logoLinkAs,
  themeToggle,
}: AppShellProps) {
  const LogoLink = logoLinkAs ?? 'a'

  return (
    <>
      <header className="meepliton-header" role="banner">
        <div className="container meepliton-header-inner">
          <LogoLink
            href="/lobby"
            className="meepliton-logo"
            aria-label="Meepliton — go to lobby"
          >
            MEEPL<em>I</em>TON
          </LogoLink>

          <div className="meepliton-header-actions">
            {themeToggle}

            {user !== null && (
              <button
                type="button"
                className="meepliton-signout-btn icon-btn"
                onClick={onSignOut}
                aria-label="Sign out"
                title="Sign out"
              >
                {/* Power / exit icon — inline SVG keeps it zero-dependency */}
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
                >
                  <path d="M9 21H5a2 2 0 0 1-2-2V5a2 2 0 0 1 2-2h4" />
                  <polyline points="16 17 21 12 16 7" />
                  <line x1="21" y1="12" x2="9" y2="12" />
                </svg>
                <span className="meepliton-signout-label">Sign out</span>
              </button>
            )}
          </div>
        </div>
      </header>

      {children}
    </>
  )
}
