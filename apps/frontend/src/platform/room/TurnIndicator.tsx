import { Avatar } from '@meepliton/ui'

interface TurnIndicatorProps {
  currentPlayerId: string | null
  players: Array<{ id: string; displayName: string; avatarUrl?: string | null }>
  myPlayerId: string
}

export function TurnIndicator({ currentPlayerId, players, myPlayerId }: TurnIndicatorProps) {
  if (!currentPlayerId) return null

  const isMyTurn = currentPlayerId === myPlayerId
  const currentPlayer = players.find(p => p.id === currentPlayerId)

  return (
    <div className={`turn-indicator${isMyTurn ? ' turn-indicator--mine' : ''}`} aria-live="polite">
      {currentPlayer && (
        <Avatar
          url={currentPlayer.avatarUrl}
          displayName={currentPlayer.displayName}
          size="sm"
        />
      )}
      {isMyTurn
        ? <span className="turn-indicator__label">Your turn</span>
        : <span className="turn-indicator__label">{currentPlayer?.displayName ?? 'Unknown'}'s turn</span>
      }
    </div>
  )
}
