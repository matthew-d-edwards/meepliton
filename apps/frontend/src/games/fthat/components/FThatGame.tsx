import type { GameContext } from '@meepliton/contracts'
import type { FThatView, FThatPlayerView } from '../types'
import '../fthat.css'
import styles from '../styles.module.css'

export default function FThatGame({ state, myPlayerId, dispatch }: GameContext<FThatView>) {
  const currentPlayer = state.players[state.currentPlayerIndex]
  const isMyTurn = currentPlayer?.id === myPlayerId
  const me = state.players.find(p => p.id === myPlayerId)

  function send(type: 'Take' | 'Pass') {
    dispatch({ type })
  }

  function nameFor(playerId: string): string {
    return state.players.find(p => p.id === playerId)?.displayName ?? playerId
  }

  return (
    <div data-game-theme="fthat" className={styles.root}>

      {/* ── Card Info Row ── */}
      <div className={styles.cardInfoRow}>
        <div className={styles.cardDisplay}>
          <span className={styles.cardLabel}>Card</span>
          <span className={styles.cardValue}>😱 {state.faceUpCard}</span>
        </div>
        <div className={styles.metaChip}>
          <span className={styles.metaLabel}>Chips on it</span>
          <span className={styles.metaValue}>💰 {state.chipsOnCard}</span>
        </div>
        <div className={styles.metaChip}>
          <span className={styles.metaLabel}>Cards left</span>
          <span className={styles.metaValue}>📦 {state.deckCount}</span>
        </div>
      </div>

      {/* ── Players List ── */}
      <div className={styles.playersList}>
        {state.players.map(player => {
          const isCurrent = player.id === currentPlayer?.id
          const sortedCards = [...player.cards].sort((a, b) => a - b)

          return (
            <PlayerRow
              key={player.id}
              player={player}
              isCurrent={isCurrent}
              isMe={player.id === myPlayerId}
              sortedCards={sortedCards}
            />
          )
        })}
      </div>

      {/* ── Action Panel ── */}
      {state.phase === 'Playing' && isMyTurn && me !== undefined && (
        <div className={styles.actionPanel}>
          <span className={styles.actionLabel}>Your move</span>
          <div className={styles.actionRow}>
            <button
              type="button"
              className={`${styles.actionBtn} ${styles.actionBtnPass}`}
              disabled={me.chips === 0}
              title={me.chips === 0 ? 'No chips — you must take it!' : undefined}
              onClick={() => send('Pass')}
            >
              F&apos;THAT 🤮
            </button>
            <button
              type="button"
              className={`${styles.actionBtn} ${styles.actionBtnTake}`}
              onClick={() => send('Take')}
            >
              Fine, I&apos;ll Take It 😭
            </button>
          </div>
          {me.chips === 0 && (
            <span style={{ fontFamily: 'var(--font-mono)', fontSize: '0.72rem', color: 'var(--color-text-muted, var(--text-muted))' }}>
              No chips — you must take it!
            </span>
          )}
        </div>
      )}

      {/* ── GameOver: Score Table + Winner ── */}
      {state.phase === 'GameOver' && state.scores !== null && (
        <div className={styles.scoreSection}>
          {state.winners !== null && state.winners.length > 0 && (
            <div className={styles.winnerBanner}>
              {state.winners.length === 1
                ? `🎉 ${nameFor(state.winners[0])} wins!`
                : `🎉 ${state.winners.map(id => nameFor(id)).join(' & ')} tie!`}
            </div>
          )}
          <table className={styles.scoreTable}>
            <thead>
              <tr>
                <th>Player</th>
                <th>Card Score</th>
                <th>Chips</th>
                <th>Total</th>
              </tr>
            </thead>
            <tbody>
              {state.scores
                .slice()
                .sort((a, b) => a.total - b.total)
                .map(score => {
                  const isWinner = state.winners?.includes(score.playerId) ?? false
                  return (
                    <tr
                      key={score.playerId}
                      className={isWinner ? styles.scoreRowWinner : ''}
                    >
                      <td>
                        {nameFor(score.playerId)}
                        {isWinner && ' 🏆'}
                      </td>
                      <td>{score.cardScore}</td>
                      <td>−{score.chips}</td>
                      <td>{score.total}</td>
                    </tr>
                  )
                })}
            </tbody>
          </table>
        </div>
      )}
    </div>
  )
}

interface PlayerRowProps {
  player: FThatPlayerView
  isCurrent: boolean
  isMe: boolean
  sortedCards: number[]
}

function PlayerRow({ player, isCurrent, isMe, sortedCards }: PlayerRowProps) {
  return (
    <div className={[styles.playerRow, isCurrent ? styles.playerRowCurrent : ''].filter(Boolean).join(' ')}>
      <span className={[styles.playerName, isCurrent ? styles.playerNameBold : ''].filter(Boolean).join(' ')}>
        {player.displayName}
        {isMe && <span style={{ fontWeight: 400, fontSize: '0.78rem', color: 'var(--color-text-muted)' }}> (you)</span>}
      </span>
      <span className={styles.playerChips}>
        {player.chipsHidden ? '?? chips' : `${player.chips} chips`}
      </span>
      <div className={styles.playerCards}>
        {sortedCards.length > 0
          ? sortedCards.map((card, i) => (
              <span key={i} className={styles.cardPill}>{card}</span>
            ))
          : <span className={styles.noCards}>no cards yet</span>}
      </div>
    </div>
  )
}
