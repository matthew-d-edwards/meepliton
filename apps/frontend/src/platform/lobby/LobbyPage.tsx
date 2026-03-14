import { useEffect, useState } from 'react'
import { useNavigate } from 'react-router-dom'
import { useAuth } from '../auth/AuthContext'
import { ThemeToggle } from '../theme/ThemeToggle'
import './lobby.css'

interface GameInfo {
  gameId: string
  name: string
  description: string
  minPlayers: number
  maxPlayers: number
}

interface RoomInfo {
  roomId: string
  gameId: string
  gameName: string
  status: 'waiting' | 'playing' | 'finished'
  playerCount: number
  joinCode: string
}

interface LobbyData {
  rooms: RoomInfo[]
  games: GameInfo[]
}

function StatusBadge({ status }: { status: RoomInfo['status'] }) {
  const labels: Record<RoomInfo['status'], string> = {
    waiting: 'Waiting',
    playing: 'Playing',
    finished: 'Finished',
  }
  return (
    <span className={`status-badge status-badge-${status}`}>{labels[status]}</span>
  )
}

export default function LobbyPage() {
  const navigate = useNavigate()

  const [data, setData] = useState<LobbyData | null>(null)
  const [loadingLobby, setLoadingLobby] = useState(true)

  const [joinCode, setJoinCode] = useState('')
  const [joinError, setJoinError] = useState<string | null>(null)
  const [joiningRoom, setJoiningRoom] = useState(false)

  const [creatingGameId, setCreatingGameId] = useState<string | null>(null)

  useEffect(() => {
    setLoadingLobby(true)
    fetch('/api/lobby', { credentials: 'include' })
      .then(r => r.ok ? r.json() : Promise.reject(r.status))
      .then((d: LobbyData) => setData(d))
      .catch(() => setData({ rooms: [], games: [] }))
      .finally(() => setLoadingLobby(false))
  }, [])

  async function createRoom(gameId: string) {
    if (creatingGameId !== null) return
    setCreatingGameId(gameId)
    try {
      const res = await fetch('/api/rooms', {
        method: 'POST',
        credentials: 'include',
        headers: { 'Content-Type': 'application/json' },
        body: JSON.stringify({ gameId }),
      })
      if (res.ok) {
        const room = await res.json() as { roomId: string; joinCode: string }
        navigate(`/room/${room.roomId}`)
      }
    } finally {
      setCreatingGameId(null)
    }
  }

  function handleJoinInput(e: React.ChangeEvent<HTMLInputElement>) {
    const value = e.target.value.replace(/[^A-Za-z0-9]/g, '').toUpperCase()
    setJoinCode(value)
    setJoinError(null)
  }

  async function handleJoin(e: React.FormEvent) {
    e.preventDefault()
    if (joinCode.length !== 6) {
      setJoinError('Enter a 6-character room code.')
      return
    }
    setJoinError(null)
    setJoiningRoom(true)
    try {
      const res = await fetch('/api/rooms/join', {
        method: 'POST',
        credentials: 'include',
        headers: { 'Content-Type': 'application/json' },
        body: JSON.stringify({ code: joinCode }),
      })
      if (res.ok) {
        const room = await res.json() as { roomId: string }
        navigate(`/room/${room.roomId}`)
      } else if (res.status === 409) {
        setJoinError('Room not found or already full.')
      } else {
        setJoinError('Something went wrong. Try again.')
      }
    } finally {
      setJoiningRoom(false)
    }
  }

  const rooms = data?.rooms ?? []
  const games = data?.games ?? []

  return (
    <div className="lobby-page">
      <header className="lobby-header">
        <span className="lobby-logo">MEEPLITON</span>
        <ThemeToggle />
        <div className="lobby-user">
          {user?.displayName && (
            <span className="lobby-username">{user.displayName}</span>
          )}
          <button className="btn btn-sm btn-danger" onClick={logout}>
            Sign out
          </button>
        </div>
      </header>

      <main className="lobby-content">
        {/* Join a room */}
        <section aria-label="Join a room">
          <h2 className="lobby-section-title">Join a room</h2>
          <div className="lobby-join">
            <form className="lobby-join-form" onSubmit={handleJoin} noValidate>
              <div className="lobby-join-input-wrap">
                <input
                  className="lobby-join-input"
                  type="text"
                  inputMode="text"
                  placeholder="XXXXXX"
                  value={joinCode}
                  onChange={handleJoinInput}
                  maxLength={6}
                  aria-label="6-character room code"
                  aria-describedby={joinError ? 'join-error' : undefined}
                  autoComplete="off"
                  spellCheck={false}
                />
                {joinError && (
                  <span id="join-error" className="lobby-join-error" role="alert">
                    {joinError}
                  </span>
                )}
              </div>
              <button
                type="submit"
                className="btn btn-primary"
                disabled={joiningRoom || joinCode.length !== 6}
                style={{ minHeight: '44px' }}
              >
                {joiningRoom ? 'Joining\u2026' : 'Join'}
              </button>
            </form>
          </div>
        </section>

        {/* Active rooms */}
        <section aria-label="Your active rooms">
          <h2 className="lobby-section-title">Your rooms</h2>
          {loadingLobby ? (
            <p className="lobby-empty-text">Loading\u2026</p>
          ) : rooms.length === 0 ? (
            <div className="lobby-empty">
              <p className="lobby-empty-text">No active games \u2014 start one below</p>
            </div>
          ) : (
            <ul className="lobby-rooms-list" data-stagger>
              {rooms.map(room => (
                <li key={room.roomId}>
                  <div className="room-card">
                    <span className="room-card-code">{room.joinCode}</span>
                    <div className="room-card-info">
                      <div className="room-card-game">{room.gameName}</div>
                      <div className="room-card-meta">
                        {room.playerCount} player{room.playerCount !== 1 ? 's' : ''}
                      </div>
                    </div>
                    <StatusBadge status={room.status} />
                    <button
                      className="btn btn-secondary btn-sm"
                      onClick={() => navigate(`/room/${room.roomId}`)}
                      style={{ minHeight: '44px' }}
                    >
                      Rejoin
                    </button>
                  </div>
                </li>
              ))}
            </ul>
          )}
        </section>

        {/* New game */}
        <section aria-label="Start a new game">
          <h2 className="lobby-section-title">New game</h2>
          {loadingLobby ? (
            <p className="lobby-empty-text">Loading\u2026</p>
          ) : games.length === 0 ? (
            <div className="lobby-empty">
              <p className="lobby-empty-text">No games available.</p>
            </div>
          ) : (
            <ul className="game-catalogue" data-stagger>
              {games.map(game => (
                <li key={game.gameId} className="game-card">
                  <div className="game-card-body">
                    <div className="game-card-name">{game.name}</div>
                    <p className="game-card-desc">{game.description}</p>
                    <div className="game-card-meta">
                      {game.minPlayers}\u2013{game.maxPlayers} players
                    </div>
                  </div>
                  <div className="game-card-footer">
                    <button
                      className="btn btn-primary btn-full"
                      onClick={() => createRoom(game.gameId)}
                      disabled={creatingGameId !== null}
                    >
                      {creatingGameId === game.gameId ? 'Creating\u2026' : 'Create room'}
                    </button>
                  </div>
                </li>
              ))}
            </ul>
          )}
        </section>
      </main>
    </div>
  )
}
