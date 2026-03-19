import type { PlayerInfo } from '@meepliton/contracts'
import { JoinCodeDisplay } from './JoinCodeDisplay'
import { PlayerPresence } from './PlayerPresence'

interface Props {
  joinCode:          string
  players:           PlayerInfo[]
  isHost:            boolean
  onStart:           () => void
  minPlayers?:       number
  onRemovePlayer?:   (playerId: string) => void
  onTransferHost?:   (playerId: string) => void
  currentUserId?:    string
}

export function RoomWaitingScreen({ joinCode, players, isHost, onStart, minPlayers = 2, onRemovePlayer, onTransferHost, currentUserId }: Props) {
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
              <span className="player-presence__dot" aria-hidden="true" />
              <span className="sr-only">{p.connected ? 'Online' : 'Offline'}</span>
              <button
                onClick={() => onRemovePlayer(p.id)}
                aria-label={`Remove ${p.displayName}`}
                className="player-presence__remove"
              >
                ×
              </button>
              {onTransferHost && p.id !== currentUserId && (
                <button
                  onClick={() => onTransferHost(p.id)}
                  aria-label={`Make ${p.displayName} the host`}
                  className="player-presence__transfer"
                >
                  ★
                </button>
              )}
            </li>
          ))}
        </ul>
      ) : (
        <PlayerPresence players={players} />
      )}
      {isHost && (
        <button onClick={onStart} disabled={!canStart}>
          {canStart ? 'Start game' : `Need ${minPlayers - players.length} more ${minPlayers - players.length === 1 ? 'player' : 'players'}`}
        </button>
      )}
      {!isHost && <p>Waiting for the host to start…</p>}
    </main>
  )
}
