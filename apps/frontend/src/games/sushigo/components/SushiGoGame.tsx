import { useState } from 'react'
import type { GameContext } from '@meepliton/contracts'
import type { SushiGoState, SushiGoAction, SushiGoPlayer } from '../types'
import '../sushigo.css'
import styles from '../styles.module.css'

// ── Card display helpers ───────────────────────────────────────────────────

const CARD_EMOJI: Record<string, string> = {
  Tempura:      '🍤',
  Sashimi:      '🐟',
  Dumpling:     '🥟',
  Maki1:        '🍣',
  Maki2:        '🍣',
  Maki3:        '🍣',
  SalmonNigiri: '🐠',
  SquidNigiri:  '🦑',
  EggNigiri:    '🥚',
  Pudding:      '🍮',
  Wasabi:       '🥬',
  Chopsticks:   '🥢',
}

const CARD_LABEL: Record<string, string> = {
  Tempura:      'Tempura',
  Sashimi:      'Sashimi',
  Dumpling:     'Dumpling',
  Maki1:        'Maki ×1',
  Maki2:        'Maki ×2',
  Maki3:        'Maki ×3',
  SalmonNigiri: 'Salmon',
  SquidNigiri:  'Squid',
  EggNigiri:    'Egg',
  Pudding:      'Pudding',
  Wasabi:       'Wasabi',
  Chopsticks:   'Chopsticks',
}

function cardEmoji(card: string): string {
  return CARD_EMOJI[card] ?? '🍱'
}

function cardLabel(card: string): string {
  return CARD_LABEL[card] ?? card
}

// ── Tableau chip ───────────────────────────────────────────────────────────

interface TableauChipProps {
  card: string
}

function TableauChip({ card }: TableauChipProps) {
  return (
    <span className={styles.tableauChip}>
      <span>{cardEmoji(card)}</span>
      <span>{cardLabel(card)}</span>
    </span>
  )
}

// ── Player card (opponent view) ────────────────────────────────────────────

interface PlayerCardProps {
  player:    SushiGoPlayer
  isMe:      boolean
  handSize:  number
  phase:     SushiGoState['phase']
}

function PlayerCard({ player, isMe, handSize, phase }: PlayerCardProps) {
  const cardClasses = [
    styles.playerCard,
    isMe ? styles.playerCardMe : '',
    player.hasPicked && phase === 'Picking' ? styles.playerCardPicked : '',
  ].filter(Boolean).join(' ')

  const totalScore = player.roundScores.reduce((a, b) => a + b, 0)

  return (
    <div className={cardClasses}>
      <div className={styles.playerCardHeader}>
        <span className={styles.playerName}>
          {player.displayName}
          {isMe && <span className={styles.playerMeTag}> (you)</span>}
        </span>
        {phase === 'Picking' && (
          player.hasPicked
            ? <span className={styles.playerPickedTag}>Picked</span>
            : <span className={styles.playerWaitingTag}>Waiting…</span>
        )}
      </div>

      <div className={styles.playerStats}>
        <div className={styles.playerStat}>
          <span className={styles.playerStatLabel}>Score</span>
          <span className={styles.playerStatValue}>{totalScore}</span>
        </div>
        <div className={styles.playerStat}>
          <span className={styles.playerStatLabel}>Pudding</span>
          <span className={styles.playerStatValue}>🍮 {player.puddingCount}</span>
        </div>
        {!isMe && (
          <div className={styles.playerStat}>
            <span className={styles.playerStatLabel}>Hand</span>
            <div className={styles.handSizeRow}>
              {Array.from({ length: handSize }).map((_, i) => (
                <div key={i} className={styles.handSizePip} aria-hidden="true" />
              ))}
            </div>
          </div>
        )}
      </div>

      <div className={styles.tableau}>
        {player.tableau.length === 0
          ? <span className={styles.tableauEmpty}>No cards yet</span>
          : player.tableau.map((card, i) => (
              <TableauChip key={`${card}-${i}`} card={card} />
            ))
        }
      </div>
    </div>
  )
}

// ── Card button (own hand) ─────────────────────────────────────────────────

interface CardBtnProps {
  card:       string
  selected:   boolean
  disabled:   boolean
  onClick:    () => void
}

function CardBtn({ card, selected, disabled, onClick }: CardBtnProps) {
  const cls = [
    styles.cardBtn,
    selected ? styles.cardBtnSelected : '',
  ].filter(Boolean).join(' ')

  return (
    <button
      type="button"
      className={cls}
      disabled={disabled}
      onClick={onClick}
      aria-pressed={selected}
    >
      <span className={styles.cardBtnEmoji}>{cardEmoji(card)}</span>
      <span className={styles.cardBtnLabel}>{cardLabel(card)}</span>
    </button>
  )
}

// ── Scores panel ───────────────────────────────────────────────────────────

interface ScoresPanelProps {
  players:    SushiGoPlayer[]
  myPlayerId: string
  round:      number
}

function ScoresPanel({ players, myPlayerId, round }: ScoresPanelProps) {
  const sorted = [...players].sort((a, b) => {
    const aTotal = a.roundScores.reduce((s, x) => s + x, 0)
    const bTotal = b.roundScores.reduce((s, x) => s + x, 0)
    return bTotal - aTotal
  })

  return (
    <div className={styles.scoresPanel}>
      <span className={styles.scoresPanelTitle}>Scores after round {round}</span>
      <div className={styles.scoresTable}>
        {sorted.map(p => {
          const total = p.roundScores.reduce((s, x) => s + x, 0)
          const isMe = p.id === myPlayerId
          return (
            <div
              key={p.id}
              className={[styles.scoresRow, isMe ? styles.scoresRowMe : ''].filter(Boolean).join(' ')}
            >
              <span className={styles.scoresRowName}>
                {p.displayName}{isMe ? ' (you)' : ''}
              </span>
              <div className={styles.scoresRowRounds}>
                {p.roundScores.map((s, i) => (
                  <span key={i} className={styles.scoresRowRoundBadge}>R{i + 1}: {s}</span>
                ))}
              </div>
              <span className={styles.scoresRowTotal}>{total}</span>
            </div>
          )
        })}
      </div>
    </div>
  )
}

// ── Main game component ────────────────────────────────────────────────────

export default function SushiGoGame({ state, myPlayerId, dispatch }: GameContext<SushiGoState>) {
  const [selectedCard, setSelectedCard] = useState<string | null>(null)
  const [chopsticksCard2, setChopsticksCard2] = useState<string | null>(null)
  const [chopsticksMode, setChopsticksMode] = useState(false)

  const me = state.players.find(p => p.id === myPlayerId)
  const isHost = state.players.some(p => p.id === myPlayerId && p.seatIndex === 0)
  const myHand: string[] = me !== undefined
    ? (state.hands[me.seatIndex] ?? [])
    : []

  const hasChopsticksInTableau = me?.tableau.includes('Chopsticks') ?? false
  const alreadyPicked = me?.hasPicked ?? false

  function send(action: SushiGoAction) {
    dispatch(action)
  }

  function handleCardClick(card: string) {
    if (chopsticksMode) {
      if (chopsticksCard2 === card) {
        setChopsticksCard2(null)
      } else {
        setChopsticksCard2(card)
      }
      return
    }
    setSelectedCard(prev => prev === card ? null : card)
  }

  function handlePickCard() {
    if (selectedCard === null) return
    send({ type: 'PickCard', pick: selectedCard })
    setSelectedCard(null)
    setChopsticksMode(false)
    setChopsticksCard2(null)
  }

  function handleUseChopsticks() {
    if (selectedCard === null || chopsticksCard2 === null) return
    send({ type: 'UseChopsticks', pick: selectedCard, pick2: chopsticksCard2 })
    setSelectedCard(null)
    setChopsticksCard2(null)
    setChopsticksMode(false)
  }

  function toggleChopsticksMode() {
    setChopsticksMode(prev => !prev)
    setChopsticksCard2(null)
  }

  const handSizes = state.handSizes ?? state.players.map(() => 0)

  return (
    <div data-game-theme="sushi-train" className={styles.root}>

      {/* Header */}
      <div className={styles.header}>
        <span className={styles.headerTitle}>The Sushi Train</span>
        {state.phase !== 'Waiting' && (
          <span className={styles.headerRound}>
            Round {state.round} of 3 · Turn {state.turn}
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
          {isHost && (
            <div className={styles.actionRow}>
              <button
                type="button"
                className={`${styles.actionBtn} ${styles.actionBtnPrimary}`}
                onClick={() => send({ type: 'StartGame' })}
              >
                Start Game
              </button>
            </div>
          )}
          {!isHost && (
            <div className={styles.waitingSubtitle}>Waiting for the host to start…</div>
          )}
        </div>
      )}

      {/* ── Picking / Revealing ── */}
      {(state.phase === 'Picking' || state.phase === 'Revealing') && (
        <>
          <div className={[styles.phaseBanner, state.phase === 'Picking' ? styles.phaseBannerActive : ''].filter(Boolean).join(' ')}>
            {state.phase === 'Picking' ? '🍣 Pick a card' : '✨ Revealing…'}
          </div>

          {/* Players grid */}
          <div className={styles.playersGrid}>
            {state.players.map(player => (
              <PlayerCard
                key={player.id}
                player={player}
                isMe={player.id === myPlayerId}
                handSize={handSizes[player.seatIndex] ?? 0}
                phase={state.phase}
              />
            ))}
          </div>

          {/* Own hand — only show during Picking and when not yet picked */}
          {state.phase === 'Picking' && !alreadyPicked && myHand.length > 0 && (
            <div className={styles.handSection}>
              <span className={styles.handLabel}>Your hand — pick one card</span>
              <div className={styles.handCards}>
                {myHand.map((card, i) => {
                  const isFirstCard = selectedCard === card && !chopsticksMode
                  const isChopsCard2 = chopsticksMode && chopsticksCard2 === card
                  return (
                    <CardBtn
                      key={`${card}-${i}`}
                      card={card}
                      selected={isFirstCard || isChopsCard2}
                      disabled={false}
                      onClick={() => handleCardClick(card)}
                    />
                  )
                })}
              </div>

              {/* Chopsticks option */}
              {hasChopsticksInTableau && myHand.length >= 2 && (
                <div className={styles.chopsticksSection}>
                  <span className={styles.chopsticksLabel}>🥢 Use Chopsticks</span>
                  {!chopsticksMode ? (
                    <div className={styles.actionRow}>
                      <button
                        type="button"
                        className={`${styles.actionBtn} ${styles.actionBtnSecondary}`}
                        onClick={toggleChopsticksMode}
                      >
                        Use Chopsticks (pick 2)
                      </button>
                    </div>
                  ) : (
                    <>
                      <span className={styles.chopsticksHint}>
                        Select two cards from your hand: first pick is highlighted above, select second pick below.
                        Return your Chopsticks to the hand.
                      </span>
                      <div className={styles.actionRow}>
                        <button
                          type="button"
                          className={`${styles.actionBtn} ${styles.actionBtnSecondary}`}
                          onClick={toggleChopsticksMode}
                        >
                          Cancel
                        </button>
                        <button
                          type="button"
                          className={`${styles.actionBtn} ${styles.actionBtnPrimary}`}
                          disabled={selectedCard === null || chopsticksCard2 === null || selectedCard === chopsticksCard2}
                          onClick={handleUseChopsticks}
                        >
                          Confirm picks
                        </button>
                      </div>
                    </>
                  )}
                </div>
              )}

              {/* Normal pick confirm */}
              {!chopsticksMode && (
                <div className={styles.actionRow}>
                  <button
                    type="button"
                    className={`${styles.actionBtn} ${styles.actionBtnPrimary}`}
                    disabled={selectedCard === null}
                    onClick={handlePickCard}
                  >
                    Confirm pick
                  </button>
                </div>
              )}
            </div>
          )}

          {/* Already picked indicator */}
          {state.phase === 'Picking' && alreadyPicked && (
            <div className={styles.phaseBanner}>
              ✓ You have picked — waiting for others…
            </div>
          )}
        </>
      )}

      {/* ── Scoring ── */}
      {state.phase === 'Scoring' && (
        <>
          <div className={`${styles.phaseBanner} ${styles.phaseBannerActive}`}>
            Round {state.round} complete — scores
          </div>

          <ScoresPanel
            players={state.players}
            myPlayerId={myPlayerId}
            round={state.round}
          />

          {/* Players tableau summary */}
          <div className={styles.playersGrid}>
            {state.players.map(player => (
              <PlayerCard
                key={player.id}
                player={player}
                isMe={player.id === myPlayerId}
                handSize={0}
                phase={state.phase}
              />
            ))}
          </div>

          {isHost && state.round < 3 && (
            <div className={styles.actionRow}>
              <button
                type="button"
                className={`${styles.actionBtn} ${styles.actionBtnPrimary}`}
                onClick={() => send({ type: 'AdvanceRound' })}
              >
                Next Round →
              </button>
            </div>
          )}

          {isHost && state.round >= 3 && (
            <div className={styles.actionRow}>
              <button
                type="button"
                className={`${styles.actionBtn} ${styles.actionBtnPrimary}`}
                onClick={() => send({ type: 'AdvanceRound' })}
              >
                Final Scores →
              </button>
            </div>
          )}

          {!isHost && (
            <div className={styles.phaseBanner}>
              Waiting for host to advance…
            </div>
          )}
        </>
      )}

      {/* ── Finished ── */}
      {state.phase === 'Finished' && (
        <>
          {state.winner !== null && (
            <div className={styles.winnerBanner}>
              <span className={styles.winnerTitle}>Winner</span>
              <span className={styles.winnerName}>
                {state.players.find(p => p.id === state.winner)?.displayName ?? state.winner}
              </span>
            </div>
          )}

          <ScoresPanel
            players={state.players}
            myPlayerId={myPlayerId}
            round={3}
          />

          <div className={styles.playersGrid}>
            {state.players.map(player => (
              <PlayerCard
                key={player.id}
                player={player}
                isMe={player.id === myPlayerId}
                handSize={0}
                phase={state.phase}
              />
            ))}
          </div>
        </>
      )}
    </div>
  )
}
