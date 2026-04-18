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

const ACTION_META = {
  TakeIncome:    { label: 'Income',      claimLabel: 'Income',            symbol: null,  charColor: null,        charBg: null,                   cost: 0, gainCoins: 1, targetRequired: false, consequence: 'Take 1 coin. Cannot be blocked or challenged.', tier: 'basic'     as const },
  TakeForeignAid:{ label: 'Foreign Aid', claimLabel: 'Foreign Aid',       symbol: null,  charColor: null,        charBg: null,                   cost: 0, gainCoins: 2, targetRequired: false, consequence: 'Take 2 coins. Blockable by Duke.',               tier: 'basic'     as const },
  TakeTax:       { label: 'Tax +3',      claimLabel: 'Claim Duke',        symbol: '♛',   charColor: '#c49de8',   charBg: 'rgba(74,29,122,0.5)',   cost: 0, gainCoins: 3, targetRequired: false, consequence: 'Take 3 coins. Claim Duke. Blockable by Duke.',   tier: 'character' as const },
  Assassinate:   { label: 'Assassinate', claimLabel: 'Claim Assassin',    symbol: '☽',   charColor: '#e87e7e',   charBg: 'rgba(130,20,20,0.5)',   cost: 3, gainCoins: 0, targetRequired: true,  consequence: 'Pay 3 coins. Target loses influence. Blockable by Contessa.', tier: 'character' as const },
  Steal:         { label: 'Steal',       claimLabel: 'Claim Captain',     symbol: '⚓',   charColor: '#7eaee8',   charBg: 'rgba(20,58,130,0.5)',   cost: 0, gainCoins: 2, targetRequired: true,  consequence: 'Take 2 coins from target. Blockable by Captain or Ambassador.', tier: 'character' as const },
  Exchange:      { label: 'Exchange',    claimLabel: 'Claim Ambassador',  symbol: '⚜',   charColor: '#7ee89e',   charBg: 'rgba(20,106,40,0.5)',   cost: 0, gainCoins: 0, targetRequired: false, consequence: 'Draw 2 cards from court deck, keep 2, return rest.', tier: 'character' as const },
  DoCoup:        { label: 'Coup',        claimLabel: 'Coup',              symbol: '☠',   charColor: '#e87e7e',   charBg: 'rgba(155,35,53,0.6)',   cost: 7, gainCoins: 0, targetRequired: true,  consequence: 'Pay 7 coins. Target loses influence. Cannot be blocked.', tier: 'coup' as const },
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
  isTargetable?: boolean
  isSelected?:   boolean
  onSelect?:     () => void
}

function PlayerCard({ player, isMe, isActiveTurn, isTargetable, isSelected, onSelect }: PlayerCardProps) {
  const cls = [
    styles.playerCard,
    isMe            ? styles.playerCardMe         : '',
    isActiveTurn    ? styles.playerCardActive      : '',
    !player.active  ? styles.playerCardEliminated  : '',
    isTargetable    ? styles.playerCardTargetable  : '',
    isSelected      ? styles.playerCardSelected    : '',
  ].filter(Boolean).join(' ')

  return (
    <div
      className={cls}
      aria-label={`${player.displayName}${isMe ? ' (you)' : ''}${!player.active ? ' — eliminated' : ''}${isTargetable ? ' — click to target' : ''}`}
      role={isTargetable ? 'button' : undefined}
      tabIndex={isTargetable ? 0 : undefined}
      onClick={isTargetable ? onSelect : undefined}
      onKeyDown={isTargetable ? (e) => { if (e.key === 'Enter' || e.key === ' ') onSelect?.() } : undefined}
    >
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

// ── Action card ───────────────────────────────────────────────────────────

interface ActionCardProps {
  actionType: keyof typeof ACTION_META
  playerCoins: number
  onActivate: () => void
  disabled?: boolean
}

function ActionCard({ actionType, playerCoins, onActivate, disabled }: ActionCardProps) {
  const meta = ACTION_META[actionType]
  const canAfford = playerCoins >= meta.cost
  const isDisabled = disabled || !canAfford

  const inlineStyle = meta.charColor
    ? ({ '--action-color': meta.charColor, '--action-bg': meta.charBg } as CSSProperties)
    : undefined

  const cardCls = [
    styles.actionCard,
    meta.tier === 'basic'     ? styles.actionCardBasic     : '',
    meta.tier === 'character' ? styles.actionCardCharacter : '',
    meta.tier === 'coup'      ? styles.actionCardCoup      : '',
    isDisabled                ? styles.actionCardDisabled  : '',
  ].filter(Boolean).join(' ')

  return (
    <button
      type="button"
      className={cardCls}
      style={inlineStyle}
      disabled={isDisabled}
      onClick={onActivate}
      aria-label={`${meta.claimLabel}${meta.cost > 0 ? `, costs ${meta.cost} coins` : ''}: ${meta.consequence}`}
    >
      {meta.symbol && <span className={styles.actionCardSymbol} aria-hidden="true">{meta.symbol}</span>}
      <span className={styles.actionCardLabel}>{meta.claimLabel}</span>
      {meta.tier !== 'basic' && (
        <span className={styles.actionCardSub}>{meta.label}{meta.gainCoins > 0 ? ` +${meta.gainCoins}` : ''}</span>
      )}
      {meta.cost > 0 && (
        <div className={styles.coinPips} aria-label={`Costs ${meta.cost} coins`}>
          {Array.from({ length: meta.cost }).map((_, i) => (
            <span key={i} className={`${styles.coinPip} ${canAfford ? styles.coinPipAfford : styles.coinPipCant}`} aria-hidden="true">◈</span>
          ))}
        </div>
      )}
      <span className={styles.actionCardConsequence}>{meta.consequence}</span>
    </button>
  )
}

// ── Main component ────────────────────────────────────────────────────────

export default function CoupGame({ state, myPlayerId, dispatch }: GameContext<CoupState>) {
  const [targetId, setTargetId] = useState<string>('')
  const [pendingAction, setPendingAction] = useState<string | null>(null)
  const [keepCards, setKeepCards] = useState<string[]>([])

  const me = state.players.find(p => p.id === myPlayerId)
  const isHost = state.players.some(p => p.id === myPlayerId && p.seatIndex === 0)
  const activePlayer = state.players[state.activePlayerIndex]
  const isMyTurn = activePlayer?.id === myPlayerId

  function send(action: CoupAction) {
    dispatch(action)
  }

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
            isTargetable={!!pendingAction && player.active && player.id !== myPlayerId}
            isSelected={player.id === targetId}
            onSelect={() => setTargetId(player.id)}
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
          {pendingAction ? (
            <div className={styles.targetSelectMode}>
              <div className={styles.actionPanelTitle}>
                Select a target for {ACTION_META[pendingAction as keyof typeof ACTION_META]?.label}
              </div>
              <p className={styles.targetSelectHint}>Click a player card above to select your target.</p>
              <div className={styles.targetConfirmRow}>
                <button type="button" className={`${styles.btn} ${styles.btnSecondary}`}
                  onClick={() => { setPendingAction(null); setTargetId('') }}>
                  ← Cancel
                </button>
                {targetId && (
                  <button
                    type="button"
                    className={`${styles.btn} ${styles.btnPrimary}`}
                    onClick={() => {
                      if (pendingAction === 'DoCoup')     send({ type: 'DoCoup',       targetId })
                      else if (pendingAction === 'Steal') send({ type: 'Steal',        targetId })
                      else if (pendingAction === 'Assassinate') send({ type: 'Assassinate', targetId })
                      setPendingAction(null)
                      setTargetId('')
                    }}
                  >
                    Confirm {ACTION_META[pendingAction as keyof typeof ACTION_META]?.label} → {state.players.find(p => p.id === targetId)?.displayName}
                  </button>
                )}
              </div>
            </div>
          ) : (
            <>
              <div className={styles.actionPanelTitle}>Your turn — choose an action</div>

              {me.coins >= 10 && (
                <div className={styles.coupWarning}>
                  You have {me.coins} coins — you must perform a Coup.
                </div>
              )}

              {me.coins < 10 && (
                <div className={styles.actionGrid}>
                  <ActionCard actionType="TakeTax"    playerCoins={me.coins} onActivate={() => send({ type: 'TakeTax' })} />
                  <ActionCard actionType="Exchange"   playerCoins={me.coins} onActivate={() => send({ type: 'Exchange' })} />
                  <ActionCard actionType="Steal"      playerCoins={me.coins} onActivate={() => setPendingAction('Steal')}      disabled={allOtherActive.length === 0} />
                  <ActionCard actionType="Assassinate" playerCoins={me.coins} onActivate={() => setPendingAction('Assassinate')} disabled={allOtherActive.length === 0} />
                </div>
              )}

              {me.coins < 10 && (
                <div className={styles.basicActionsRow}>
                  <ActionCard actionType="TakeIncome"     playerCoins={me.coins} onActivate={() => send({ type: 'TakeIncome' })} />
                  <ActionCard actionType="TakeForeignAid" playerCoins={me.coins} onActivate={() => send({ type: 'TakeForeignAid' })} />
                </div>
              )}

              <div className={[
                styles.coupZone,
                me.coins >= 7  ? styles.coupZoneReady    : '',
                me.coins >= 10 ? styles.coupZoneRequired : '',
              ].filter(Boolean).join(' ')}>
                <ActionCard
                  actionType="DoCoup"
                  playerCoins={me.coins}
                  onActivate={() => setPendingAction('DoCoup')}
                  disabled={allOtherActive.length === 0}
                />
              </div>
            </>
          )}
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
