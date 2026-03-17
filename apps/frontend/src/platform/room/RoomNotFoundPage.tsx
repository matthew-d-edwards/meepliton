import { useSearchParams, Link } from 'react-router-dom'

/**
 * RoomNotFoundPage — shown when a join code is invalid or the room no longer exists.
 *
 * Rendered inside AppShell (via the router), so the header chrome is already present.
 * Uses global design token classes — no CSS Modules needed for a platform chrome page.
 */
export default function RoomNotFoundPage() {
  const [params] = useSearchParams()
  const code = params.get('code')

  return (
    <main
      className="container"
      style={{
        paddingTop: 'var(--space-12)',
        paddingBottom: 'var(--space-12)',
        display: 'flex',
        flexDirection: 'column',
        alignItems: 'center',
        gap: 'var(--space-6)',
        textAlign: 'center',
      }}
    >
      {/* Icon / glyph */}
      <div
        aria-hidden="true"
        style={{
          fontFamily: 'var(--font-display)',
          fontSize: '3rem',
          fontWeight: 900,
          color: 'var(--status-error)',
          textShadow:
            'var(--glow-sm) var(--status-error), var(--glow-md) color-mix(in srgb, var(--status-error) 30%, transparent)',
          letterSpacing: '4px',
        }}
      >
        404
      </div>

      <h1
        style={{
          fontFamily: 'var(--font-display)',
          fontWeight: 700,
          fontSize: 'clamp(1.25rem, 4vw, 1.75rem)',
          color: 'var(--text-bright)',
          letterSpacing: '2px',
          textTransform: 'uppercase',
        }}
      >
        Room not found
      </h1>

      <p
        style={{
          fontFamily: 'var(--font-body)',
          color: 'var(--text-primary)',
          fontSize: '1rem',
          maxWidth: '420px',
          lineHeight: 1.6,
        }}
      >
        {code ? (
          <>
            The code{' '}
            <span
              style={{
                fontFamily: 'var(--font-mono)',
                color: 'var(--accent)',
                textShadow: 'var(--glow-sm) var(--accent-glow)',
                letterSpacing: '3px',
              }}
            >
              {code}
            </span>{' '}
            is invalid or the room no longer exists.
          </>
        ) : (
          'This join link is invalid or the room no longer exists.'
        )}
      </p>

      <Link
        to="/lobby"
        className="btn btn-secondary"
      >
        Back to lobby
      </Link>
    </main>
  )
}
