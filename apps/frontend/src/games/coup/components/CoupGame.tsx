import { useState } from 'react'
import type { CSSProperties } from 'react'
import type { GameContext } from '@meepliton/contracts'
import type { CoupState, CoupAction, CoupPlayer } from '../types'
import '../coup.css'
import styles from '../styles.module.css'

// ── Character helpers ─────────────────────────────────────────────────────

const CHARACTERS = ['Duke', 'Assassin', 'Captain', 'Ambassador', 'Contessa']

const CHAR_DATA: Record<string, { symbol: string; bg: string; color: string; abbr: string }> = {
  Duke:       { symbol: '♛', bg: 'rgba(74,29,122,0.45)',   color: '#c49de8', abbr: 'D'  },
  Assassin:   { symbol: '☽', bg: 'rgba(130,20,20,0.45)',   color: '#e87e7e', abbr: 'A'  },
  Captain:    { symbol: '⚓', bg: 'rgba(20,58,130,0.45)',   color: '#7eaee8', abbr: 'C'  },
  Ambassador: { symbol: '⚜', bg: 'rgba(20,106,40,0.45)',   color: '#7ee89e', abbr: 'Am' },
  Contessa:   { symbol: '♥', bg: 'rgba(130,20,90,0.45)',   color: '#e87ec8', abbr: 'Co' },
}

// Which characters can block which actions
const BLOCK_MAP: Record<string, string[]> = {
  TakeForeignAid: ['Duke'],
  Assassinate:    ['Contessa'],
  Steal:          ['Captain', 'Ambassador'],
}

function actionLabel(actionType: string): string {
  switch (actionType) {
    case 'TakeIncome':    return 'Income (+1)'
    case 'TakeForeignAid': return 'Foreign Aid (+2)'
    case 'DoCoup':        return 'Coup (7 coins)'
    case 'TakeTax':       return 'Tax — Duke (+3)'
    case 'Assassinate':   return 'Assassinate — Assassin (3 coins)'
    case 'Steal':         return 'Steal — Captain'
    case 'Exchange':      return 'Exchange — Ambassador'
    default:              return actionType
  }
}

// ── Player card ───────────────────────────────────────────────────────────

interface PlayerCardProps {
  player:        CoupPlayer
  isMe:          boolean
  isActiveTurn:  boolean
}

function PlayerCard({ player, isMe, isActiveTurn }: PlayerCardProps) {
  const cls = [
    styles.playerCard,
    isMe ? styles.playerCardMe : '',
    isActiveTurn ? styles.playerCardActive : '',
    !player.active ? styles.playerCardEliminated : '',
  ].filter(Boolean).join(' ')

  return (
    <div className={cls} aria-label={`${player.displayName}${isMe ? ' (you)' : ''}${!player.active ? ' — eliminated' : ''}`}>
      <div className={styles.playerCardHeader}>
        <span className={styles.playerName}>
          {player.displayName}
          {isMe && <span className={styles.playerMeTag}> (you)</span>}
        </span>
        {isActiveTurn && player.active && (
          <span className={styles.playerTurnTag}>Turn</span>
        )}
      </div>

      <div className={styles.playerCoins}><span className={styles.coinIcon} aria-hidden="true">◈</span>{player.coins}</div>

      <div className={styles.influenceSlots}>
        {player.influence.map((card, i) => {
          const isHidden = !card.revealed && card.character === null
          const charData = card.character ? CHAR_DATA[card.character] : null
          const cls2 = [
            styles.influenceCard,
            card.revealed ? styles.influenceCardRevealed : '',
            !card.revealed && !isHidden ? styles.influenceCardOwn : '',
            isHidden ? styles.influenceCardHidden : '',
          ].filter(Boolean).join(' ')
          const inlineStyle = charData && !card.revealed && !isHidden
            ? ({ '--char-bg': charData.bg, '--char-color': charData.color } as CSSProperties)
            : undefined
          return (
            <div key={i} className={cls2} style={inlineStyle}>
              {isHidden ? (
                <div className={styles.cardBack}>
                  <div className={styles.cardBackInner} />
                </div>
              ) : card.revealed ? (
                <>
                  <span className={styles.cardCornerTL}>{charData?.abbr ?? '?'}</span>
                  <span className={styles.cardSymbol}>{charData?.symbol ?? '☽'}</span>
                  <span className={styles.cardCharName}>{card.character}</span>
                  <span className={styles.cardCornerBR}>{charData?.abbr ?? '?'}</span>
                </>
              ) : (
                <>
                  <span className={styles.cardCornerTL}>{charData?.abbr}</span>
                  <span className={styles.cardSymbol}>{charData?.symbol}</span>
                  <span className={styles.cardCharName}>{card.character}</span>
                  <span className={styles.cardCornerBR}>{charData?.abbr}</span>
                </>
              )}
            </div>
          )
        })}
        {player.influence.length === 0 && (
          <div className={`${styles.influenceCard} ${styles.influenceCardRevealed}`}>
            Eliminated
          </div>
        )}
      </div>
    </div>
  )
}

// ── Main component ────────────────────────────────────────────────────────

export default function CoupGame({ state, myPlayerId, dispatch }: GameContext<CoupState>) {
  const [targetId, setTargetId] = useState<string>('')
  const [keepCards, setKeepCards] = useState<string[]>([])

  const me = state.players.find(p => p.id === myPlayerId)
  const isHost = state.players.some(p => p.id === myPlayerId && p.seatIndex === 0)
  const activePlayer = state.players[state.activePlayerIndex]
  const isMyTurn = activePlayer?.id === myPlayerId

  function send(action: CoupAction) {
    dispatch(action)
  }

  const activePlayers = state.players.filter(p => p.active && p.id !== myPlayerId)
  const allOtherActive = state.players.filter(p => p.active && p.id !== myPlayerId)

  // Has my player already passed in response window?
  const iHavePassed = state.pending?.passedPlayers.includes(myPlayerId) ?? false
  const iAmBlocker = state.pending?.blockerId === myPlayerId

  // ── Influence loss ── (I need to choose a card to lose)
  const myInfluenceLoss = state.phase === 'InfluenceLoss' && state.pending?.influenceLossPlayerId === myPlayerId

  // ── Exchange ── (I'm the ambassador selecting cards)
  const myExchange = state.phase === 'Exchange' && activePlayer?.id === myPlayerId

  // Toggle exchange card selection
  function toggleExchangeCard(card: string) {
    setKeepCards(prev => {
      if (prev.includes(card)) return prev.filter(c => c !== card)
      if (prev.length >= 2) return prev
      return [...prev, card]
    })
  }

  // Determine which block characters I can claim
  const blockableChars = state.pending ? (BLOCK_MAP[state.pending.actionType] ?? []) : []
  const canBlock = blockableChars.length > 0 &&
    !isMyTurn &&
    state.phase === 'AwaitingResponses' &&
    state.pending?.step === 'ActionResponses' &&
    !iHavePassed &&
    // Only target or anyone for foreign aid
    (state.pending.actionType === 'TakeForeignAid' || state.pending.targetId === myPlayerId)

  const canChallenge = !isMyTurn &&
    state.phase === 'AwaitingResponses' &&
    !iHavePassed &&
    !iAmBlocker &&
    state.pending !== null &&
    (['TakeTax', 'Assassinate', 'Steal', 'Exchange'].includes(state.pending.actionType) ||
     state.pending.step === 'BlockResponses')

  const canPass = !isMyTurn &&
    state.phase === 'AwaitingResponses' &&
    !iHavePassed &&
    state.pending !== null

  // ── My unrevealed influence cards (for influence loss) ──
  const myUnrevealedCards = me?.influence.filter(c => !c.revealed) ?? []

  return (
    <div data-game-theme="inner-circle" className={styles.root}>

      {/* Header */}
      <div className={styles.gameTitle}>
        <span className={styles.gameTitleMain}>The Inner Circle</span>
      </div>
      {activePlayer && (
        <div className={styles.headerInfo}>
          {isMyTurn ? 'Your turn' : `${activePlayer.displayName}'s turn`}
        </div>
      )}

      {/* ── Finished ── */}
      {state.phase === 'Finished' && state.winner !== null && (
        <div className={styles.winnerBanner}>
          <span className={styles.winnerTitle}>The winner</span>
          <span className={styles.winnerName}>
            {state.players.find(p => p.id === state.winner)?.displayName ?? state.winner}
          </span>
        </div>
      )}

      {/* Players grid */}
      <div className={styles.playersGrid}>
        {state.players.map(player => (
          <PlayerCard
            key={player.id}
            player={player}
            isMe={player.id === myPlayerId}
            isActiveTurn={player.id === activePlayer?.id}
          />
        ))}
      </div>

      {/* ── Pending action description ── */}
      {state.pending !== null && (
        <div className={styles.statusBanner}>
          {state.pending.blockerId
            ? `${state.players.find(p => p.id === state.pending!.blockerId)?.displayName} is blocking with ${state.pending.actionType === 'Assassinate' ? 'Contessa' : '…'}`
            : `${state.players.find(p => p.id === state.pending!.actorId)?.displayName}: ${actionLabel(state.pending.actionType)}`
          }
          {state.pending.targetId && ` → ${state.players.find(p => p.id === state.pending!.targetId)?.displayName}`}
        </div>
      )}

      {/* ── My turn: action panel ── */}
      {state.phase === 'AwaitingResponses' && isMyTurn && state.pending === null && me?.active && (
        <div className={styles.actionPanel}>
          <div className={styles.actionPanelTitle}>Your turn — choose an action</div>

          {me.coins >= 10 && (
            <div className={styles.coupWarning}>
              You have {me.coins} coins — you must perform a Coup.
            </div>
          )}

          {/* Target selector for targeted actions */}
          {activePlayers.length > 0 && (
            <div className={styles.targetSection}>
              <label htmlFor="target-select" className={styles.targetLabel}>Target player</label>
              <select
                id="target-select"
                className={styles.targetSelect}
                value={targetId}
                onChange={e => setTargetId(e.target.value)}
              >
                <option value="">— select target —</option>
                {allOtherActive.map(p => (
                  <option key={p.id} value={p.id}>{p.displayName}</option>
                ))}
              </select>
            </div>
          )}

          <div className={styles.actionButtons}>
            {me.coins < 10 && (
              <>
                <button type="button" className={`${styles.btn} ${styles.btnSecondary}`}
                  onClick={() => send({ type: 'TakeIncome' })}>
                  Income (+1)
                </button>
                <button type="button" className={`${styles.btn} ${styles.btnSecondary}`}
                  onClick={() => send({ type: 'TakeForeignAid' })}>
                  Foreign Aid (+2)
                </button>
                <button type="button" className={`${styles.btn} ${styles.btnSecondary}`}
                  onClick={() => send({ type: 'TakeTax' })}>
                  Tax — Duke (+3)
                </button>
                <button type="button" className={`${styles.btn} ${styles.btnSecondary}`}
                  onClick={() => send({ type: 'Exchange' })}>
                  Exchange — Ambassador
                </button>
                <button type="button" className={`${styles.btn} ${styles.btnSecondary}`}
                  disabled={!targetId || me.coins < 3}
                  onClick={() => targetId && send({ type: 'Assassinate', targetId })}>
                  Assassinate — Assassin (3)
                </button>
                <button type="button" className={`${styles.btn} ${styles.btnSecondary}`}
                  disabled={!targetId}
                  onClick={() => targetId && send({ type: 'Steal', targetId })}>
                  Steal — Captain
                </button>
              </>
            )}
            <button
              type="button"
              className={`${styles.btn} ${styles.btnPrimary}`}
              disabled={!targetId || me.coins < 7}
              onClick={() => targetId && send({ type: 'DoCoup', targetId })}
            >
              Coup (7 coins){me.coins >= 10 ? ' ← required' : ''}
            </button>
          </div>
        </div>
      )}

      {/* ── Waiting (my turn, action already declared) ── */}
      {state.phase === 'AwaitingResponses' && isMyTurn && state.pending !== null && (
        <div className={`${styles.statusBanner} ${styles.statusBannerActive}`}>
          Waiting for other players to respond…
        </div>
      )}

      {/* ── Response panel (other players' turn) ── */}
      {state.phase === 'AwaitingResponses' && !isMyTurn && state.pending !== null && me?.active && !iHavePassed && !iAmBlocker && (
        <div className={styles.responsePanel}>
          <div className={styles.responsePanelTitle}>Respond</div>
          <div className={styles.responsePendingDesc}>
            {state.players.find(p => p.id === state.pending!.actorId)?.displayName}{' '}
            {state.pending.step === 'BlockResponses'
              ? `is blocking with ${blockableChars[0] ?? 'a character'}`
              : `is claiming ${actionLabel(state.pending.actionType)}`}
          </div>
          <div className={styles.responseButtons}>
            {canChallenge && (
              <button type="button" className={`${styles.btn} ${styles.btnDanger}`}
                onClick={() => send({ type: 'Challenge' })}>
                Challenge
              </button>
            )}
            {canBlock && blockableChars.map(char => (
              <button key={char} type="button" className={`${styles.btn} ${styles.btnSecondary}`}
                onClick={() => send({ type: 'Block', character: char })}>
                Block with {char}
              </button>
            ))}
            {canPass && (
              <button type="button" className={`${styles.btn} ${styles.btnSecondary}`}
                onClick={() => send({ type: 'Pass' })}>
                Pass
              </button>
            )}
          </div>
        </div>
      )}

      {iHavePassed && state.phase === 'AwaitingResponses' && (
        <div className={styles.statusBanner}>
          You passed — waiting for others…
        </div>
      )}

      {/* ── Influence loss ── */}
      {myInfluenceLoss && (
        <div className={styles.influenceLossPanel}>
          <div className={styles.influenceLossTitle}>Choose a card to reveal (lose influence)</div>
          <div className={styles.influenceLossCards}>
            {myUnrevealedCards.map((card, i) => (
              <button
                key={i}
                type="button"
                className={`${styles.btn} ${styles.btnDanger}`}
                onClick={() => card.character && send({ type: 'LoseInfluence', influenceToLose: card.character })}
              >
                Reveal {card.character}
              </button>
            ))}
          </div>
        </div>
      )}

      {/* ── Ambassador exchange ── */}
      {myExchange && state.pending?.exchangeOptions && (
        <div className={styles.exchangePanel}>
          <div className={styles.exchangeTitle}>Choose 2 cards to keep</div>
          <div className={styles.exchangeCards}>
            {state.pending.exchangeOptions.map((card, i) => (
              <div
                key={i}
                role="checkbox"
                aria-checked={keepCards.includes(card)}
                tabIndex={0}
                className={[styles.exchangeCard, keepCards.includes(card) ? styles.exchangeCardSelected : ''].filter(Boolean).join(' ')}
                onClick={() => toggleExchangeCard(card)}
                onKeyDown={e => e.key === 'Enter' && toggleExchangeCard(card)}
              >
                {card}
              </div>
            ))}
          </div>
          <div className={styles.actionRow}>
            <button
              type="button"
              className={`${styles.btn} ${styles.btnPrimary}`}
              disabled={keepCards.length !== 2}
              onClick={() => send({ type: 'ChooseExchange', keepCards })}
            >
              Keep selected ({keepCards.length}/2)
            </button>
          </div>
        </div>
      )}
    </div>
  )
}
