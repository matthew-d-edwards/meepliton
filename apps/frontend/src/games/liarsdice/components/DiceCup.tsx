import type { DicePlayer, LiarsDicePhase, Bid } from '../types'
import { DiceFace } from './DiceFace'
import styles from '../styles.module.css'

interface Props {
  player: DicePlayer
  isMe: boolean
  phase: LiarsDicePhase
  currentBid: Bid | null
  palificoActive: boolean
  isCurrentPlayer: boolean
  revealDice?: number[]  // from RevealSnapshot during Reveal phase
}

export function DiceCup({
  player,
  isMe,
  phase,
  currentBid,
  palificoActive,
  isCurrentPlayer,
  revealDice,
}: Props) {
  const showAllDice = isMe || phase === 'Reveal' || phase === 'Finished'

  // Determine which dice values to show
  const diceToShow: number[] = (() => {
    if (phase === 'Reveal' && revealDice !== undefined) return revealDice
    if (showAllDice) return player.dice
    return []
  })()

  // During Reveal, highlight dice matching the challenged bid face (or wilds if applicable)
  function isHighlighted(dieValue: number): boolean {
    if (phase !== 'Reveal' || currentBid === null) return false
    const bidFace = currentBid.face
    const isWild = !palificoActive && bidFace !== 1 && dieValue === 1
    return dieValue === bidFace || isWild
  }

  function isWildDie(dieValue: number): boolean {
    if (palificoActive) return false
    return dieValue === 1 && (currentBid === null || currentBid.face !== 1)
  }

  return (
    <div
      className={`${styles.cup} ${isCurrentPlayer ? styles.cupActive : ''} ${!player.active ? styles.cupEliminated : ''}`}
      aria-label={`${player.displayName}'s cup`}
    >
      <div className={styles.cupHeader}>
        <span className={styles.cupPlayerName}>
          {player.displayName}
          {isMe && <span className={styles.cupMeTag}> (you)</span>}
        </span>
        {!player.active && (
          <span className={styles.cupEliminatedTag}>eliminated</span>
        )}
        {isCurrentPlayer && player.active && (
          <span className={styles.cupTurnTag}>active</span>
        )}
      </div>

      <div className={`${styles.cupDice} ${phase === 'Reveal' ? styles.cupDiceRevealing : ''}`}>
        {showAllDice ? (
          diceToShow.map((dieValue, i) => {
            const value = dieValue as 1 | 2 | 3 | 4 | 5 | 6
            return (
              <DiceFace
                key={i}
                value={value}
                size="md"
                highlighted={isHighlighted(dieValue)}
                wild={isWildDie(dieValue)}
              />
            )
          })
        ) : (
          // Face-down dice: show diceCount outlines
          <div
            className={styles.cupHiddenDice}
            aria-label={`${player.diceCount} hidden dice`}
          >
            {Array.from({ length: player.diceCount }, (_, i) => (
              <div key={i} className={styles.cupHiddenDie} aria-hidden="true" />
            ))}
          </div>
        )}

        {player.diceCount === 0 && player.active === false && (
          <span className={styles.cupNoDice}>—</span>
        )}
      </div>

      <div className={styles.cupDiceCount}>
        {player.active
          ? `${player.diceCount} ${player.diceCount === 1 ? 'die' : 'dice'}`
          : 'eliminated'}
      </div>
    </div>
  )
}
