import React, { useEffect, useState, useCallback, FormEvent } from 'react'
import { useAuth } from '../auth/AuthContext'
import { useToast } from './ToastContext'
import AdminLayout from './AdminLayout'

interface AdminUser {
  id: string
  displayName: string
  email: string | null
  emailConfirmed: boolean
  createdAt: string
  lastSeenAt: string | null
  loginMethods: string[]
  isLockedOut: boolean
  lockoutEnd: string | null
  isAdmin: boolean
}

interface UsersResponse {
  items: AdminUser[]
  totalCount: number
  page: number
  pageSize: number
}

function relativeTime(dateStr: string | null): string {
  if (!dateStr) return 'Never'
  const diff = Date.now() - new Date(dateStr).getTime()
  const seconds = Math.floor(diff / 1000)
  if (seconds < 60) return 'Just now'
  const minutes = Math.floor(seconds / 60)
  if (minutes < 60) return `${minutes}m ago`
  const hours = Math.floor(minutes / 60)
  if (hours < 24) return `${hours}h ago`
  const days = Math.floor(hours / 24)
  if (days < 30) return `${days}d ago`
  const months = Math.floor(days / 30)
  if (months < 12) return `${months}mo ago`
  return `${Math.floor(months / 12)}y ago`
}

function loginMethodLabel(methods: string[]): string {
  if (methods.includes('password') && methods.includes('google')) return 'password + google'
  if (methods.includes('google')) return 'google'
  if (methods.includes('password')) return 'password'
  return '—'
}

export default function AdminUsersPage() {
  const { user: currentUser } = useAuth()
  const { showToast } = useToast()

  const [users, setUsers] = useState<AdminUser[]>([])
  const [totalCount, setTotalCount] = useState(0)
  const [page, setPage] = useState(1)
  const pageSize = 25
  const [searchInput, setSearchInput] = useState('')
  const [activeSearch, setActiveSearch] = useState('')
  const [loading, setLoading] = useState(false)
  const [error, setError] = useState<string | null>(null)

  const fetchUsers = useCallback(async (p: number, search: string) => {
    setLoading(true)
    setError(null)
    try {
      const params = new URLSearchParams({
        page: String(p),
        pageSize: String(pageSize),
      })
      if (search.trim()) params.set('search', search.trim())

      const res = await fetch(`/api/admin/users?${params.toString()}`, {
        credentials: 'include',
      })
      if (!res.ok) {
        setError('Failed to load users.')
        return
      }
      const data = (await res.json()) as UsersResponse
      setUsers(data.items)
      setTotalCount(data.totalCount)
    } catch {
      setError('Failed to load users.')
    } finally {
      setLoading(false)
    }
  }, [])

  useEffect(() => {
    void fetchUsers(page, activeSearch)
  }, [page, activeSearch, fetchUsers])

  function handleSearchSubmit(e: FormEvent) {
    e.preventDefault()
    setPage(1)
    setActiveSearch(searchInput)
  }

  async function handleSendReset(userId: string) {
    try {
      const res = await fetch(`/api/admin/users/${userId}/send-password-reset`, {
        method: 'POST',
        credentials: 'include',
      })
      if (res.ok) {
        showToast('Password reset email sent.', 'success')
      } else {
        showToast('Failed to send reset email.', 'error')
      }
    } catch {
      showToast('Failed to send reset email.', 'error')
    }
  }

  async function handleUnlock(userId: string) {
    try {
      const res = await fetch(`/api/admin/users/${userId}/unlock`, {
        method: 'POST',
        credentials: 'include',
      })
      if (res.ok) {
        showToast('User unlocked.', 'success')
        setUsers((prev: AdminUser[]) =>
          prev.map((u: AdminUser) => u.id === userId ? { ...u, isLockedOut: false, lockoutEnd: null } : u)
        )
      } else {
        showToast('Failed to unlock user.', 'error')
      }
    } catch {
      showToast('Failed to unlock user.', 'error')
    }
  }

  async function handleToggleAdmin(user: AdminUser) {
    const endpoint = user.isAdmin ? 'revoke-admin' : 'grant-admin'
    try {
      const res = await fetch(`/api/admin/users/${user.id}/${endpoint}`, {
        method: 'POST',
        credentials: 'include',
      })
      if (res.ok) {
        const newIsAdmin = !user.isAdmin
        setUsers((prev: AdminUser[]) =>
          prev.map((u: AdminUser) => u.id === user.id ? { ...u, isAdmin: newIsAdmin } : u)
        )
        showToast(newIsAdmin ? 'Admin role granted.' : 'Admin role revoked.', 'success')
      } else {
        showToast('Failed to update admin role.', 'error')
      }
    } catch {
      showToast('Failed to update admin role.', 'error')
    }
  }

  const totalPages = Math.max(1, Math.ceil(totalCount / pageSize))

  return (
    <AdminLayout>
      <div>
        {/* Page heading */}
        <div style={{ marginBottom: 'var(--space-5)' }}>
          <h1 style={{
            fontFamily: 'var(--font-display)',
            fontSize: '1rem',
            fontWeight: 700,
            letterSpacing: '3px',
            textTransform: 'uppercase',
            color: 'var(--accent)',
          }}>
            Users
          </h1>
          <p style={{ fontFamily: 'var(--font-mono)', fontSize: '.74rem', color: 'var(--text-muted)', marginTop: 'var(--space-1)' }}>
            {totalCount} total
          </p>
        </div>

        {/* Search */}
        <form
          onSubmit={handleSearchSubmit}
          style={{ display: 'flex', gap: 'var(--space-3)', marginBottom: 'var(--space-5)', maxWidth: '480px' }}
        >
          <input
            type="search"
            value={searchInput}
            onChange={(e: React.ChangeEvent<HTMLInputElement>) => setSearchInput(e.target.value)}
            placeholder="Search by name or email…"
            aria-label="Search users"
            style={{ flex: 1 }}
          />
          <button type="submit" className="btn btn-secondary btn-sm" style={{ whiteSpace: 'nowrap' }}>
            Search
          </button>
        </form>

        {/* Error */}
        {error && (
          <p style={{ fontFamily: 'var(--font-mono)', fontSize: '.78rem', color: 'var(--status-error)', marginBottom: 'var(--space-4)' }}>
            {error}
          </p>
        )}

        {/* Table */}
        <div style={{ overflowX: 'auto', borderRadius: 'var(--radius-md)', border: '1px solid var(--edge-subtle)' }}>
          <table
            style={{
              width: '100%',
              borderCollapse: 'collapse',
              fontFamily: 'var(--font-mono)',
              fontSize: '.74rem',
            }}
          >
            <thead>
              <tr style={{ background: 'var(--surface-overlay)', borderBottom: '1px solid var(--edge-subtle)' }}>
                {['Display name', 'Email', 'Confirmed', 'Login', 'Last seen', 'Status', 'Actions'].map(h => (
                  <th
                    key={h}
                    style={{
                      padding: 'var(--space-3) var(--space-4)',
                      textAlign: 'left',
                      fontFamily: 'var(--font-display)',
                      fontSize: '.62rem',
                      fontWeight: 700,
                      letterSpacing: '1.5px',
                      textTransform: 'uppercase',
                      color: 'var(--text-muted)',
                      whiteSpace: 'nowrap',
                    }}
                  >
                    {h}
                  </th>
                ))}
              </tr>
            </thead>
            <tbody>
              {loading ? (
                <tr>
                  <td colSpan={7} style={{ padding: 'var(--space-8)', textAlign: 'center', color: 'var(--text-muted)' }}>
                    Loading&hellip;
                  </td>
                </tr>
              ) : users.length === 0 ? (
                <tr>
                  <td colSpan={7} style={{ padding: 'var(--space-8)', textAlign: 'center', color: 'var(--text-muted)' }}>
                    No users found.
                  </td>
                </tr>
              ) : (
                users.map((u: AdminUser, i: number) => (
                  <tr
                    key={u.id}
                    style={{
                      background: i % 2 === 0 ? 'var(--surface-float)' : 'var(--surface-raised)',
                      borderBottom: '1px solid var(--edge-subtle)',
                    }}
                  >
                    {/* Display name */}
                    <td style={{ padding: 'var(--space-3) var(--space-4)', whiteSpace: 'nowrap' }}>
                      <span style={{ color: 'var(--text-bright)' }}>{u.displayName}</span>
                      {u.isAdmin && (
                        <span style={{
                          marginLeft: 'var(--space-2)',
                          fontFamily: 'var(--font-mono)',
                          fontSize: '.56rem',
                          fontWeight: 700,
                          padding: '2px 6px',
                          borderRadius: 'var(--radius-pill)',
                          background: 'color-mix(in srgb, var(--accent) 15%, transparent)',
                          color: 'var(--accent)',
                          border: '1px solid color-mix(in srgb, var(--accent) 40%, transparent)',
                          textTransform: 'uppercase',
                          letterSpacing: '.5px',
                        }}>
                          Admin
                        </span>
                      )}
                    </td>

                    {/* Email */}
                    <td style={{ padding: 'var(--space-3) var(--space-4)', color: 'var(--text-primary)', whiteSpace: 'nowrap' }}>
                      {u.email ?? '—'}
                    </td>

                    {/* Confirmed */}
                    <td style={{ padding: 'var(--space-3) var(--space-4)', whiteSpace: 'nowrap' }}>
                      <span style={{ color: u.emailConfirmed ? 'var(--status-success)' : 'var(--text-muted)' }}>
                        {u.emailConfirmed ? 'Yes' : 'No'}
                      </span>
                    </td>

                    {/* Login methods */}
                    <td style={{ padding: 'var(--space-3) var(--space-4)', color: 'var(--text-primary)', whiteSpace: 'nowrap' }}>
                      {loginMethodLabel(u.loginMethods)}
                    </td>

                    {/* Last seen */}
                    <td style={{ padding: 'var(--space-3) var(--space-4)', color: 'var(--text-muted)', whiteSpace: 'nowrap' }}>
                      {relativeTime(u.lastSeenAt)}
                    </td>

                    {/* Lockout status */}
                    <td style={{ padding: 'var(--space-3) var(--space-4)', whiteSpace: 'nowrap' }}>
                      {u.isLockedOut ? (
                        <span style={{
                          fontFamily: 'var(--font-mono)',
                          fontSize: '.56rem',
                          fontWeight: 700,
                          padding: '2px 6px',
                          borderRadius: 'var(--radius-pill)',
                          background: 'color-mix(in srgb, var(--status-error) 15%, transparent)',
                          color: 'var(--status-error)',
                          border: '1px solid color-mix(in srgb, var(--status-error) 40%, transparent)',
                          textTransform: 'uppercase',
                          letterSpacing: '.5px',
                        }}>
                          Locked
                        </span>
                      ) : (
                        <span style={{ color: 'var(--text-muted)' }}>—</span>
                      )}
                    </td>

                    {/* Actions */}
                    <td style={{ padding: 'var(--space-3) var(--space-4)', whiteSpace: 'nowrap' }}>
                      <div style={{ display: 'flex', gap: 'var(--space-2)', flexWrap: 'wrap' }}>
                        <button
                          type="button"
                          className="btn btn-sm btn-secondary"
                          disabled={!u.loginMethods.includes('password')}
                          onClick={() => void handleSendReset(u.id)}
                          title={u.loginMethods.includes('password') ? 'Send password reset email' : 'User has no password login'}
                        >
                          Reset email
                        </button>
                        <button
                          type="button"
                          className="btn btn-sm btn-secondary"
                          disabled={!u.isLockedOut}
                          onClick={() => void handleUnlock(u.id)}
                          title={u.isLockedOut ? 'Unlock account' : 'Account is not locked'}
                        >
                          Unlock
                        </button>
                        <button
                          type="button"
                          className="btn btn-sm btn-secondary"
                          disabled={u.id === currentUser?.id}
                          onClick={() => void handleToggleAdmin(u)}
                          title={u.id === currentUser?.id ? 'Cannot modify your own admin role' : undefined}
                        >
                          {u.isAdmin ? 'Revoke admin' : 'Grant admin'}
                        </button>
                      </div>
                    </td>
                  </tr>
                ))
              )}
            </tbody>
          </table>
        </div>

        {/* Pagination */}
        <div style={{
          display: 'flex',
          alignItems: 'center',
          justifyContent: 'space-between',
          marginTop: 'var(--space-4)',
          fontFamily: 'var(--font-mono)',
          fontSize: '.74rem',
          color: 'var(--text-muted)',
          flexWrap: 'wrap',
          gap: 'var(--space-3)',
        }}>
          <span>
            Page {page} of {totalPages} &mdash; {totalCount} users
          </span>
          <div style={{ display: 'flex', gap: 'var(--space-2)' }}>
            <button
              type="button"
              className="btn btn-sm btn-secondary"
              disabled={page <= 1 || loading}
              onClick={() => setPage((p: number) => p - 1)}
            >
              Previous
            </button>
            <button
              type="button"
              className="btn btn-sm btn-secondary"
              disabled={page >= totalPages || loading}
              onClick={() => setPage((p: number) => p + 1)}
            >
              Next
            </button>
          </div>
        </div>
      </div>
    </AdminLayout>
  )
}
