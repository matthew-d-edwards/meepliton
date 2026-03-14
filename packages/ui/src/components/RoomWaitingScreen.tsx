import type { PlayerInfo } from '@meepliton/contracts'
import { JoinCodeDisplay } from './JoinCodeDisplay'
import { PlayerPresence } from './PlayerPresence'

interface Props {
  joinCode:         string
  players:          PlayerInfo[]
  isHost:           boolean
  onStart:          () => void
  minPlayers?:      number
  onRemovePlayer?:  (playerId: string) => void
}

export function RoomWaitingScreen({ joinCode, players, isHost, onStart, minPlayers = 2, onRemovePlayer }: Props) {
  const canStart = isHost && players.length >= minPlayers

  return (
    <main className="room-waiting">
      <h2>Waiting for players</h2>
      <JoinCodeDisplay code={joinCode} />
      {onRemovePlayer && isHost ? (
        <ul className="player-presence">
          {players.map(p => (
            <li key={p.id} className={`player-presence__player ${p.connected ? 'connected' : 'disconnected'}`}>
              {p.avatarUrl && <img src={p.avatarUrl} alt="" className="player-presence__avatar" />}
              <span>{p.displayName}</span>
              <span className="player-presence__dot" aria-label={p.connected ? 'Online' : 'Offline'} />
              <button
                onClick={() => onRemovePlayer(p.id)}
                aria-label={`Remove ${p.displayName}`}
                className="player-presence__remove"
              >
                ×
              </button>
            </li>
          ))}
        </ul>
      ) : (
        <PlayerPresence players={players} />
      )}
      {isHost && (
        <button onClick={onStart} disabled={!canStart}>
          {canStart ? 'Start game' : `Need ${minPlayers - players.length} more player(s)`}
        </button>
      )}
      {!isHost && <p>Waiting for the host to start…</p>}
    </main>
  )
}
