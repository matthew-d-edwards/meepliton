import { useState } from 'react'
import type { GameContext } from '@meepliton/contracts'
import type { SkylineState, SkylineAction, PlayerState } from '../types'
import styles from '../styles.module.css'

const BOARD_SIZE = 5

export default function Game({ state, myPlayerId, dispatch }: GameContext<SkylineState>) {
  const [selectedTile, setSelectedTile] = useState<number | null>(null)

  const isMyTurn = state.currentPlayerId === myPlayerId
  const currentPlayer = state.players.find(p => p.id === state.currentPlayerId)
  const me = state.players.find(p => p.id === myPlayerId)

  function handleTileClick(tileValue: number) {
    if (!isMyTurn) return
    setSelectedTile((prev: number | null) => (prev === tileValue ? null : tileValue))
  }

  function handleCellClick(row: number, col: number) {
    if (!isMyTurn || selectedTile === null || state.board[row]?.[col] !== null) return
    const action: SkylineAction = {
      type: 'PlaceTile',
      placeTile: { row, col, tileValue: selectedTile },
    }
    dispatch(action)
    setSelectedTile(null)
  }

  if (state.phase === 'GameOver') {
    const winner = state.players.find(p => p.id === state.winnerId)
    return (
      <div>
        <h2>Game over!</h2>
        <p>{winner ? `${winner.displayName} wins with ${winner.score} points!` : 'Draw!'}</p>
        <Scoreboard players={state.players} />
      </div>
    )
  }

  return (
    <div>
      {/* Turn indicator */}
      <p>
        {isMyTurn
          ? 'Your turn — pick a tile then click a cell'
          : `Waiting for ${currentPlayer?.displayName ?? 'opponent'}…`}
      </p>

      <Scoreboard players={state.players} />

      {/* My hand */}
      {me && (
        <div>
          <p className={styles.handLabel}>
            {isMyTurn ? 'Your Hand' : `Waiting for ${currentPlayer?.displayName ?? 'opponent'}'s turn…`}
          </p>
          <div className={styles.hand}>
            {me.hand.map((tileValue, i) => (
              <div
                key={i}
                className={`${styles.tile} ${isMyTurn && selectedTile === tileValue ? styles.selected : ''} ${!isMyTurn ? styles.tileDisabled : ''}`}
                onClick={() => handleTileClick(tileValue)}
                role="button"
                aria-label={`Tile ${tileValue}`}
                aria-disabled={!isMyTurn}
                aria-pressed={isMyTurn && selectedTile === tileValue}
              >
                {tileValue}
              </div>
            ))}
          </div>
        </div>
      )}

      {/* Opponents' hidden hands */}
      {state.players
        .filter(p => p.id !== myPlayerId)
        .map(opponent => (
          <OpponentHand key={opponent.id} opponent={opponent} />
        ))}

      {/* Board */}
      <div className={styles.board}>
        {Array.from({ length: BOARD_SIZE }, (_, row) =>
          Array.from({ length: BOARD_SIZE }, (_, col) => {
            const value = state.board[row]?.[col] ?? null
            const isPlaceable = isMyTurn && selectedTile !== null && value === null
            return (
              <div
                key={`${row}-${col}`}
                className={`${styles.cell} ${value !== null ? styles.occupied : ''} ${isPlaceable ? styles.placeable : ''}`}
                onClick={() => handleCellClick(row, col)}
                role="button"
                aria-label={value !== null ? `Occupied: ${value}` : isPlaceable ? 'Place tile here' : 'Empty cell'}
              >
                {value ?? ''}
              </div>
            )
          })
        )}
      </div>
    </div>
  )
}

// Renders a row of face-down tile placeholders for an opponent.
// The hand array is always [] for opponents in multiplayer (backend projection),
// so we fall back to showing a fixed placeholder count of 3 (HandSize).
const OPPONENT_HAND_PLACEHOLDER_COUNT = 3

function OpponentHand({ opponent }: { opponent: PlayerState }) {
  // In multiplayer opponent.hand is always [] — show placeholder tiles.
  // If somehow the hand is non-empty (e.g. local dev pass-and-play), still hide it.
  const count = opponent.hand.length > 0 ? opponent.hand.length : OPPONENT_HAND_PLACEHOLDER_COUNT
  return (
    <div>
      <p className={styles.handLabel}>{opponent.displayName}'s hand</p>
      <div className={styles.hand}>
        {Array.from({ length: count }, (_, i) => (
          <div
            key={i}
            className={`${styles.tile} ${styles.tileHidden}`}
            aria-label="Hidden tile"
          />
        ))}
      </div>
    </div>
  )
}

function Scoreboard({ players }: { players: SkylineState['players'] }) {
  return (
    <div className={styles.scoreboard}>
      {players
        .slice()
        .sort((a, b) => b.score - a.score)
        .map(p => (
          <span key={p.id}>{p.displayName}: {p.score}</span>
        ))}
    </div>
  )
}
