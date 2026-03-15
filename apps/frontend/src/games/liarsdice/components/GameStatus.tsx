import type { LiarsDiceState, Bid } from '../types'
import { DiceFace } from './DiceFace'
import styles from '../styles.module.css'

interface Props {
  state: LiarsDiceState
  myPlayerId: string
}

function BidDisplay({ bid }: { bid: Bid }) {
  const face = bid.face as 1 | 2 | 3 | 4 | 5 | 6
  return (
    <div className={styles.statusBidDisplay}>
      <span className={styles.statusBidQuantity}>{bid.quantity}×</span>
      <DiceFace value={face} size="lg" />
    </div>
  )
}

export function GameStatus({ state, myPlayerId }: Props) {
  const currentPlayer = state.players[state.currentPlayerIndex]
  const isMyTurn = currentPlayer?.id === myPlayerId

  return (
    <div className={styles.statusPanel}>
      {/* Palifico banner */}
      {state.palificoActive && (
        <div className={styles.statusPalifico} role="status">
          <span className={styles.statusPalificoIcon}>⚓</span>
          <span>Palifico round — no wilds</span>
        </div>
      )}

      {/* Round indicator */}
      <div className={styles.statusRound}>
        Round {state.roundNumber}
      </div>

      {/* Current bid */}
      <div className={styles.statusBidSection}>
        {state.currentBid !== null ? (
          <>
            <div className={styles.statusBidLabel}>Current bid</div>
            <BidDisplay bid={state.currentBid} />
          </>
        ) : (
          <div className={styles.statusNoBid}>
            No bid yet — make the opening bid
          </div>
        )}
      </div>

      {/* Whose turn */}
      {state.phase === 'Bidding' && currentPlayer && (
        <div className={`${styles.statusTurn} ${isMyTurn ? styles.statusTurnMe : ''}`}>
          {isMyTurn ? 'Your turn' : `${currentPlayer.displayName}'s turn`}
        </div>
      )}

      {/* Reveal result */}
      {state.phase === 'Reveal' && state.lastChallengeResult && (
        <div className={styles.statusRevealResult} role="status">
          {state.lastChallengeResult}
        </div>
      )}

      {/* Reveal details */}
      {state.phase === 'Reveal' && state.lastReveal && (
        <div className={styles.statusRevealDetail}>
          Actual count: <strong>{state.lastReveal.actualCount}</strong>
          {' '}(bid was {state.lastReveal.challengedBid.quantity}×{state.lastReveal.challengedBid.face})
        </div>
      )}

      {/* Winner */}
      {state.phase === 'Finished' && state.winner && (
        <div className={styles.statusWinner}>
          {state.winner === myPlayerId ? 'You win!' : (
            <>
              {state.players.find(p => p.id === state.winner)?.displayName ?? 'Unknown'} wins!
            </>
          )}
        </div>
      )}
    </div>
  )
}
