import { useState } from 'react'
import type { GameContext } from '@meepliton/contracts'
import type { SkylineState, SkylineAction } from '../types'
import styles from '../styles.module.css'

const BOARD_SIZE = 5

export default function Game({ state, myPlayerId, dispatch }: GameContext<SkylineState>) {
  const [selectedTile, setSelectedTile] = useState<number | null>(null)

  const me = state.players.find(p => p.id === myPlayerId)
  const isMyTurn = state.currentPlayerId === myPlayerId
  const currentPlayer = state.players.find(p => p.id === state.currentPlayerId)

  function handleCellClick(row: number, col: number) {
    if (!isMyTurn || selectedTile === null || state.board[row][col] !== null) return
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
      <p>
        {isMyTurn
          ? 'Your turn — pick a tile then click a cell'
          : `Waiting for ${currentPlayer?.displayName}…`}
      </p>

      <Scoreboard players={state.players} />

      {/* Hand */}
      {me && (
        <div className={styles.hand}>
          {me.hand.map((tileValue, i) => (
            <div
              key={i}
              className={`${styles.tile} ${selectedTile === tileValue ? styles.selected : ''}`}
              onClick={() => isMyTurn && setSelectedTile(tileValue === selectedTile ? null : tileValue)}
              role="button"
              aria-label={`Tile ${tileValue}`}
            >
              {tileValue}
            </div>
          ))}
        </div>
      )}

      {/* Board */}
      <div className={styles.board}>
        {Array.from({ length: BOARD_SIZE }, (_, row) =>
          Array.from({ length: BOARD_SIZE }, (_, col) => {
            const value = state.board[row]?.[col]
            return (
              <div
                key={`${row}-${col}`}
                className={`${styles.cell} ${value !== null ? styles.occupied : ''}`}
                onClick={() => handleCellClick(row, col)}
                role="button"
                aria-label={value !== null ? `${value}` : 'Empty cell'}
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
