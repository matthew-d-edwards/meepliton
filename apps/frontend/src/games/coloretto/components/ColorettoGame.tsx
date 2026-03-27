import { useState } from 'react'
import type { GameContext } from '@meepliton/contracts'
import type { ColorettoState, ColorettoAction, ColorettoRow, PlayerScore } from '../types'
import '../coloretto.css'
import styles from '../styles.module.css'

// ── Colour helpers ────────────────────────────────────────────────────────

function cardColor(card: string): string {
  const map: Record<string, string> = {
    Brown:   'var(--card-brown)',
    Blue:    'var(--card-blue)',
    Green:   'var(--card-green)',
    Orange:  'var(--card-orange)',
    Purple:  'var(--card-purple)',
    Red:     'var(--card-red)',
    Yellow:  'var(--card-yellow)',
    Joker:   'var(--card-joker)',
    EndGame: 'var(--card-endgame)',
  }
  return map[card] ?? '#888'
}

function cardTextColor(card: string): string {
  const lightCards = new Set(['Yellow', 'Joker'])
  return lightCards.has(card) ? '#000' : '#fff'
}

// ── Scoring scale ─────────────────────────────────────────────────────────

const SCORE_SCALE = [0, 1, 3, 6, 10, 15, 21, 28, 36, 45]

function scoreForCount(n: number): number {
  return SCORE_SCALE[Math.min(n, SCORE_SCALE.length - 1)] ?? 45
}

// ── Card chip ─────────────────────────────────────────────────────────────

interface CardChipProps {
  card:  string
  small?: boolean
}

function CardChip({ card, small }: CardChipProps) {
  const bg   = cardColor(card)
  const fg   = cardTextColor(card)
  const size = small ? 24 : 36
  return (
    <div
      style={{
        width: size,
        height: size,
        background: bg,
        color: fg,
        display: 'flex',
        alignItems: 'center',
        justifyContent: 'center',
        fontSize: small ? '0.55rem' : '0.62rem',
        fontWeight: 700,
        fontFamily: 'var(--font-mono)',
        letterSpacing: '-0.3px',
        flexShrink: 0,
        border: '2px solid var(--color-border, #1a1a1a)',
      }}
      title={card}
      aria-label={card}
    >
      {card === 'EndGame' ? '⚑' : card === 'Joker' ? '★' : card.slice(0, 2)}
    </div>
  )
}

// ── Row display ───────────────────────────────────────────────────────────

interface RowDisplayProps {
  row:              ColorettoRow
  isCurrentPlayer:  boolean
  hasTaken:         boolean
  onDraw:           (rowIndex: number) => void
  onTake:           (rowIndex: number) => void
  selectedRow:      number | null
  onSelectRow:      (rowIndex: number) => void
}

function RowDisplay({ row, isCurrentPlayer, hasTaken, onDraw, onTake, selectedRow, onSelectRow }: RowDisplayProps) {
  const isFull = row.cards.length >= 3
  const isSelected = selectedRow === row.rowIndex
  const isEmpty = row.cards.length === 0

  return (
    <div
      className={[styles.row, isSelected && isCurrentPlayer ? styles.rowSelected : ''].filter(Boolean).join(' ')}
      onClick={() => isCurrentPlayer && !hasTaken && onSelectRow(row.rowIndex)}
      style={{ cursor: isCurrentPlayer && !hasTaken ? 'pointer' : 'default' }}
    >
      {/* Card slots */}
      <div className={styles.rowCards}>
        {row.cards.map((card, i) => <CardChip key={i} card={card} />)}
        {Array.from({ length: 3 - row.cards.length }).map((_, i) => (
          <div key={`empty-${i}`} className={styles.rowEmptySlot} />
        ))}
      </div>

      {/* Full badge */}
      {isFull && (
        <span className={styles.rowFullBadge}>FULL — TAKE ONLY</span>
      )}

      {/* Action buttons */}
      {isCurrentPlayer && !hasTaken && isSelected && (
        <div className={styles.rowActions}>
          <button
            type="button"
            className={`${styles.btn} ${styles.btnSecondary}`}
            disabled={isFull}
            onClick={e => { e.stopPropagation(); onDraw(row.rowIndex) }}
          >
            Draw here
          </button>
          <button
            type="button"
            className={`${styles.btn} ${styles.btnPrimary}`}
            onClick={e => { e.stopPropagation(); onTake(row.rowIndex) }}
          >
            Take row{isEmpty ? ' (empty)' : ''}
          </button>
        </div>
      )}
    </div>
  )
}

// ── Collection display ────────────────────────────────────────────────────

interface CollectionDisplayProps {
  collection: Record<string, number>
  topColors:  string[]
  showScore?: boolean
}

function CollectionDisplay({ collection, topColors, showScore }: CollectionDisplayProps) {
  const entries = Object.entries(collection).filter(([, count]) => count > 0)
  if (entries.length === 0) return <span style={{ fontSize: '0.72rem', color: 'var(--color-text-muted)' }}>Empty</span>

  return (
    <div className={styles.collection}>
      {entries.sort((a, b) => b[1] - a[1]).map(([colour, count]) => {
        const isTop = topColors.includes(colour)
        return (
          <div key={colour} className={styles.collectionEntry}>
            <CardChip card={colour} small />
            <span className={styles.collectionCount}>{count}</span>
            {showScore && (
              <span className={isTop ? styles.collectionScorePos : styles.collectionScoreNeg}>
                {isTop ? '+' : '-'}{scoreForCount(count)}
              </span>
            )}
          </div>
        )
      })}
    </div>
  )
}

// ── Score row ─────────────────────────────────────────────────────────────

interface ScoreRowProps {
  score:   PlayerScore
  players: ColorettoState['players']
  isMe:    boolean
  isWinner: boolean
}

function ScoreRow({ score, players, isMe, isWinner }: ScoreRowProps) {
  const player = players.find(p => p.id === score.playerId)
  return (
    <div className={[styles.scoreRow, isMe ? styles.scoreRowMe : '', isWinner ? styles.scoreRowWinner : ''].filter(Boolean).join(' ')}>
      <span className={styles.scoreRowName}>
        {player?.displayName ?? score.playerId}{isMe ? ' (you)' : ''}
        {isWinner && ' 🏆'}
      </span>
      <CollectionDisplay collection={score.collection} topColors={score.topColors} showScore />
      <span className={styles.scoreRowTotal}>{score.total}</span>
    </div>
  )
}

// ── Main component ────────────────────────────────────────────────────────

export default function ColorettoGame({ state, myPlayerId, dispatch }: GameContext<ColorettoState>) {
  const [selectedRow, setSelectedRow] = useState<number | null>(null)

  const me = state.players.find(p => p.id === myPlayerId)
  const isHost = state.players.some(p => p.id === myPlayerId && p.seatIndex === 0)
  const currentPlayer = state.players[state.currentPlayerIndex]
  const isMyTurn = currentPlayer?.id === myPlayerId && !(me?.hasTakenThisRound)

  function send(action: ColorettoAction) {
    dispatch(action)
  }

  function handleDraw(rowIndex: number) {
    send({ type: 'DrawCard', rowIndex })
    setSelectedRow(null)
  }

  function handleTake(rowIndex: number) {
    send({ type: 'TakeRow', rowIndex })
    setSelectedRow(null)
  }

  const bestScores = state.finalScores
    ? Math.max(...state.finalScores.scores.map(s => s.total))
    : null

  return (
    <div data-game-theme="chameleon-market" className={styles.root}>

      {/* Header */}
      <div className={styles.header}>
        <span className={styles.headerTitle}>Chameleon Market</span>
        {state.phase !== 'Waiting' && (
          <span className={styles.headerInfo}>
            {state.deckSize} cards left
            {state.endGameTriggered && ' · Final round!'}
          </span>
        )}
      </div>

      {/* ── Waiting ── */}
      {state.phase === 'Waiting' && (
        <div className={styles.waitingArea}>
          <div className={styles.waitingTitle}>Waiting for players…</div>
          <div className={styles.waitingSubtitle}>
            {state.players.length} player{state.players.length !== 1 ? 's' : ''} in room
          </div>
          {isHost ? (
            <div className={styles.actionRow}>
              <button type="button" className={`${styles.btn} ${styles.btnPrimary}`}
                onClick={() => send({ type: 'StartGame' })}>
                Start Game
              </button>
            </div>
          ) : (
            <div className={styles.waitingSubtitle}>Waiting for the host to start…</div>
          )}
        </div>
      )}

      {/* ── End game banner ── */}
      {state.phase === 'Playing' && state.endGameTriggered && (
        <div className={`${styles.statusBanner} ${styles.statusBannerEndGame}`}>
          ⚑ Final round in progress — game ends when everyone has taken a row
        </div>
      )}

      {/* ── Turn indicator ── */}
      {state.phase === 'Playing' && !state.endGameTriggered && (
        <div className={styles.statusBanner}>
          {isMyTurn
            ? '🎴 Your turn — click a row'
            : me?.hasTakenThisRound
              ? '✓ You have taken this round — waiting for others'
              : `${currentPlayer?.displayName}'s turn`}
        </div>
      )}

      {/* ── Rows ── */}
      {state.phase === 'Playing' && (
        <div className={styles.rows}>
          {state.rows.map(row => (
            <RowDisplay
              key={row.rowIndex}
              row={row}
              isCurrentPlayer={isMyTurn}
              hasTaken={me?.hasTakenThisRound ?? false}
              onDraw={handleDraw}
              onTake={handleTake}
              selectedRow={selectedRow}
              onSelectRow={setSelectedRow}
            />
          ))}
        </div>
      )}

      {/* ── Players ── */}
      {state.phase !== 'Waiting' && (
        <div className={styles.playersSection}>
          <div className={styles.playersSectionTitle}>Collections</div>
          <div className={styles.playersGrid}>
            {state.players.map(player => {
              const isMe = player.id === myPlayerId
              const isCurrentTurn = player.id === currentPlayer?.id && !player.hasTakenThisRound
              return (
                <div
                  key={player.id}
                  className={[
                    styles.playerCard,
                    isMe ? styles.playerCardMe : '',
                    isCurrentTurn ? styles.playerCardCurrentTurn : '',
                    player.hasTakenThisRound ? styles.playerCardTaken : '',
                  ].filter(Boolean).join(' ')}
                >
                  <div className={styles.playerCardHeader}>
                    <span className={styles.playerName}>
                      {player.displayName}{isMe ? ' (you)' : ''}
                    </span>
                    {player.hasTakenThisRound && (
                      <span className={styles.takenTag}>Done</span>
                    )}
                  </div>
                  <CollectionDisplay
                    collection={player.collection}
                    topColors={[]}
                  />
                </div>
              )
            })}
          </div>
        </div>
      )}

      {/* ── Finished ── */}
      {state.phase === 'Finished' && state.finalScores && (
        <div className={styles.scoresPanel}>
          <div className={styles.scoresPanelTitle}>Final Scores</div>
          {state.finalScores.scores
            .sort((a, b) => b.total - a.total)
            .map(score => (
              <ScoreRow
                key={score.playerId}
                score={score}
                players={state.players}
                isMe={score.playerId === myPlayerId}
                isWinner={score.total === bestScores}
              />
            ))}
        </div>
      )}

      {state.phase === 'Finished' && !state.winner && (
        <div className={styles.statusBanner}>It's a tie!</div>
      )}
      {state.phase === 'Finished' && state.winner && (
        <div className={styles.winnerBanner}>
          <span className={styles.winnerTitle}>Winner</span>
          <span className={styles.winnerName}>
            {state.players.find(p => p.id === state.winner)?.displayName ?? state.winner}
          </span>
        </div>
      )}
    </div>
  )
}
