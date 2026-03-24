import { useState } from 'react'
import type { GameContext } from '@meepliton/contracts'
import type { DeadMansSwitchState, DeadMansSwitchAction, DevicePlayer } from '../types'
import '../deadmansswitch.css'
import styles from '../styles.module.css'

export default function DeadMansSwitchGame({ state, myPlayerId, dispatch }: GameContext<DeadMansSwitchState>) {
  const currentPlayer = state.players[state.currentPlayerIndex]
  const isMyTurn = currentPlayer?.id === myPlayerId
  const me = state.players.find(p => p.id === myPlayerId)
  const isChallenger = state.challengerId === myPlayerId

  function send(action: DeadMansSwitchAction) {
    dispatch(action)
  }

  // Compute face-down count for a player: stackCount minus flipped discs
  function faceDownCount(player: DevicePlayer): number {
    const flippedCount = player.stack.filter(d => d.flipped).length
    return player.stackCount - flippedCount
  }

  // Resolve display name for a player ID
  function nameFor(playerId: string): string {
    return state.players.find(p => p.id === playerId)?.displayName ?? playerId
  }

  return (
    <div data-game-theme="deadmansswitch" className={styles.root}>

      {/* ── Info Bar ── */}
      <div className={styles.infoBar}>
        <div className={styles.infoItem}>
          <span className={styles.infoLabel}>Phase</span>
          <span className={styles.infoValue}>{state.phase.toUpperCase()}</span>
        </div>
        <div className={styles.infoItem}>
          <span className={styles.infoLabel}>Mission</span>
          <span className={styles.infoValuePlain}>{state.roundNumber}</span>
        </div>
        {(state.phase === 'Bidding' || state.phase === 'Revealing') && (
          <div className={styles.infoItem}>
            <span className={styles.infoLabel}>Target Count</span>
            <span className={styles.infoValue}>{state.currentBid}</span>
          </div>
        )}
        <div className={styles.infoItem}>
          <span className={styles.infoLabel}>Devices on Table</span>
          <span className={styles.infoValuePlain}>{state.totalDiscsOnTable}</span>
        </div>
      </div>

      {/* ── Last Flip Notification ── */}
      {state.lastFlip !== null && (
        <div className={styles.flipNotification}>
          <span className={styles.flipNotificationIcon}>
            {state.lastFlip.result === 'Skull' ? '⚡' : '✓'}
          </span>
          <span>
            {nameFor(state.lastFlip.flippedByPlayerId)} flipped a{' '}
            <strong>{state.lastFlip.result === 'Skull' ? 'TRIGGER' : 'DUD'}</strong>
            {' '}from {nameFor(state.lastFlip.stackOwnerId)}&apos;s stack!
            {' '}(flip #{state.lastFlip.flipNumber})
          </span>
        </div>
      )}

      {/* ── Players Grid ── */}
      <div className={styles.playersGrid}>
        {state.players.map(player => {
          const isCurrentPlayer = player.id === currentPlayer?.id
          const faceDown = faceDownCount(player)
          const flippedDiscs = player.stack.filter(d => d.flipped)

          return (
            <div
              key={player.id}
              className={[
                styles.playerCard,
                isCurrentPlayer && player.active ? styles.playerCardActive : '',
                !player.active ? styles.playerCardEliminated : '',
              ].filter(Boolean).join(' ')}
            >
              {/* Header: name + badge */}
              <div className={styles.playerHeader}>
                <span className={styles.playerName}>
                  {player.displayName}
                  {player.id === myPlayerId && <span style={{ fontWeight: 400, color: 'var(--color-text-muted)' }}> (you)</span>}
                </span>
                {!player.active && (
                  <span className={`${styles.playerBadge} ${styles.badgeEliminated}`}>ELIMINATED</span>
                )}
                {player.active && isCurrentPlayer && (
                  <span className={`${styles.playerBadge} ${styles.badgeTurn}`}>ACTIVE</span>
                )}
                {player.active && player.passed && (
                  <span className={`${styles.playerBadge} ${styles.badgePassed}`}>PASSED</span>
                )}
              </div>

              {/* Points: ★ tokens */}
              <div className={styles.pointsRow}>
                {[0, 1].map(i => (
                  <span key={i} className={i < player.pointsWon ? styles.pointStar : styles.pointStarEmpty}>
                    ★
                  </span>
                ))}
              </div>

              {/* Device stack */}
              <div className={styles.stackArea}>
                <span className={styles.stackLabel}>Devices</span>
                <div className={styles.stackDiscs}>
                  {/* Face-down devices */}
                  {Array.from({ length: faceDown }).map((_, i) => (
                    <div key={`fd-${i}`} className={styles.discFaceDown} aria-label="Face-down device" />
                  ))}
                  {/* Flipped devices */}
                  {flippedDiscs.map((disc, i) => (
                    disc.type === 'Skull' ? (
                      <div key={`fl-${i}`} className={styles.discTrigger} aria-label="Trigger">TRG</div>
                    ) : (
                      <div key={`fl-${i}`} className={styles.discDud} aria-label="Dud">DUD</div>
                    )
                  ))}
                  {player.stackCount === 0 && (
                    <span className={styles.stackEmpty}>—</span>
                  )}
                </div>
              </div>
            </div>
          )
        })}
      </div>

      {/* ── Action Panel ── */}
      <ActionPanel
        state={state}
        me={me}
        myPlayerId={myPlayerId}
        isMyTurn={isMyTurn}
        isChallenger={isChallenger}
        send={send}
      />

      {/* ── Finished Banner ── */}
      {state.phase === 'Finished' && (
        <div className={styles.finishedBanner}>
          <div className={styles.finishedTitle}>Mission Complete</div>
          <div className={styles.finishedSubtitle}>
            {state.winner
              ? `${nameFor(state.winner)} defused the switch.`
              : 'The job is done.'}
          </div>
        </div>
      )}
    </div>
  )
}

interface ActionPanelProps {
  state: DeadMansSwitchState
  me: DevicePlayer | undefined
  myPlayerId: string
  isMyTurn: boolean
  isChallenger: boolean
  send: (action: DeadMansSwitchAction) => void
}

function ActionPanel({ state, me, myPlayerId, isMyTurn, isChallenger, send }: ActionPanelProps) {
  const [bidValue, setBidValue] = useState(state.currentBid + 1)
  const [targetValue, setTargetValue] = useState(1)

  if (state.phase === 'Finished') return null

  // Placing phase — my turn
  if (state.phase === 'Placing' && isMyTurn && me !== undefined) {
    return (
      <div className={styles.actionPanel}>
        <span className={styles.actionLabel}>Your move — Arming Phase</span>
        <div className={styles.actionRow}>
          <button
            type="button"
            className={`${styles.actionBtn} ${styles.actionBtnSecondary}`}
            onClick={() => send({ type: 'PlaceDisc' })}
          >
            ARM DEVICE
          </button>
        </div>
        {me.stackCount >= 1 && (
          <>
            <span className={styles.actionLabel}>— or commit to the job —</span>
            <div className={styles.actionRow}>
              <input
                type="number"
                className={styles.numberInput}
                value={targetValue}
                min={1}
                max={state.totalDiscsOnTable}
                onChange={e => setTargetValue(Number(e.target.value))}
                aria-label="Target count"
              />
              <button
                type="button"
                className={`${styles.actionBtn} ${styles.actionBtnPrimary}`}
                onClick={() => send({ type: 'StartBid', targetCount: targetValue })}
              >
                COMMIT TO JOB
              </button>
            </div>
          </>
        )}
      </div>
    )
  }

  // Bidding phase — my turn and not passed
  if (state.phase === 'Bidding' && isMyTurn && me !== undefined && !me.passed) {
    const minBid = state.currentBid + 1
    const effectiveBid = Math.max(bidValue, minBid)
    return (
      <div className={styles.actionPanel}>
        <span className={styles.actionLabel}>Your move — Bidding Phase</span>
        <div className={styles.actionRow}>
          <input
            type="number"
            className={styles.numberInput}
            value={effectiveBid}
            min={minBid}
            max={state.totalDiscsOnTable}
            onChange={e => setBidValue(Number(e.target.value))}
            aria-label="New bid"
          />
          <button
            type="button"
            className={`${styles.actionBtn} ${styles.actionBtnPrimary}`}
            onClick={() => send({ type: 'RaiseBid', newBid: effectiveBid })}
          >
            RAISE BID
          </button>
          <button
            type="button"
            className={`${styles.actionBtn} ${styles.actionBtnSecondary}`}
            onClick={() => send({ type: 'Pass' })}
          >
            PASS
          </button>
        </div>
      </div>
    )
  }

  // Revealing phase — I am Challenger
  if (state.phase === 'Revealing' && isChallenger && me !== undefined) {
    const myUnflipped = me.stack.filter(d => !d.flipped)
    const hasOwnUnflipped = myUnflipped.length > 0

    return (
      <div className={styles.actionPanel}>
        <span className={styles.actionLabel}>Revealing — Flip Devices</span>
        <div className={styles.actionRow}>
          {hasOwnUnflipped && (
            <button
              type="button"
              className={`${styles.actionBtn} ${styles.actionBtnPrimary}`}
              onClick={() => send({ type: 'FlipDisc', targetPlayerId: myPlayerId })}
            >
              FLIP OWN DEVICE
            </button>
          )}
          {!hasOwnUnflipped && state.players
            .filter(p => p.active && p.id !== myPlayerId && p.stackCount > 0)
            .map(p => (
              <button
                key={p.id}
                type="button"
                className={`${styles.actionBtn} ${styles.actionBtnSecondary}`}
                onClick={() => send({ type: 'FlipDisc', targetPlayerId: p.id })}
              >
                FLIP {p.displayName.toUpperCase()}&apos;S DEVICE
              </button>
            ))}
        </div>
      </div>
    )
  }

  // DiscardChoice phase — I am Challenger
  if (state.phase === 'DiscardChoice' && isChallenger && me !== undefined) {
    return (
      <div className={styles.actionPanel}>
        <span className={styles.actionLabel}>Discard a Device Permanently</span>
        <div className={styles.actionRow}>
          <button
            type="button"
            className={`${styles.actionBtn} ${styles.actionBtnSecondary}`}
            disabled={me.rosesOwned === 0}
            onClick={() => send({ type: 'DiscardDisc', discType: 'Rose' })}
            title={me.rosesOwned === 0 ? 'No duds to discard' : undefined}
          >
            DISCARD DUD (ROSE)
          </button>
          <button
            type="button"
            className={`${styles.actionBtn} ${styles.actionBtnDanger}`}
            disabled={!me.skullOwned}
            onClick={() => send({ type: 'DiscardDisc', discType: 'Skull' })}
            title={!me.skullOwned ? 'No trigger to discard' : undefined}
          >
            DISCARD TRIGGER (SKULL)
          </button>
        </div>
      </div>
    )
  }

  // RoundOver phase — any active player
  if (state.phase === 'RoundOver' && me !== undefined && me.active) {
    return (
      <div className={styles.actionPanel}>
        <span className={styles.actionLabel}>Round Over</span>
        <div className={styles.actionRow}>
          <button
            type="button"
            className={`${styles.actionBtn} ${styles.actionBtnPrimary}`}
            onClick={() => send({ type: 'StartNextRound' })}
          >
            START NEXT MISSION
          </button>
        </div>
      </div>
    )
  }

  return null
}
