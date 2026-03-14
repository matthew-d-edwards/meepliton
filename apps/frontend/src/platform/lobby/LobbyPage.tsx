import { useEffect, useState } from 'react'
import { useNavigate } from 'react-router-dom'
import { useAuth } from '../auth/AuthContext'

interface GameInfo { id: string; name: string; description: string; minPlayers: number; maxPlayers: number }
interface RoomInfo  { id: string; gameId: string; joinCode: string; status: string }

export default function LobbyPage() {
  const { user, logout } = useAuth()
  const navigate = useNavigate()
  const [games, setGames] = useState<GameInfo[]>([])
  const [myRooms, setMyRooms] = useState<RoomInfo[]>([])
  const [joinCode, setJoinCode] = useState('')

  useEffect(() => {
    fetch('/api/lobby', { credentials: 'include' })
      .then(r => r.json())
      .then(data => { setGames(data.games ?? []); setMyRooms(data.myRooms ?? []) })
  }, [])

  async function createRoom(gameId: string) {
    const res = await fetch('/api/rooms', {
      method: 'POST',
      credentials: 'include',
      headers: { 'Content-Type': 'application/json' },
      body: JSON.stringify({ gameId }),
    })
    if (res.ok) {
      const room = await res.json()
      navigate(`/room/${room.id}`)
    }
  }

  async function handleJoin(e: React.FormEvent) {
    e.preventDefault()
    const res = await fetch('/api/rooms/join', {
      method: 'POST',
      credentials: 'include',
      headers: { 'Content-Type': 'application/json' },
      body: JSON.stringify({ code: joinCode.toUpperCase() }),
    })
    if (res.ok) {
      const room = await res.json()
      navigate(`/room/${room.id}`)
    }
  }

  return (
    <main className="lobby-page">
      <header className="lobby-header">
        <h1>Meepliton</h1>
        <span>{user?.displayName}</span>
        <button onClick={logout}>Sign out</button>
      </header>

      <section className="lobby-join">
        <form onSubmit={handleJoin}>
          <input
            placeholder="Join code"
            value={joinCode}
            onChange={e => setJoinCode(e.target.value)}
            maxLength={6}
          />
          <button type="submit">Join</button>
        </form>
      </section>

      {myRooms.length > 0 && (
        <section className="lobby-my-rooms">
          <h2>Your rooms</h2>
          <ul>
            {myRooms.map(r => (
              <li key={r.id}>
                <button onClick={() => navigate(`/room/${r.id}`)}>
                  {r.gameId} — {r.joinCode} ({r.status})
                </button>
              </li>
            ))}
          </ul>
        </section>
      )}

      <section className="lobby-games">
        <h2>Games</h2>
        <ul className="game-catalogue">
          {games.map(g => (
            <li key={g.id} className="game-card">
              <h3>{g.name}</h3>
              <p>{g.description}</p>
              <small>{g.minPlayers}–{g.maxPlayers} players</small>
              <button onClick={() => createRoom(g.id)}>Create room</button>
            </li>
          ))}
        </ul>
      </section>
    </main>
  )
}
