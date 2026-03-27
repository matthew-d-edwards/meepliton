import { useState } from 'react'
import type { GameContext } from '@meepliton/contracts'
import type { LoveLetterState, LoveLetterAction, LoveLetterPlayer } from '../types'
import '../loveletter.css'
import styles from '../styles.module.css'

// ── Card constants ────────────────────────────────────────────────────────

const CARD_VALUE: Record<string, number> = {
  Guard: 1, Priest: 2, Baron: 3, Handmaid: 4,
  Prince: 5, King: 6, Countess: 7, Princess: 8,
}

const CARD_DESC: Record<string, string> = {
  Guard:    'Name a character — if correct, eliminate target',
  Priest:   'Look at another player\'s hand card',
  Baron:    'Compare hands — lower value is eliminated',
  Handmaid: 'Protection until your next turn',
  Prince:   'Force any player to discard and draw',
  King:     'Swap hands with another player',
  Countess: 'Must play if holding King or Prince',
  Princess: 'Eliminated if discarded or played',
}

const ALL_CARDS = Object.keys(CARD_VALUE)
const NON_GUARD_CARDS = ALL_CARDS.filter(c => c !== 'Guard')

// Targeting requirements
const NEEDS_TARGET = new Set(['Guard', 'Priest', 'Baron', 'Prince', 'King'])
const NEEDS_GUESS  = new Set(['Guard'])
// Prince can target self; others cannot target self
const CAN_TARGET_SELF = new Set(['Prince'])

// ── Player card component ─────────────────────────────────────────────────

interface PlayerCardProps {
  player:           LoveLetterPlayer
  isMe:             boolean
  isCurrentTurn:    boolean
  tokenTarget:      number
}

function PlayerCard({ player, isMe, isCurrentTurn, tokenTarget }: PlayerCardProps) {
  const cls = [
    styles.playerCard,
    isMe ? styles.playerCardMe : '',
    isCurrentTurn ? styles.playerCardCurrentTurn : '',
    !player.active ? styles.playerCardEliminated : '',
  ].filter(Boolean).join(' ')

  return (
    <div className={cls}>
      <div className={styles.playerCardHeader}>
        <span className={styles.playerName}>
          {player.displayName}
          {isMe && <span className={styles.playerMeTag}> (you)</span>}
        </span>
        <div style={{ display: 'flex', gap: 4 }}>
          {isCurrentTurn && player.active && <span className={styles.playerTurnTag}>Turn</span>}
          {!player.active && <span className={styles.playerEliminatedTag}>Out</span>}
          {player.handmaid && <span className={styles.playerHandmaidTag}>Protected</span>}
        </div>
      </div>

      {/* Token track */}
      <div className={styles.tokenRow} aria-label={`${player.tokens} of ${tokenTarget} tokens`}>
        {Array.from({ length: tokenTarget }).map((_, i) => (
          <div
            key={i}
            className={styles.token}
            style={{ opacity: i < player.tokens ? 0.9 : 0.2 }}
          />
        ))}
      </div>

      {/* Hand card */}
      <div className={isMe && player.handCard ? styles.handCardDisplay : `${styles.handCardDisplay} ${styles.handCardBack}`}>
        {isMe && player.handCard
          ? `${player.handCard} (${CARD_VALUE[player.handCard] ?? '?'})`
          : player.active ? 'Hidden' : '—'}
      </div>

      {/* Discard pile */}
      <div className={styles.discardPile}>
        {player.discardPile.length === 0
          ? <span className={styles.discardEmpty}>No discards</span>
          : player.discardPile.map((c, i) => (
              <span key={i} className={styles.discardChip}>{c}</span>
            ))
        }
      </div>
    </div>
  )
}

// ── Main component ────────────────────────────────────────────────────────

export default function LoveLetterGame({ state, myPlayerId, dispatch }: GameContext<LoveLetterState>) {
  const [selectedCard, setSelectedCard] = useState<string | null>(null)
  const [targetId, setTargetId]         = useState<string>('')
  const [guessedCard, setGuessedCard]   = useState<string>('')

  const me = state.players.find(p => p.id === myPlayerId)
  const isHost = state.players.some(p => p.id === myPlayerId && p.seatIndex === 0)
  const currentPlayer = state.players[state.currentPlayerIndex]
  const isMyTurn = currentPlayer?.id === myPlayerId

  const tokenTarget = state.players.length === 2 ? 7 : state.players.length === 3 ? 5 : 4

  function send(action: LoveLetterAction) {
    dispatch(action)
  }

  // Cards I can play this turn (own hand — after draw, backend gives me the updated handCard)
  // The backend handles the draw; the client just needs to show the card and let me play it
  // For Love Letter the player always has exactly 1 card until their turn, then backend gives them 2
  // We show the hand card and provide a play button. Since state only shows 1 card at a time,
  // the UI simply shows "Play [card]" when it's my turn.
  const myCard = me?.handCard ?? null

  // Valid targets: active, not handmaid-protected, not self (unless Prince)
  function validTargets(card: string): LoveLetterPlayer[] {
    const canSelf = CAN_TARGET_SELF.has(card)
    return state.players.filter(p => {
      if (!p.active) return false
      if (p.handmaid) return false
      if (!canSelf && p.id === myPlayerId) return false
      return true
    })
  }

  const targets = selectedCard ? validTargets(selectedCard) : []
  const needsTarget = selectedCard ? NEEDS_TARGET.has(selectedCard) : false
  const needsGuess  = selectedCard === 'Guard'

  const canPlay = selectedCard !== null &&
    (!needsTarget || (targets.length > 0 && targetId !== '')) &&
    (!needsGuess || guessedCard !== '')

  function handlePlay() {
    if (!selectedCard) return
    const action: LoveLetterAction = {
      type: 'PlayCard',
      cardPlayed: selectedCard,
      ...(targetId ? { targetId } : {}),
      ...(guessedCard ? { guessedCard } : {}),
    }
    send(action)
    setSelectedCard(null)
    setTargetId('')
    setGuessedCard('')
  }

  // Priest reveal modal — only shown to the viewer
  const priestReveal = state.pendingPriestReveal?.viewerId === myPlayerId
    ? state.pendingPriestReveal
    : null

  return (
    <div data-game-theme="affairs-of-the-court" className={styles.root}>

      {/* Priest reveal modal */}
      {priestReveal && (
        <div className={styles.priestModal} role="dialog" aria-modal="true" aria-label="Priest reveal">
          <div className={styles.priestModalBox}>
            <div className={styles.priestModalTitle}>You looked at their card</div>
            <div className={styles.priestModalCard}>{priestReveal.card}</div>
            <p style={{ fontSize: '0.85rem', color: 'var(--color-text-muted)' }}>
              {state.players.find(p => p.id === priestReveal.targetId)?.displayName} holds the {priestReveal.card}.
            </p>
            <div className={styles.actionRow}>
              <button
                type="button"
                className={`${styles.btn} ${styles.btnPrimary}`}
                onClick={() => send({ type: 'AcknowledgePriest' })}
              >
                Understood
              </button>
            </div>
          </div>
        </div>
      )}

      {/* Header */}
      <div className={styles.header}>
        <span className={styles.headerTitle}>Affairs of the Court</span>
        {state.phase !== 'Waiting' && (
          <span className={styles.headerInfo}>
            Round {state.round} · {state.deckSize} cards left
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

      {/* ── Game Over ── */}
      {state.phase === 'GameOver' && state.winner !== null && (
        <div className={styles.winnerBanner}>
          <span className={styles.winnerTitle}>Winner</span>
          <span className={styles.winnerName}>
            {state.players.find(p => p.id === state.winner)?.displayName ?? state.winner}
          </span>
        </div>
      )}

      {/* Players grid */}
      {state.phase !== 'Waiting' && (
        <div className={styles.playersGrid}>
          {state.players.map(player => (
            <PlayerCard
              key={player.id}
              player={player}
              isMe={player.id === myPlayerId}
              isCurrentTurn={player.id === currentPlayer?.id}
              tokenTarget={tokenTarget}
            />
          ))}
        </div>
      )}

      {/* 2-player face-up set aside cards */}
      {state.phase === 'Playing' && state.faceUpSetAside.length > 0 && (
        <div className={styles.setAsideSection}>
          <div className={styles.setAsideLabel}>Removed from play (known)</div>
          <div className={styles.setAsideCards}>
            {state.faceUpSetAside.map((c, i) => (
              <span key={i} className={styles.discardChip}>{c}</span>
            ))}
          </div>
        </div>
      )}

      {/* ── My turn — play a card ── */}
      {state.phase === 'Playing' && isMyTurn && me?.active && !priestReveal && (
        <div className={styles.actionPanel}>
          <div className={styles.actionPanelTitle}>Your turn — play a card</div>

          <div className={styles.cardSelector}>
            {myCard && (
              <button
                type="button"
                className={[styles.cardOption, selectedCard === myCard ? styles.cardOptionSelected : ''].filter(Boolean).join(' ')}
                onClick={() => { setSelectedCard(myCard); setTargetId(''); setGuessedCard(''); }}
              >
                <span className={styles.cardOptionValue}>{CARD_VALUE[myCard] ?? '?'}</span>
                <span className={styles.cardOptionName}>{myCard}</span>
                <span style={{ fontSize: '0.62rem', color: 'var(--color-text-muted)', textAlign: 'center', marginTop: 2 }}>
                  {CARD_DESC[myCard] ?? ''}
                </span>
              </button>
            )}
          </div>

          {/* Target selector */}
          {needsTarget && selectedCard && (
            <div className={styles.inputSection}>
              <label htmlFor="ll-target" className={styles.inputLabel}>Target player</label>
              <select id="ll-target" className={styles.inputSelect} value={targetId}
                onChange={e => { setTargetId(e.target.value); setGuessedCard(''); }}>
                <option value="">— select —</option>
                {targets.map(p => (
                  <option key={p.id} value={p.id}>{p.displayName}{p.id === myPlayerId ? ' (you)' : ''}</option>
                ))}
              </select>
              {targets.length === 0 && (
                <span style={{ fontSize: '0.78rem', color: 'var(--color-text-muted)' }}>
                  All other players are protected by Handmaid — no valid targets.
                </span>
              )}
            </div>
          )}

          {/* Card guess for Guard */}
          {needsGuess && targetId && (
            <div className={styles.inputSection}>
              <label htmlFor="ll-guess" className={styles.inputLabel}>Guess their card</label>
              <select id="ll-guess" className={styles.inputSelect} value={guessedCard}
                onChange={e => setGuessedCard(e.target.value)}>
                <option value="">— select —</option>
                {NON_GUARD_CARDS.map(c => (
                  <option key={c} value={c}>{c} ({CARD_VALUE[c]})</option>
                ))}
              </select>
            </div>
          )}

          <div className={styles.actionRow}>
            <button type="button" className={`${styles.btn} ${styles.btnPrimary}`}
              disabled={!canPlay || (needsTarget && targets.length === 0 && !CAN_TARGET_SELF.has(selectedCard ?? ''))}
              onClick={handlePlay}>
              Play {selectedCard ?? '…'}
            </button>
          </div>
        </div>
      )}

      {/* ── Waiting for others ── */}
      {state.phase === 'Playing' && !isMyTurn && (
        <div className={styles.statusBanner}>
          {currentPlayer?.displayName}'s turn…
        </div>
      )}

      {/* ── Round End ── */}
      {state.phase === 'RoundEnd' && state.lastRoundResult && (
        <div className={styles.roundEndPanel}>
          <div className={styles.roundEndTitle}>
            Round {state.round} ended — {state.lastRoundResult.reason === 'LastStanding' ? 'last standing' : 'highest card'}
          </div>
          <div className={styles.roundEndWinners}>
            {state.lastRoundResult.winnerIds
              .map(id => state.players.find(p => p.id === id)?.displayName ?? id)
              .join(' & ')} {state.lastRoundResult.winnerIds.length === 1 ? 'wins' : 'win'} the round!
          </div>

          {state.lastRoundResult.reveals.map(r => {
            const p = state.players.find(p => p.id === r.playerId)
            const isWinner = state.lastRoundResult!.winnerIds.includes(r.playerId)
            return (
              <div key={r.playerId} className={[styles.revealRow, isWinner ? styles.revealRowWinner : ''].filter(Boolean).join(' ')}>
                <span className={styles.revealRowName}>{p?.displayName ?? r.playerId}</span>
                {r.card && <span className={styles.revealRowCard}>{r.card} ({CARD_VALUE[r.card] ?? '?'})</span>}
              </div>
            )
          })}

          {isHost ? (
            <div className={styles.actionRow}>
              <button type="button" className={`${styles.btn} ${styles.btnPrimary}`}
                onClick={() => send({ type: 'StartNextRound' })}>
                Next Round →
              </button>
            </div>
          ) : (
            <div className={styles.statusBanner}>Waiting for host to start next round…</div>
          )}
        </div>
      )}
    </div>
  )
}
