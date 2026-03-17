import { useEffect, useState } from 'react'
import { Link, useNavigate } from 'react-router-dom'
import './account.css'

interface ProfileData {
  id: string
  displayName: string
  avatarUrl: string | null
  theme: 'light' | 'dark' | 'system'
}

interface SavedValues {
  displayName: string
  avatarUrl: string
}

type LoadState = 'loading' | 'error' | 'ready'

export default function ProfilePage() {
  const navigate = useNavigate()

  const [loadState, setLoadState] = useState<LoadState>('loading')
  const [loadError, setLoadError] = useState<string | null>(null)

  const [saved, setSaved] = useState<SavedValues>({ displayName: '', avatarUrl: '' })
  const [displayName, setDisplayName] = useState('')
  const [avatarUrl, setAvatarUrl] = useState('')

  const [saving, setSaving] = useState(false)
  const [saveError, setSaveError] = useState<string | null>(null)
  const [saveSuccess, setSaveSuccess] = useState(false)

  function load() {
    setLoadState('loading')
    setLoadError(null)
    fetch('/api/auth/me', { credentials: 'include' })
      .then(async r => {
        if (r.status === 401) {
          navigate('/sign-in', { replace: true })
          return
        }
        if (!r.ok) {
          setLoadError(`Failed to load profile (${r.status}). Try refreshing.`)
          setLoadState('error')
          return
        }
        const data = (await r.json()) as ProfileData
        const vals: SavedValues = {
          displayName: data.displayName,
          avatarUrl: data.avatarUrl ?? '',
        }
        setSaved(vals)
        setDisplayName(vals.displayName)
        setAvatarUrl(vals.avatarUrl)
        setLoadState('ready')
      })
      .catch(() => {
        setLoadError('Network error — check your connection and try again.')
        setLoadState('error')
      })
  }

  useEffect(() => {
    load()
    // eslint-disable-next-line react-hooks/exhaustive-deps
  }, [])

  async function handleSave(e: React.FormEvent) {
    e.preventDefault()
    setSaving(true)
    setSaveError(null)
    setSaveSuccess(false)

    const body: Record<string, string | null> = {}
    if (displayName !== saved.displayName) body.displayName = displayName
    if (avatarUrl !== saved.avatarUrl) body.avatarUrl = avatarUrl.trim() === '' ? null : avatarUrl.trim()

    try {
      const res = await fetch('/api/auth/me', {
        method: 'PUT',
        credentials: 'include',
        headers: { 'Content-Type': 'application/json' },
        body: JSON.stringify(body),
      })

      if (res.status === 204) {
        const newSaved: SavedValues = {
          displayName,
          avatarUrl: avatarUrl.trim(),
        }
        setSaved(newSaved)
        setAvatarUrl(newSaved.avatarUrl)
        setSaveSuccess(true)
      } else {
        let message = `Save failed (${res.status}).`
        try {
          const err = (await res.json()) as { message?: string; errors?: Record<string, string[]> }
          if (err.message) {
            message = err.message
          } else if (err.errors) {
            const msgs = Object.values(err.errors).flat()
            if (msgs.length > 0) message = msgs.join(' ')
          }
        } catch {
          // use default message
        }
        setSaveError(message)
        setDisplayName(saved.displayName)
        setAvatarUrl(saved.avatarUrl)
      }
    } catch {
      setSaveError('Network error — changes not saved.')
      setDisplayName(saved.displayName)
      setAvatarUrl(saved.avatarUrl)
    } finally {
      setSaving(false)
    }
  }

  const avatarPreview = avatarUrl.trim() !== '' ? avatarUrl.trim() : null
  const initials = saved.displayName
    ? saved.displayName.trim().slice(0, 2).toUpperCase()
    : '?'

  const isLoading = loadState === 'loading'
  const hasChanges = displayName !== saved.displayName || avatarUrl.trim() !== saved.avatarUrl

  return (
    <div className="account-page">
      <main className="account-content">
        <nav>
          <Link to="/lobby" className="account-back">
            &#8592; Back to lobby
          </Link>
        </nav>

        <section aria-label="Profile settings">
          <h2 className="account-section-title">Profile</h2>

          {loadState === 'loading' && (
            <div className="account-loading" role="status" aria-live="polite">
              <span className="account-spinner" aria-hidden="true" />
              Loading profile…
            </div>
          )}

          {loadState === 'error' && loadError && (
            <div className="account-card">
              <p className="account-error" role="alert">{loadError}</p>
              <div className="account-form-actions">
                <button className="btn btn-secondary" onClick={load}>
                  Retry
                </button>
              </div>
            </div>
          )}

          {loadState === 'ready' && (
            <div className="account-card">
              {/* Avatar preview */}
              <div className="account-avatar-row">
                {avatarPreview ? (
                  <img
                    className="account-avatar-img"
                    src={avatarPreview}
                    alt={`${saved.displayName} avatar`}
                    onError={e => {
                      (e.currentTarget as HTMLImageElement).style.display = 'none'
                    }}
                  />
                ) : (
                  <div className="account-avatar-placeholder" aria-hidden="true">
                    {initials}
                  </div>
                )}
                <p className="account-avatar-hint">
                  Enter an HTTPS image URL to use as your avatar.
                  Leave blank to use the default.
                </p>
              </div>

              <form className="account-form" onSubmit={handleSave} noValidate>
                {/* Display name */}
                <div className="account-field">
                  <label htmlFor="account-display-name" className="account-label">
                    Display name
                  </label>
                  <input
                    id="account-display-name"
                    className="account-input"
                    type="text"
                    value={displayName}
                    onChange={e => {
                      setDisplayName(e.target.value)
                      setSaveSuccess(false)
                    }}
                    maxLength={32}
                    disabled={saving || isLoading}
                    autoComplete="nickname"
                    aria-describedby="account-display-name-count"
                  />
                  <span
                    id="account-display-name-count"
                    className={`account-char-count${displayName.length >= 28 ? ' account-char-count-warn' : ''}`}
                  >
                    {displayName.length}/32
                  </span>
                </div>

                {/* Avatar URL */}
                <div className="account-field">
                  <label htmlFor="account-avatar-url" className="account-label">
                    Avatar URL
                  </label>
                  <input
                    id="account-avatar-url"
                    className="account-input"
                    type="url"
                    value={avatarUrl}
                    onChange={e => {
                      setAvatarUrl(e.target.value)
                      setSaveSuccess(false)
                    }}
                    placeholder="https://example.com/avatar.png"
                    disabled={saving || isLoading}
                    autoComplete="off"
                  />
                </div>

                {saveError && (
                  <p className="account-error" role="alert">{saveError}</p>
                )}

                {saveSuccess && (
                  <p className="account-success" role="status">Profile saved.</p>
                )}

                <div className="account-form-actions">
                  <button
                    type="submit"
                    className="btn btn-primary"
                    disabled={saving || !hasChanges || displayName.trim().length === 0}
                  >
                    {saving ? 'Saving\u2026' : 'Save'}
                  </button>
                </div>
              </form>
            </div>
          )}
        </section>
      </main>
    </div>
  )
}
