import { useEffect, useState } from 'react'
import type { LiarsDiceState, Bid, RevealSnapshot } from '../types'
import { DiceFace } from './DiceFace'
import styles from '../styles.module.css'

interface Props {
  state: LiarsDiceState
  myPlayerId: string
}

function RevealScoreline({ reveal, loserId, myPlayerId }: { reveal: RevealSnapshot; loserId: string; myPlayerId: string }) {
  const face = reveal.challengedBid.face as 1 | 2 | 3 | 4 | 5 | 6
  const bidMet = reveal.actualCount >= reveal.challengedBid.quantity
  const iLost = loserId === myPlayerId
  const loserName = iLost ? 'One die falls.' : undefined

  return (
    <div
      className={styles.revealScoreline}
      role="status"
      aria-live="polite"
      aria-atomic="true"
      aria-label={`Found ${reveal.actualCount} dice showing ${face}, bid was ${reveal.challengedBid.quantity}. ${bidMet ? 'Bid proven.' : 'Bid failed.'}`}
    >
      {/* Found side */}
      <div className={`${styles.revealSide} ${bidMet ? styles.revealSideWin : styles.revealSideLose}`}>
        <span className={styles.revealSideLabel}>Found</span>
        <div className={styles.revealSideCount} aria-hidden="true">
          <span className={styles.revealQty}>{reveal.actualCount}</span>
          <span className={styles.revealTimes} aria-hidden="true">×</span>
          <DiceFace value={face} size="md" highlighted={bidMet} />
        </div>
      </div>

      <span className={styles.revealVs} aria-hidden="true">vs</span>

      {/* Bid side */}
      <div className={`${styles.revealSide} ${styles.revealSideBid}`}>
        <span className={styles.revealSideLabel}>Bid</span>
        <div className={styles.revealSideCount} aria-hidden="true">
          <span className={styles.revealQty}>{reveal.challengedBid.quantity}</span>
          <span className={styles.revealTimes} aria-hidden="true">×</span>
          <DiceFace value={face} size="md" />
        </div>
      </div>

      {/* Loser tag */}
      {loserName && (
        <div className={styles.revealLoserTag} aria-live="assertive">
          {loserName}
        </div>
      )}
    </div>
  )
}

function BidDisplay({ bid }: { bid: Bid }) {
  const face = bid.face as 1 | 2 | 3 | 4 | 5 | 6
  return (
    <div
      className={styles.statusBidDisplay}
      aria-label={`Current bid: ${bid.quantity} dice showing ${bid.face}`}
    >
      <span className={styles.statusBidQuantity} key={bid.quantity} aria-hidden="true">{bid.quantity}×</span>
      <div className={styles.statusBidDieBreath}>
        <DiceFace value={face} size="lg" />
      </div>
    </div>
  )
}

export function GameStatus({ state, myPlayerId }: Props) {
  const currentPlayer = state.players[state.currentPlayerIndex]
  const isMyTurn = currentPlayer?.id === myPlayerId
  const [showTurnFlash, setShowTurnFlash] = useState(false)

  useEffect(() => {
    if (isMyTurn) {
      setShowTurnFlash(true)
      const timer = setTimeout(() => setShowTurnFlash(false), 1400)
      return () => clearTimeout(timer)
    }
  }, [isMyTurn])

  return (
    <div className={styles.statusPanel}>
      {/* Game title */}
      <h2 className={styles.gameTitle}>Liar's Dice</h2>

      {/* "Your turn" flash overlay */}
      {showTurnFlash && (
        <div className={styles.turnFlash} aria-live="assertive" aria-atomic="true">
          Your move
        </div>
      )}

      {/* Palifico banner */}
      {state.palificoActive && (
        <div className={styles.statusPalifico} role="status" aria-live="polite" aria-atomic="true">
          <span className={styles.statusPalificoIcon} aria-hidden="true">⚓</span>
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
            The table is quiet. Open the bidding.
          </div>
        )}
      </div>

      {/* Whose turn */}
      {state.phase === 'Bidding' && currentPlayer && (
        <div
          className={`${styles.statusTurn} ${isMyTurn ? styles.statusTurnMe : ''}`}
          role="status"
          aria-live="polite"
          aria-atomic="true"
        >
          {isMyTurn ? 'Your turn' : `${currentPlayer.displayName}'s turn`}
        </div>
      )}

      {/* Reveal result verdict */}
      {state.phase === 'Reveal' && state.lastChallengeResult && (
        <div className={styles.statusRevealResult} role="status">
          {state.lastChallengeResult}
        </div>
      )}

      {/* Reveal scoreline — found × [face] vs bid × [face] */}
      {state.phase === 'Reveal' && state.lastReveal && (
        <RevealScoreline
          reveal={state.lastReveal}
          loserId={state.lastReveal.loserId}
          myPlayerId={myPlayerId}
        />
      )}

      {/* Winner */}
      {state.phase === 'Finished' && state.winner && (
        <div className={styles.statusWinner} role="status" aria-live="assertive" aria-atomic="true">
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
