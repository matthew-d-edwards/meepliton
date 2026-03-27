import React, { useEffect, useState, useCallback, FormEvent } from 'react'
import { useToast } from './ToastContext'
import AdminLayout from './AdminLayout'

type RoomStatus = 'Waiting' | 'InProgress' | 'Finished' | 'Closed'

interface AdminRoom {
  id: string
  joinCode: string
  gameId: string
  gameName: string
  hostId: string
  hostDisplayName: string
  status: string
  playerCount: number
  connectedCount: number
  createdAt: string
  updatedAt: string
  expiresAt: string | null
}

interface RoomsResponse {
  items: AdminRoom[]
  totalCount: number
  page: number
  pageSize: number
}

interface DeleteConfirm {
  roomId: string
  joinCode: string
  gameName: string
}

const ALL_STATUSES: RoomStatus[] = ['Waiting', 'InProgress', 'Finished', 'Closed']

function relativeTime(dateStr: string | null): string {
  if (!dateStr) return '—'
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

interface StatusBadgeProps {
  status: string
}

function StatusBadge({ status }: StatusBadgeProps) {
  const styles: Record<string, { bg: string; color: string; border: string }> = {
    Waiting: {
      bg: 'color-mix(in srgb, var(--neon-cyan) 12%, transparent)',
      color: 'var(--neon-cyan)',
      border: 'color-mix(in srgb, var(--neon-cyan) 40%, transparent)',
    },
    InProgress: {
      bg: 'color-mix(in srgb, var(--status-success) 12%, transparent)',
      color: 'var(--status-success)',
      border: 'color-mix(in srgb, var(--status-success) 40%, transparent)',
    },
    Finished: {
      bg: 'color-mix(in srgb, var(--text-muted) 12%, transparent)',
      color: 'var(--text-muted)',
      border: 'var(--edge-subtle)',
    },
    Closed: {
      bg: 'color-mix(in srgb, var(--text-muted) 8%, transparent)',
      color: 'var(--text-muted)',
      border: 'var(--edge-subtle)',
    },
  }
  const s = styles[status] ?? styles.Closed
  return (
    <span style={{
      fontFamily: 'var(--font-mono)',
      fontSize: '.56rem',
      fontWeight: 700,
      padding: '2px 8px',
      borderRadius: 'var(--radius-pill)',
      background: s.bg,
      color: s.color,
      border: `1px solid ${s.border}`,
      textTransform: 'uppercase',
      letterSpacing: '.5px',
      whiteSpace: 'nowrap',
    }}>
      {status}
    </span>
  )
}

export default function AdminRoomsPage() {
  const { showToast } = useToast()

  const [rooms, setRooms] = useState<AdminRoom[]>([])
  const [totalCount, setTotalCount] = useState(0)
  const [page, setPage] = useState(1)
  const pageSize = 25
  const [selectedStatuses, setSelectedStatuses] = useState<Set<RoomStatus>>(new Set())
  const [pendingStatuses, setPendingStatuses] = useState<Set<RoomStatus>>(new Set())
  const [loading, setLoading] = useState(false)
  const [error, setError] = useState<string | null>(null)
  const [deleteConfirm, setDeleteConfirm] = useState<DeleteConfirm | null>(null)

  const fetchRooms = useCallback(async (p: number, statuses: Set<RoomStatus>) => {
    setLoading(true)
    setError(null)
    try {
      const params = new URLSearchParams({
        page: String(p),
        pageSize: String(pageSize),
      })
      if (statuses.size > 0) {
        params.set('status', Array.from(statuses).join(','))
      }

      const res = await fetch(`/api/admin/rooms?${params.toString()}`, {
        credentials: 'include',
      })
      if (!res.ok) {
        setError('Failed to load rooms.')
        return
      }
      const data = (await res.json()) as RoomsResponse
      setRooms(data.items)
      setTotalCount(data.totalCount)
    } catch {
      setError('Failed to load rooms.')
    } finally {
      setLoading(false)
    }
  }, [])

  useEffect(() => {
    void fetchRooms(page, selectedStatuses)
  }, [page, selectedStatuses, fetchRooms])

  function handleStatusToggle(status: RoomStatus) {
    setPendingStatuses((prev: Set<RoomStatus>) => {
      const next = new Set(prev)
      if (next.has(status)) next.delete(status)
      else next.add(status)
      return next
    })
  }

  function handleFilterApply(e: FormEvent) {
    e.preventDefault()
    setPage(1)
    setSelectedStatuses(new Set(pendingStatuses))
  }

  async function handleDeleteConfirm() {
    if (!deleteConfirm) return
    const { roomId } = deleteConfirm
    setDeleteConfirm(null)
    try {
      const res = await fetch(`/api/admin/rooms/${roomId}`, {
        method: 'DELETE',
        credentials: 'include',
      })
      if (res.ok) {
        setRooms((prev: AdminRoom[]) => prev.filter((r: AdminRoom) => r.id !== roomId))
        setTotalCount((prev: number) => prev - 1)
        showToast('Room deleted.', 'success')
      } else {
        showToast('Failed to delete room.', 'error')
      }
    } catch {
      showToast('Failed to delete room.', 'error')
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
            Rooms
          </h1>
          <p style={{ fontFamily: 'var(--font-mono)', fontSize: '.74rem', color: 'var(--text-muted)', marginTop: 'var(--space-1)' }}>
            {totalCount} total
          </p>
        </div>

        {/* Status filter */}
        <form onSubmit={handleFilterApply} style={{ marginBottom: 'var(--space-5)' }}>
          <fieldset style={{ border: 'none', padding: 0 }}>
            <legend style={{
              fontFamily: 'var(--font-mono)',
              fontSize: '.68rem',
              color: 'var(--text-muted)',
              letterSpacing: '.5px',
              marginBottom: 'var(--space-2)',
            }}>
              Filter by status
            </legend>
            <div style={{ display: 'flex', gap: 'var(--space-4)', flexWrap: 'wrap', alignItems: 'center' }}>
              {ALL_STATUSES.map(s => (
                <label
                  key={s}
                  style={{
                    display: 'flex',
                    alignItems: 'center',
                    gap: 'var(--space-2)',
                    fontFamily: 'var(--font-mono)',
                    fontSize: '.72rem',
                    color: 'var(--text-primary)',
                    cursor: 'pointer',
                    userSelect: 'none',
                  }}
                >
                  <input
                    type="checkbox"
                    checked={pendingStatuses.has(s)}
                    onChange={() => handleStatusToggle(s)}
                    style={{ width: 'auto', cursor: 'pointer' }}
                  />
                  {s}
                </label>
              ))}
              <button type="submit" className="btn btn-secondary btn-sm">
                Apply
              </button>
            </div>
          </fieldset>
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
                {['Join code', 'Game', 'Status', 'Host', 'Players', 'Created', 'Actions'].map(h => (
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
              ) : rooms.length === 0 ? (
                <tr>
                  <td colSpan={7} style={{ padding: 'var(--space-8)', textAlign: 'center', color: 'var(--text-muted)' }}>
                    No rooms found.
                  </td>
                </tr>
              ) : (
                rooms.map((r: AdminRoom, i: number) => (
                  <tr
                    key={r.id}
                    style={{
                      background: i % 2 === 0 ? 'var(--surface-float)' : 'var(--surface-raised)',
                      borderBottom: '1px solid var(--edge-subtle)',
                    }}
                  >
                    {/* Join code */}
                    <td style={{ padding: 'var(--space-3) var(--space-4)', whiteSpace: 'nowrap' }}>
                      <span style={{
                        fontFamily: 'var(--font-display)',
                        fontWeight: 900,
                        fontSize: '.85rem',
                        letterSpacing: '2px',
                        color: 'var(--neon-cyan)',
                      }}>
                        {r.joinCode}
                      </span>
                    </td>

                    {/* Game */}
                    <td style={{ padding: 'var(--space-3) var(--space-4)', color: 'var(--text-bright)', whiteSpace: 'nowrap' }}>
                      {r.gameName}
                    </td>

                    {/* Status */}
                    <td style={{ padding: 'var(--space-3) var(--space-4)', whiteSpace: 'nowrap' }}>
                      <StatusBadge status={r.status} />
                    </td>

                    {/* Host */}
                    <td style={{ padding: 'var(--space-3) var(--space-4)', color: 'var(--text-primary)', whiteSpace: 'nowrap' }}>
                      {r.hostDisplayName}
                    </td>

                    {/* Players */}
                    <td style={{ padding: 'var(--space-3) var(--space-4)', color: 'var(--text-primary)', whiteSpace: 'nowrap' }}>
                      {r.playerCount} / {r.connectedCount} connected
                    </td>

                    {/* Created */}
                    <td style={{ padding: 'var(--space-3) var(--space-4)', color: 'var(--text-muted)', whiteSpace: 'nowrap' }}>
                      {relativeTime(r.createdAt)}
                    </td>

                    {/* Actions */}
                    <td style={{ padding: 'var(--space-3) var(--space-4)', whiteSpace: 'nowrap' }}>
                      <button
                        type="button"
                        className="btn btn-sm btn-danger"
                        onClick={() => setDeleteConfirm({ roomId: r.id, joinCode: r.joinCode, gameName: r.gameName })}
                      >
                        Delete
                      </button>
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
            Page {page} of {totalPages} &mdash; {totalCount} rooms
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

      {/* Delete confirmation dialog */}
      {deleteConfirm && (
        <div
          role="dialog"
          aria-modal="true"
          aria-labelledby="delete-dialog-title"
          style={{
            position: 'fixed',
            inset: 0,
            zIndex: 200,
            display: 'flex',
            alignItems: 'center',
            justifyContent: 'center',
            background: 'rgba(0, 0, 0, 0.6)',
            backdropFilter: 'blur(4px)',
            padding: 'var(--space-4)',
          }}
          onClick={(e: React.MouseEvent<HTMLDivElement>) => { if (e.target === e.currentTarget) setDeleteConfirm(null) }}
        >
          <div
            style={{
              background: 'var(--surface-overlay)',
              border: '1px solid var(--status-error)',
              borderRadius: 'var(--radius-lg)',
              padding: 'var(--space-6)',
              maxWidth: '400px',
              width: '100%',
              boxShadow: '0 0 40px rgba(0, 0, 0, 0.6)',
            }}
          >
            <h2
              id="delete-dialog-title"
              style={{
                fontFamily: 'var(--font-display)',
                fontSize: '.9rem',
                fontWeight: 700,
                letterSpacing: '2px',
                textTransform: 'uppercase',
                color: 'var(--status-error)',
                marginBottom: 'var(--space-4)',
              }}
            >
              Delete room
            </h2>
            <p style={{ fontFamily: 'var(--font-mono)', fontSize: '.78rem', color: 'var(--text-primary)', lineHeight: 1.6, marginBottom: 'var(--space-6)' }}>
              Delete room <strong style={{ color: 'var(--neon-cyan)' }}>{deleteConfirm.joinCode}</strong> ({deleteConfirm.gameName})? This cannot be undone.
            </p>
            <div style={{ display: 'flex', gap: 'var(--space-3)', justifyContent: 'flex-end' }}>
              <button
                type="button"
                className="btn btn-sm btn-secondary"
                onClick={() => setDeleteConfirm(null)}
              >
                Cancel
              </button>
              <button
                type="button"
                className="btn btn-sm btn-danger"
                onClick={() => void handleDeleteConfirm()}
              >
                Delete
              </button>
            </div>
          </div>
        </div>
      )}
    </AdminLayout>
  )
}
