import type { PlayerInfo } from '@meepliton/contracts'
import { JoinCodeDisplay } from './JoinCodeDisplay'
import { PlayerPresence } from './PlayerPresence'

interface Props {
  joinCode:  string
  players:   PlayerInfo[]
  isHost:    boolean
  onStart:   () => void
  minPlayers?: number
}

export function RoomWaitingScreen({ joinCode, players, isHost, onStart, minPlayers = 2 }: Props) {
  const canStart = isHost && players.length >= minPlayers

  return (
    <main className="room-waiting">
      <h2>Waiting for players</h2>
      <JoinCodeDisplay code={joinCode} />
      <PlayerPresence players={players} />
      {isHost && (
        <button onClick={onStart} disabled={!canStart}>
          {canStart ? 'Start game' : `Need ${minPlayers - players.length} more player(s)`}
        </button>
      )}
      {!isHost && <p>Waiting for the host to start…</p>}
    </main>
  )
}
