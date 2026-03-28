import { useEffect, useState } from 'react'
import type { CSSProperties } from 'react'
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
  style?: CSSProperties
}

const MAX_DICE = 5

function LeatherCup({ isMe, isActive, isEliminated }: { isMe: boolean; isActive: boolean; isEliminated: boolean }) {
  const width = isMe ? 88 : 72
  const height = isMe ? 104 : 88
  // Cup path scaled: base is 72×88, me is 88×104
  const cx = width / 2
  const topY = height * (6 / 88)
  const bottomY = height * (80 / 88)
  const rimRx = width * (18 / 72)
  const rimRy = height * (5 / 88)
  const innerRx = width * (15 / 72)
  const innerRy = height * (3.5 / 88)
  const rivetY = height * (40 / 88)
  const rivetLx = width * (18 / 72)
  const rivetRx = width * (54 / 72)
  const seamX = width * (22 / 72)
  const seamCtrlX = width * (20 / 72)
  const seamEndX = width * (22 / 72)
  const seamTopY = height * (10 / 88)
  const seamMidY = height * (44 / 88)
  const seamEndY = height * (78 / 88)
  // Cup body path — trapezoid wider at top
  const bodyLeft  = width * (8 / 72)
  const bodyRight = width * (64 / 72)
  const bodyTopL  = width * (18 / 72)
  const bodyTopR  = width * (54 / 72)
  const cupPath = `M ${bodyLeft} ${bottomY} Q ${width * (6 / 72)} ${height * (20 / 88)} ${bodyTopL} ${topY} L ${bodyTopR} ${topY} Q ${width * (66 / 72)} ${height * (20 / 88)} ${bodyRight} ${bottomY} Z`

  const svgClassName = [
    styles.cupSvg,
    isActive && !isEliminated ? styles.cupSvgActive : '',
    isEliminated ? styles.cupSvgEliminated : '',
  ].filter(Boolean).join(' ')

  return (
    <svg
      width={width}
      height={height}
      viewBox={`0 0 ${width} ${height}`}
      aria-hidden="true"
      className={svgClassName}
    >
      <defs>
        <linearGradient id={`cup-body-${isMe ? 'me' : 'opp'}`} x1="0%" y1="0%" x2="100%" y2="0%">
          <stop offset="0%" stopColor="#2a1408" />
          <stop offset="30%" stopColor="#6b3518" />
          <stop offset="60%" stopColor="#4a2410" />
          <stop offset="100%" stopColor="#2a1408" />
        </linearGradient>
      </defs>
      {/* Cup body — trapezoid wider at top */}
      <path
        d={cupPath}
        fill={`url(#cup-body-${isMe ? 'me' : 'opp'})`}
      />
      {/* Rim ellipse */}
      <ellipse cx={cx} cy={topY} rx={rimRx} ry={rimRy}
        fill="#4a2410" stroke="#a06030" strokeWidth="1.5" />
      {/* Interior dark mouth */}
      <ellipse cx={cx} cy={topY} rx={innerRx} ry={innerRy} fill="#1a0a04" />
      {/* Brass rivets */}
      <circle cx={rivetLx} cy={rivetY} r={width * (2.5 / 72)} fill="#c8973a" />
      <circle cx={rivetRx} cy={rivetY} r={width * (2.5 / 72)} fill="#c8973a" />
      {/* Leather highlight seam */}
      <path
        d={`M ${seamX} ${seamTopY} Q ${seamCtrlX} ${seamMidY} ${seamEndX} ${seamEndY}`}
        stroke="#8a4a20" strokeWidth="1" fill="none" opacity="0.5"
      />
    </svg>
  )
}

function LifeTrack({ diceCount, active, prevDiceCount }: { diceCount: number; active: boolean; prevDiceCount: number }) {
  const [flashIndex, setFlashIndex] = useState<number | null>(null)

  useEffect(() => {
    // When a die is lost, flash the newly-dimmed pip
    if (prevDiceCount > diceCount && diceCount >= 0) {
      const lostIndex = diceCount // the index that just became dim
      setFlashIndex(lostIndex)
      const timer = setTimeout(() => setFlashIndex(null), 600)
      return () => clearTimeout(timer)
    }
  }, [diceCount, prevDiceCount])

  if (!active) {
    return (
      <div className={styles.lifeTrack} aria-hidden="true">
        {Array.from({ length: MAX_DICE }).map((_, i) => (
          <div key={i} className={`${styles.lifeTrackPip} ${styles.lifeTrackPipLost}`} />
        ))}
        <span className={styles.lifeTrackOut}>out</span>
      </div>
    )
  }

  return (
    <div className={styles.lifeTrack} aria-hidden="true">
      {Array.from({ length: MAX_DICE }).map((_, i) => {
        const isActive = i < diceCount
        const isFlashing = flashIndex === i
        const className = [
          styles.lifeTrackPip,
          !isActive ? styles.lifeTrackPipLost : '',
          isFlashing ? styles.lifeTrackPipFlash : '',
        ].filter(Boolean).join(' ')
        return <div key={i} className={className} />
      })}
    </div>
  )
}

export function DiceCup({
  player,
  isMe,
  phase,
  currentBid,
  palificoActive,
  isCurrentPlayer,
  revealDice,
  style,
}: Props) {
  // Track previous dice count to detect die loss for flash animation
  const [prevDiceCount, setPrevDiceCount] = useState(player.diceCount)
  useEffect(() => {
    if (player.diceCount !== prevDiceCount) {
      setPrevDiceCount(player.diceCount)
    }
  }, [player.diceCount])

  // Server projects per-player state: player.dice is already correct for this viewer.
  // During Reveal, prefer the snapshot dice if available (they are the same values but
  // kept here so the highlight logic works even if the round-end state arrives before
  // the reveal snapshot is cleared).
  const diceToShow: number[] = phase === 'Reveal' && revealDice !== undefined
    ? revealDice
    : player.dice

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

  // Show placeholder dice during Bidding when we don't know the dice values
  const showPlaceholders = diceToShow.length === 0 && player.active

  const rootClassName = [
    styles.cup,
    isCurrentPlayer ? styles.cupActive : '',
    !player.active ? styles.cupEliminated : '',
    !isMe ? styles.cupShimmer : '',
  ].filter(Boolean).join(' ')

  return (
    <div
      className={rootClassName}
      aria-label={`${player.displayName}'s cup`}
      style={style}
    >
      {/* Leather cup SVG illustration */}
      <LeatherCup
        isMe={isMe}
        isActive={isCurrentPlayer}
        isEliminated={!player.active}
      />

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
        {showPlaceholders
          ? Array.from({ length: player.diceCount }).map((_, i) => (
              <div key={i} className={styles.cupHiddenDie}>
                <span
                  style={{
                    display: 'flex',
                    alignItems: 'center',
                    justifyContent: 'center',
                    height: '100%',
                    color: 'var(--pirate-parchment-dim, #b8ab8a)',
                    fontSize: '1.1rem',
                  }}
                  aria-hidden="true"
                >
                  ?
                </span>
              </div>
            ))
          : diceToShow.map((dieValue, i) => {
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
        }

        {player.diceCount === 0 && player.active === false && (
          <span className={styles.cupNoDice}>—</span>
        )}
      </div>

      {/* Life track — replaces dice count text */}
      <LifeTrack
        diceCount={player.diceCount}
        active={player.active}
        prevDiceCount={prevDiceCount}
      />
    </div>
  )
}
