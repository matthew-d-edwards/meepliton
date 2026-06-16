import { useState, useRef, useEffect } from 'react'
import type { GameContext } from '@meepliton/contracts'
import type { HexEscapeState, HexEscapeAction, HexTileType, PlayerSlot } from '../types'
import { HexBoard } from './HexBoard'
import '../hexescape.css'
import styles from '../styles.module.css'

// Tile type display names
const TILE_LABELS: Record<HexTileType, string> = {
  Straight: 'Straight',
  Elbow:    'Elbow',
  Tee:      'Tee',
  Cross:    'Cross',
  Deadend:  'Dead End',
}

const TILE_TYPES: HexTileType[] = ['Straight', 'Elbow', 'Tee', 'Cross', 'Deadend']

type PickerMode = 'place' | 'rotate'

interface PendingAction {
  coord: string
  mode: PickerMode
  /** Only set for 'place' mode */
  tileType: HexTileType | null
  rotation: number
}

// ── Main Game component ───────────────────────────────────────────────────────

export default function Game({ state, myPlayerId, dispatch }: GameContext<HexEscapeState>) {
  const [pending, setPending] = useState<PendingAction | null>(null)

  // ── Derived state ──────────────────────────────────────────────────────────

  const me: PlayerSlot | undefined = state.players.find(p => p.id === myPlayerId)
  const myHasActed = me !== undefined && state.seatsActedThisRound.includes(me.seatIndex)
  const canAct = state.phase === 'Playing' && !myHasActed && me !== undefined

  // ── Dispatch helpers ───────────────────────────────────────────────────────

  function send(action: HexEscapeAction) {
    dispatch(action)
  }

  function handlePass() {
    if (!canAct) return
    setPending(null)
    send({ type: 'Pass' })
  }

  function handleCellClick(coord: string) {
    if (!canAct) return

    const cell = state.grid[coord]
    const isWall = state.walls.includes(coord)
    if (isWall) return

    if (cell) {
      // Placed non-fixed tile → offer rotate
      if (!cell.fixed) {
        setPending({ coord, mode: 'rotate', tileType: null, rotation: cell.rotation })
      }
      // Fixed tile → no interaction
    } else {
      // Empty cell → offer place
      // Start with first available tile type
      const firstAvailable = TILE_TYPES.find(t => (state.handCounts[t] ?? 0) > 0) ?? null
      setPending({ coord, mode: 'place', tileType: firstAvailable, rotation: 0 })
    }
  }

  function confirmAction() {
    if (!pending || !canAct) return

    if (pending.mode === 'place') {
      if (!pending.tileType) return
      send({ type: 'PlaceTile', coord: pending.coord, tileType: pending.tileType, rotation: pending.rotation })
    } else {
      send({ type: 'RotateTile', coord: pending.coord, rotation: pending.rotation })
    }
    setPending(null)
  }

  function cancelPicker() {
    setPending(null)
  }

  function setRotation(delta: number) {
    if (!pending) return
    const next = ((pending.rotation + delta) % 6 + 6) % 6
    setPending({ ...pending, rotation: next })
  }

  // ── Game Over screen ───────────────────────────────────────────────────────

  if (state.phase === 'GameOver') {
    const escaped = state.outcome === 'Escaped'
    return (
      <div data-game-theme="hexescape" className={styles.root}>
        <div role="alert" aria-atomic="true" className="sr-only">
          {escaped ? 'Escaped! All survivors reached the exit.' : 'Overrun! The threat counter maxed out.'}
        </div>
        <div className={styles.gameOverCard}>
          <h1
            className={[
              styles.gameOverTitle,
              escaped ? styles.gameOverTitleEscaped : styles.gameOverTitleOverrun,
            ].join(' ')}
          >
            {escaped ? 'Escaped!' : 'Overrun!'}
          </h1>
          <div className={styles.gameOverSub}>
            {escaped
              ? 'All survivors found a path to safety.'
              : 'The threat counter reached the threshold before survivors escaped.'}
          </div>
          <div className={styles.gameOverStats}>
            <div className={styles.gameOverStat}>
              <div className={styles.gameOverStatLabel}>Level</div>
              <div className={styles.gameOverStatValue} style={{ fontSize: '1rem' }}>{state.levelName}</div>
            </div>
            <div className={styles.gameOverStat}>
              <div className={styles.gameOverStatLabel}>Final Threat</div>
              <div className={styles.gameOverStatValue}>{state.threatCounter}</div>
            </div>
            <div className={styles.gameOverStat}>
              <div className={styles.gameOverStatLabel}>Survivors</div>
              <div className={styles.gameOverStatValue}>
                {state.connectedSurvivors}/{state.totalSurvivors}
              </div>
            </div>
          </div>
        </div>
      </div>
    )
  }

  // ── Playing screen ─────────────────────────────────────────────────────────

  const threatPct = Math.min(100, (state.threatCounter / state.threatThreshold) * 100)
  const threatDanger = state.threatCounter >= Math.floor(state.threatThreshold * 0.75)
  const allConnected = state.connectedSurvivors === state.totalSurvivors

  return (
    <div data-game-theme="hexescape" className={styles.root}>
      {/* Screen-reader live turn announcer */}
      <div aria-live="polite" aria-atomic="true" className="sr-only">
        {canAct
          ? 'Your turn — place or rotate a tile, or pass.'
          : myHasActed
          ? 'You have acted this round. Waiting for others.'
          : 'Waiting for your turn.'}
      </div>

      {/* ── Header strip ── */}
      <div className={styles.header}>
        <div className={styles.levelName}>{state.levelName}</div>
        <div className={styles.survivorRow} aria-label={`Survivors connected: ${state.connectedSurvivors} of ${state.totalSurvivors}${allConnected ? ' — all connected' : ''}`}>
          <span className={allConnected ? styles.survivorCountAll : styles.survivorCount}>
            {state.connectedSurvivors}/{state.totalSurvivors}
          </span>
          <span>survivors connected</span>
          {allConnected && (
            <span className={styles.survivorAllBadge} aria-hidden="true">all</span>
          )}
        </div>
        <div className={styles.threatWrap} aria-label={`Threat: ${state.threatCounter} of ${state.threatThreshold}${threatDanger ? ' — danger' : ''}`}>
          <span className={styles.threatLabel}>Threat</span>
          <div
            className={styles.threatBar}
            role="progressbar"
            aria-valuenow={state.threatCounter}
            aria-valuemin={0}
            aria-valuemax={state.threatThreshold}
            aria-label={`Threat level: ${state.threatCounter} of ${state.threatThreshold}${threatDanger ? ', danger' : ''}`}
          >
            <div
              className={[styles.threatFill, threatDanger ? styles.threatFillDanger : ''].filter(Boolean).join(' ')}
              style={{ width: `${threatPct}%` }}
            />
          </div>
          <span className={styles.threatCount}>
            {threatDanger && <span aria-hidden="true" className={styles.threatDangerIcon}>!</span>}
            {state.threatCounter}/{state.threatThreshold}
          </span>
        </div>
      </div>

      {/* ── Two-column layout ── */}
      <div className={styles.gameLayout}>
        <div className={styles.mainCol}>

          {/* Action bar */}
          <div className={styles.actionBar}>
            <span className={canAct ? styles.turnInfoYours : styles.turnInfo}>
              {canAct
                ? 'Your turn — click a cell to act'
                : myHasActed
                ? 'Waiting for others…'
                : `Waiting for ${me ? 'your turn' : 'a player'}…`}
            </span>
            <button
              className={styles.btnPass}
              onClick={handlePass}
              disabled={!canAct}
              aria-label="Pass your turn this round"
            >
              Pass
            </button>
          </div>

          {/* Board */}
          <HexBoard
            state={state}
            onCellClick={handleCellClick}
            canInteract={canAct}
          />

        </div>

        {/* ── Sidebar ── */}
        <div className={styles.sideCol}>

          {/* Tile hand counts */}
          <div className={styles.sideSection} aria-label="Tile hand">
            <div className={styles.sideTitle}>Tile Hand</div>
            <div className={styles.handGrid}>
              {TILE_TYPES.map(t => {
                const count = state.handCounts[t] ?? 0
                return (
                  <div key={t} className={styles.handItem} aria-label={`${TILE_LABELS[t]}: ${count} remaining`}>
                    <div className={styles.handItemType}>{TILE_LABELS[t]}</div>
                    <div className={count === 0 ? `${styles.handItemCount} ${styles.handItemCountZero}` : styles.handItemCount}>
                      {count}
                    </div>
                  </div>
                )
              })}
            </div>
          </div>

          {/* Players */}
          <div className={styles.sideSection} aria-label="Players">
            <div className={styles.sideTitle}>Players</div>
            {state.players.map(p => {
              const hasActed = state.seatsActedThisRound.includes(p.seatIndex)
              const isMe = p.id === myPlayerId
              return (
                <PlayerRow
                  key={p.id}
                  player={p}
                  hasActed={hasActed}
                  isMe={isMe}
                />
              )
            })}
          </div>

        </div>
      </div>

      {/* ── Tile picker modal ── */}
      {pending && canAct && (
        <TilePicker
          pending={pending}
          handCounts={state.handCounts}
          onSelectType={(t) => setPending({ ...pending, tileType: t })}
          onSetRotation={setRotation}
          onConfirm={confirmAction}
          onCancel={cancelPicker}
        />
      )}
    </div>
  )
}

// ── Player row ────────────────────────────────────────────────────────────────

interface PlayerRowProps {
  player: PlayerSlot
  hasActed: boolean
  isMe: boolean
}

function PlayerRow({ player, hasActed, isMe }: PlayerRowProps) {
  const rowClass = [
    styles.playerRow,
    hasActed ? styles.playerRowActed : styles.playerRowWaiting,
  ].filter(Boolean).join(' ')

  return (
    <div className={rowClass} aria-label={`${player.displayName}${isMe ? ' (you)' : ''}, ${hasActed ? 'acted' : 'waiting'}`}>
      {player.avatarUrl ? (
        <img
          src={player.avatarUrl}
          alt=""
          className={styles.playerAvatar}
          width={28}
          height={28}
        />
      ) : (
        <div className={styles.playerAvatar} aria-hidden="true">
          {player.displayName.charAt(0).toUpperCase()}
        </div>
      )}
      <span className={styles.playerName}>
        {player.displayName}
        {isMe && <span style={{ fontSize: '0.72rem', color: 'var(--text-muted)', fontWeight: 400 }}> (you)</span>}
      </span>
      <span className={hasActed ? styles.playerBadgeActed : styles.playerBadgePending}>
        {hasActed ? 'acted' : 'waiting'}
      </span>
    </div>
  )
}

// ── Tile picker modal ─────────────────────────────────────────────────────────

interface TilePickerProps {
  pending: PendingAction
  handCounts: Record<HexTileType, number>
  onSelectType: (t: HexTileType) => void
  onSetRotation: (delta: number) => void
  onConfirm: () => void
  onCancel: () => void
}

function TilePicker({ pending, handCounts, onSelectType, onSetRotation, onConfirm, onCancel }: TilePickerProps) {
  const isPlace = pending.mode === 'place'
  const canConfirm = isPlace ? pending.tileType !== null : true

  // Move focus into the dialog when it opens; return focus to the trigger on close.
  const cardRef = useRef<HTMLDivElement>(null)
  useEffect(() => {
    const firstFocusable = cardRef.current?.querySelector<HTMLElement>(
      'button:not(:disabled), [tabindex]:not([tabindex="-1"])'
    )
    firstFocusable?.focus()
  }, [])

  // Trap focus inside the dialog
  function handleKeyDown(e: React.KeyboardEvent<HTMLDivElement>) {
    if (e.key === 'Escape') {
      onCancel()
      return
    }
    if (e.key !== 'Tab') return
    const focusable = Array.from(
      cardRef.current?.querySelectorAll<HTMLElement>(
        'button:not(:disabled), [tabindex]:not([tabindex="-1"])'
      ) ?? []
    )
    if (focusable.length === 0) return
    const first = focusable[0]
    const last = focusable[focusable.length - 1]
    if (e.shiftKey) {
      if (document.activeElement === first) {
        e.preventDefault()
        last.focus()
      }
    } else {
      if (document.activeElement === last) {
        e.preventDefault()
        first.focus()
      }
    }
  }

  const titleId = `tile-picker-title-${pending.coord.replace(',', '-')}`

  return (
    <div
      className={styles.pickerOverlay}
      role="dialog"
      aria-modal="true"
      aria-labelledby={titleId}
      onClick={(e) => { if (e.target === e.currentTarget) onCancel() }}
      onKeyDown={handleKeyDown}
    >
      <div className={styles.pickerCard} ref={cardRef}>
        <div id={titleId} className={styles.pickerTitle}>
          {isPlace ? `Place Tile on ${pending.coord}` : `Rotate Tile on ${pending.coord}`}
        </div>

        {/* Tile type selector (place mode only) */}
        {isPlace && (
          <div className={styles.pickerGrid} role="group" aria-label="Tile type">
            {TILE_TYPES.map(t => {
              const count = handCounts[t] ?? 0
              const isAvailable = count > 0
              const isSelected = pending.tileType === t
              return (
                <button
                  key={t}
                  className={[
                    styles.tileBtn,
                    isSelected ? styles.tileBtnActive : '',
                  ].filter(Boolean).join(' ')}
                  onClick={() => onSelectType(t)}
                  disabled={!isAvailable}
                  aria-pressed={isSelected}
                  aria-label={`${TILE_LABELS[t]}, ${count} remaining`}
                >
                  <span>{TILE_LABELS[t]}</span>
                  <span className={styles.tileBtnCount}>{count} left</span>
                </button>
              )
            })}
          </div>
        )}

        {/* Rotation control */}
        <div className={styles.rotationWrap}>
          <span className={styles.rotLabel}>Rotation</span>
          <button
            className={styles.rotBtn}
            onClick={() => onSetRotation(-1)}
            aria-label="Rotate counter-clockwise"
          >
            &#8635;
          </button>
          <span className={styles.rotValue} aria-label={`Rotation: ${pending.rotation} of 5`}>
            {pending.rotation}
          </span>
          <button
            className={styles.rotBtn}
            onClick={() => onSetRotation(+1)}
            aria-label="Rotate clockwise"
          >
            &#8634;
          </button>
          <span style={{ fontSize: '0.62rem', color: 'var(--text-muted)' }}>× 60°</span>
        </div>

        {/* Actions */}
        <div className={styles.pickerActions}>
          <button className={styles.btnCancel} onClick={onCancel}>
            Cancel
          </button>
          <button
            className={styles.btnConfirm}
            onClick={onConfirm}
            disabled={!canConfirm}
          >
            {isPlace ? 'Place' : 'Rotate'}
          </button>
        </div>
      </div>
    </div>
  )
}
