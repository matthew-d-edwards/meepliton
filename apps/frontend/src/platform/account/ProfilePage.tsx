import { useEffect, useRef, useState } from 'react'
import { Link, useNavigate, useSearchParams } from 'react-router-dom'
import './account.css'

interface ProfileData {
  id: string
  displayName: string
  email: string
  avatarUrl: string | null
  theme: 'light' | 'dark' | 'system'
}

interface SavedValues {
  displayName: string
  avatarUrl: string
}

interface LoginMethodsData {
  loginMethods: string[]
}

type LoadState = 'loading' | 'error' | 'ready'
type LoginMethodsLoadState = 'loading' | 'error' | 'ready'

export default function ProfilePage() {
  const navigate = useNavigate()
  const [searchParams] = useSearchParams()

  const [loadState, setLoadState] = useState<LoadState>('loading')
  const [loadError, setLoadError] = useState<string | null>(null)

  const [saved, setSaved] = useState<SavedValues>({ displayName: '', avatarUrl: '' })
  const [displayName, setDisplayName] = useState('')
  const [avatarUrl, setAvatarUrl] = useState('')

  const [saving, setSaving] = useState(false)
  const [saveError, setSaveError] = useState<string | null>(null)
  const [saveSuccess, setSaveSuccess] = useState(false)

  // Sign-in methods state
  const [loginMethodsState, setLoginMethodsState] = useState<LoginMethodsLoadState>('loading')
  const [loginMethods, setLoginMethods] = useState<string[]>([])

  // Add-password form state
  const [newPassword, setNewPassword] = useState('')
  const [confirmPassword, setConfirmPassword] = useState('')
  const [addPasswordSaving, setAddPasswordSaving] = useState(false)
  const [addPasswordError, setAddPasswordError] = useState<string | null>(null)
  // addPasswordSuccess is kept outside the !hasPassword conditional so the
  // screen reader live-region announcement survives when the form collapses.
  const [addPasswordSuccess, setAddPasswordSuccess] = useState(false)
  // Ref to the add-password success message so focus can be moved there after
  // the form collapses — otherwise keyboard focus is lost into the void.
  const addPasswordSuccessRef = useRef<HTMLParagraphElement>(null)

  // URL-driven banners
  const linkedParam = searchParams.get('linked')
  const errorParam = searchParams.get('error')
  const urlBannerRef = useRef<HTMLParagraphElement>(null)

  // Move focus to the URL-driven banner on mount so screen readers announce it.
  // Live-region announcements for content present at initial paint are unreliable.
  useEffect(() => {
    if ((linkedParam !== null || errorParam !== null) && urlBannerRef.current) {
      urlBannerRef.current.focus()
    }
  }, [linkedParam, errorParam])

  // When the add-password form succeeds it collapses (hasPassword becomes true).
  // Move focus to the success message so keyboard and screen-reader users are
  // not left with focus lost in the void. WCAG 2.4.3 / 3.2.2.
  useEffect(() => {
    if (addPasswordSuccess && addPasswordSuccessRef.current) {
      addPasswordSuccessRef.current.focus()
    }
  }, [addPasswordSuccess])

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

  function loadLoginMethods() {
    setLoginMethodsState('loading')
    fetch('/api/auth/me/login-methods', { credentials: 'include' })
      .then(async r => {
        if (r.status === 401) {
          navigate('/sign-in', { replace: true })
          return
        }
        if (!r.ok) {
          setLoginMethodsState('error')
          return
        }
        const data = (await r.json()) as LoginMethodsData
        setLoginMethods(data.loginMethods)
        setLoginMethodsState('ready')
      })
      .catch(() => {
        setLoginMethodsState('error')
      })
  }

  useEffect(() => {
    load()
    loadLoginMethods()
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
        let message = `Save failed (${res.status}) — try again.`
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

  async function handleAddPassword(e: React.FormEvent) {
    e.preventDefault()
    setAddPasswordError(null)
    setAddPasswordSuccess(false)

    if (newPassword !== confirmPassword) {
      setAddPasswordError('Passwords do not match.')
      return
    }

    setAddPasswordSaving(true)
    try {
      const res = await fetch('/api/auth/add-password', {
        method: 'POST',
        credentials: 'include',
        headers: { 'Content-Type': 'application/json' },
        body: JSON.stringify({ newPassword }),
      })

      if (res.status === 204) {
        setAddPasswordSuccess(true)
        setNewPassword('')
        setConfirmPassword('')
        setLoginMethods(prev => [...prev, 'password'])
      } else {
        let message = `Failed to add password (${res.status}) — try again.`
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
        setAddPasswordError(message)
      }
    } catch {
      setAddPasswordError('Network error — password not added.')
    } finally {
      setAddPasswordSaving(false)
    }
  }

  const avatarPreview = avatarUrl.trim() !== '' ? avatarUrl.trim() : null
  const initials = saved.displayName
    ? saved.displayName.trim().slice(0, 2).toUpperCase()
    : '?'

  const isLoading = loadState === 'loading'
  const hasChanges = displayName !== saved.displayName || avatarUrl.trim() !== saved.avatarUrl

  const hasGoogle = loginMethods.includes('google')
  const hasPassword = loginMethods.includes('password')

  return (
    <div className="account-page">
      <main className="account-content">
        {/* Visually-hidden h1 gives the page a top-level heading for screen readers.
            WCAG 1.3.1 / 2.4.6 — heading hierarchy must not skip from no h1 to h2. */}
        <h1 className="account-page-title-sr">Account settings</h1>
        <nav aria-label="Page navigation">
          <Link to="/lobby" className="account-back">
            <span aria-hidden="true">&#8592;</span> Back to lobby
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
                    aria-describedby={
                      saveError
                        ? 'account-display-name-count profile-save-error'
                        : 'account-display-name-count'
                    }
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
                  <p id="profile-save-error" className="account-error" role="alert">{saveError}</p>
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

        <section aria-label="Sign-in methods">
          <h2 className="account-section-title">Sign-in methods</h2>

          {/* URL-driven banners — focus is moved here on mount (see useEffect above)
              because live regions on content present at initial paint are unreliable. */}
          {linkedParam === 'google' && (
            <p
              ref={urlBannerRef}
              className="account-success account-signin-banner"
              role="status"
              tabIndex={-1}
            >
              Google account linked.
            </p>
          )}
          {errorParam === 'google_already_linked' && (
            <p
              ref={urlBannerRef}
              className="account-error account-signin-banner"
              role="alert"
              tabIndex={-1}
            >
              That Google account is already linked to another user.
            </p>
          )}
          {errorParam !== null && errorParam !== 'google_already_linked' && (
            <p
              ref={urlBannerRef}
              className="account-error account-signin-banner"
              role="alert"
              tabIndex={-1}
            >
              Something went wrong — try again.
            </p>
          )}

          {loginMethodsState === 'loading' && (
            <div className="account-loading" role="status" aria-live="polite">
              <span className="account-spinner" aria-hidden="true" />
              Loading sign-in methods…
            </div>
          )}

          {loginMethodsState === 'error' && (
            <div className="account-card">
              <p className="account-error" role="alert">
                Failed to load sign-in methods. Try refreshing.
              </p>
              <div className="account-form-actions">
                <button className="btn btn-secondary" onClick={loadLoginMethods}>
                  Retry
                </button>
              </div>
            </div>
          )}

          {loginMethodsState === 'ready' && (
            <div className="account-card">
              {/* Current linked methods */}
              <div className="account-signin-methods">
                <p className="account-label account-signin-methods-label">Linked methods</p>
                <ul className="account-signin-method-list" aria-label="Currently linked sign-in methods">
                  {loginMethods.length === 0 && (
                    <li className="account-signin-method-item account-signin-method-none">
                      No sign-in methods found — try refreshing.
                    </li>
                  )}
                  {hasPassword && (
                    <li className="account-signin-method-item">
                      <span className="account-signin-method-icon" aria-hidden="true">&#128274;</span>
                      Email &amp; password
                    </li>
                  )}
                  {hasGoogle && (
                    <li className="account-signin-method-item">
                      <span className="account-signin-method-icon" aria-hidden="true">G</span>
                      Google
                    </li>
                  )}
                </ul>
              </div>

              {/* Link Google — only when not already linked */}
              {!hasGoogle && (
                <div className="account-signin-action">
                  <p className="account-signin-action-desc">
                    Link your Google account to sign in with Google.
                  </p>
                  <a
                    href="/api/auth/link-google"
                    className="btn btn-secondary"
                    aria-label="Link Google account"
                  >
                    Link Google account
                  </a>
                </div>
              )}

              {/* Add-password success — rendered outside the !hasPassword gate so the
                  screen reader live-region announcement is not cut off when the form
                  collapses after a successful submission. WCAG 4.1.3.
                  tabIndex={-1} + ref allows programmatic focus after the form collapses
                  so keyboard users are not left with lost focus. WCAG 2.4.3. */}
              {addPasswordSuccess && (
                <p
                  id="add-password-success"
                  ref={addPasswordSuccessRef}
                  className="account-success account-signin-banner"
                  role="status"
                  tabIndex={-1}
                >
                  Password added. You can now sign in with your email and password.
                </p>
              )}

              {/* Add password — only when not already set */}
              {!hasPassword && (
                <div className="account-signin-action">
                  <p className="account-signin-action-desc">
                    Add a password so you can sign in with your email address.
                  </p>
                  <form
                    className="account-form"
                    onSubmit={handleAddPassword}
                    noValidate
                    aria-label="Add password"
                  >
                    <div className="account-field">
                      <label htmlFor="account-new-password" className="account-label">
                        New password
                      </label>
                      <input
                        id="account-new-password"
                        className="account-input"
                        type="password"
                        value={newPassword}
                        onChange={e => {
                          setNewPassword(e.target.value)
                          setAddPasswordError(null)
                          setAddPasswordSuccess(false)
                        }}
                        disabled={addPasswordSaving}
                        autoComplete="new-password"
                        aria-describedby={addPasswordError ? 'add-password-error' : undefined}
                        required
                      />
                    </div>
                    <div className="account-field">
                      <label htmlFor="account-confirm-password" className="account-label">
                        Confirm password
                      </label>
                      <input
                        id="account-confirm-password"
                        className="account-input"
                        type="password"
                        value={confirmPassword}
                        onChange={e => {
                          setConfirmPassword(e.target.value)
                          setAddPasswordError(null)
                          setAddPasswordSuccess(false)
                        }}
                        disabled={addPasswordSaving}
                        autoComplete="new-password"
                        aria-describedby={addPasswordError ? 'add-password-error' : undefined}
                        required
                      />
                    </div>

                    {addPasswordError && (
                      <p id="add-password-error" className="account-error" role="alert">{addPasswordError}</p>
                    )}

                    <div className="account-form-actions">
                      <button
                        type="submit"
                        className="btn btn-primary"
                        disabled={addPasswordSaving || newPassword.length === 0 || confirmPassword.length === 0}
                      >
                        {addPasswordSaving ? 'Adding\u2026' : 'Add password'}
                      </button>
                    </div>
                  </form>
                </div>
              )}
            </div>
          )}
        </section>
      </main>
    </div>
  )
}
