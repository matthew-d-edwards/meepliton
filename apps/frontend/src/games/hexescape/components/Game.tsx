import { useState, useRef, useEffect } from 'react'
import type { GameContext } from '@meepliton/contracts'
import type {
  HexEscapeState,
  HexEscapeAction,
  HexTileType,
  HeldTile,
  ZombieRoll,
  CharacterState,
  PlayerSlot,
} from '../types'
import { HexBoard, openEdges, RoadTile } from './HexBoard'
import '../hexescape.css'
import styles from '../styles.module.css'

// ── Constants (must match HexEscapeConstants.cs) ───────────────────────────────

const MIN_ACTIONS_PER_TURN = 2
// Mirror of HexEscapeConstants.ApPoolSize — AP granted on claiming a turn, indexed by player count.
const AP_POOL_SIZE = [0, 5, 4, 4, 4, 4, 4]
const TILE_LABELS: Record<HexTileType, string> = {
  Straight: 'Straight',
  Elbow:    'Elbow',
  Tee:      'Tee',
  Cross:    'Cross',
  Deadend:  'Dead End',
}
const TILE_TYPES: HexTileType[] = ['Straight', 'Elbow', 'Tee', 'Cross', 'Deadend']

// ── Interaction modes ──────────────────────────────────────────────────────────

type PickerMode =
  | { kind: 'place'; coord: string; tileType: HexTileType | null; rotation: number }
  | { kind: 'placeZombie'; coord: string }
  | { kind: 'rotate'; coord: string; rotation: number }
  | { kind: 'move'; toCoord: string }

// ── Main Game component ────────────────────────────────────────────────────────

export default function Game({ state, myPlayerId, dispatch }: GameContext<HexEscapeState>) {
  const [picker, setPicker] = useState<PickerMode | null>(null)
  const [zombieAnimPhase, setZombieAnimPhase] = useState<'idle' | 'showing'>('idle')
  const [exitBannerVisible, setExitBannerVisible] = useState(false)

  // Trigger zombie animation when lastZombieRolls changes (round boundary).
  // The auto-dismiss timer only fires when the overlay is not focused — keyboard
  // and screen reader users must be able to dismiss manually via the Continue button
  // without the dialog closing under them.
  const prevRoundRef = useRef(state.roundNumber)
  const zombieOverlayFocusedRef = useRef(false)
  useEffect(() => {
    if (state.roundNumber !== prevRoundRef.current && state.lastZombieRolls.length > 0) {
      prevRoundRef.current = state.roundNumber
      setZombieAnimPhase('showing')
      const timer = setTimeout(() => {
        if (!zombieOverlayFocusedRef.current) {
          setZombieAnimPhase('idle')
        }
      }, 3500)
      return () => clearTimeout(timer)
    }
    prevRoundRef.current = state.roundNumber
  }, [state.roundNumber, state.lastZombieRolls.length])

  // ── Derived state ────────────────────────────────────────────────────────────

  const me: PlayerSlot | undefined = state.players.find(p => p.id === myPlayerId)
  const mySeat = me?.seatIndex ?? -1
  const isMyActiveTurn = state.activeSeat === mySeat && mySeat >= 0
  const myHasActedThisRound = state.seatsActedThisRound.includes(mySeat)
  const activeSeatPlayer = state.activeSeat !== null
    ? state.players.find(p => p.seatIndex === state.activeSeat)
    : null

  // Seat claiming (AC-v2-6): when no seat is active and I haven't acted this round,
  // my first action claims the turn. The board must be interactive in this state too —
  // otherwise the first player can never place their first tile.
  const canClaimTurn =
    state.phase === 'Actions' &&
    state.activeSeat === null &&
    mySeat >= 0 &&
    !myHasActedThisRound
  const isMyTurn = isMyActiveTurn || canClaimTurn

  // Track previous values to detect changes and drive screen-reader announcements.
  // Initialised from current state so reconnecting mid-game does not fire false alerts.
  const prevExitRevealedRef = useRef(state.exitRevealed)
  const prevMyCharEliminatedRef = useRef(
    state.characters.find(c => c.playerId === myPlayerId)?.eliminated ?? false
  )

  const myChar: CharacterState | undefined = state.characters.find(c => c.playerId === myPlayerId)
  const myCharPlaced = myChar !== null && myChar !== undefined && myChar.pos !== null
  const myReservedSpawn = state.reservedSpawnCells[myPlayerId] ?? null

  const myHand: HeldTile[] = state.hands[myPlayerId] ?? []
  const myZombieTile: HeldTile | null = myHand.find(t => t.isZombieTile) ?? null
  const hasZombieObligation = myZombieTile !== null && isMyActiveTurn

  // Detect state changes that need live screen-reader announcements
  const myCharEliminated = (state.characters.find(c => c.playerId === myPlayerId)?.eliminated ?? false)
  const exitJustRevealed = state.exitRevealed && !prevExitRevealedRef.current
  const justEliminated = myCharEliminated && !prevMyCharEliminatedRef.current

  // Show exit banner when exit is newly revealed; auto-dismiss after 4s
  useEffect(() => {
    if (state.exitRevealed && !prevExitRevealedRef.current) {
      setExitBannerVisible(true)
      const timer = setTimeout(() => setExitBannerVisible(false), 4000)
      return () => clearTimeout(timer)
    }
  // We need prevExitRevealedRef.current's value at effect time — we read it before the ref
  // is updated below, so state.exitRevealed changing is the correct trigger.
  // eslint-disable-next-line react-hooks/exhaustive-deps
  }, [state.exitRevealed])

  // Update refs after deriving the "just changed" flags
  prevExitRevealedRef.current = state.exitRevealed
  prevMyCharEliminatedRef.current = myCharEliminated

  // AP display. When claiming (not yet active), the seat will receive the full AP pool
  // on the first dispatched action, so treat AP as available for interaction gating.
  const ap = isMyActiveTurn
    ? state.actionPointsRemaining
    : canClaimTurn
    ? (AP_POOL_SIZE[state.players.length] ?? 0)
    : 0
  const qualActions = isMyActiveTurn ? state.qualifyingActionsThisTurn : 0

  // Character positions for board rendering
  const characterPositions: Record<string, string> = {}
  for (const c of state.characters) {
    if (c.pos && !c.eliminated) {
      characterPositions[c.playerId] = c.pos
    }
  }

  const zombieCoords = state.zombies.map(z => z.pos)

  // Cells the active player can actually act on right now — mirrors handleCellClick's
  // "opens a picker" branches. Drives aria-disabled on the board so keyboard/screen-reader
  // users aren't sent to dead cells with no feedback.
  const actionableCoords = new Set<string>()
  if (isMyTurn && ap > 0) {
    const hasPlaceableTile = myHand.some(h => !h.isZombieTile)
    for (const coord of state.cells) {
      const cell = state.grid[coord]
      const inExitZone = state.exitZoneCells.includes(coord)
      if (hasZombieObligation) {
        if (coord in state.grid && !inExitZone) actionableCoords.add(coord)
        continue
      }
      if (cell) {
        if (!cell.fixed) actionableCoords.add(coord)
        continue
      }
      if (inExitZone) continue
      if ((myCharPlaced && myChar?.pos && !myCharEliminated) || hasPlaceableTile) {
        actionableCoords.add(coord)
      }
    }
  }

  // ── Dispatch helpers ─────────────────────────────────────────────────────────

  function send(action: HexEscapeAction) {
    dispatch(action)
    setPicker(null)
  }

  function handleDrawTile() {
    if (!isMyTurn || ap === 0) return
    send({ type: 'DrawTile' })
  }

  function handleEndTurn() {
    if (!isMyActiveTurn) return
    send({ type: 'EndTurn' })
  }

  // ── Cell click logic ─────────────────────────────────────────────────────────

  function handleCellClick(coord: string) {
    if (!isMyTurn || ap === 0) return

    // If holding zombie tile: show zombie placement picker
    if (hasZombieObligation) {
      const cellHasTile = coord in state.grid
      const isExitZone = state.exitZoneCells.includes(coord)
      if (cellHasTile && !isExitZone) {
        setPicker({ kind: 'placeZombie', coord })
      }
      return
    }

    const cell = state.grid[coord]
    const isExitZone = state.exitZoneCells.includes(coord)

    if (cell) {
      // Placed tile: offer rotate if non-fixed, and not in exit zone
      if (!cell.fixed) {
        setPicker({ kind: 'rotate', coord, rotation: cell.rotation })
      }
      return
    }

    if (isExitZone) {
      // Exit zone cells cannot receive tiles
      return
    }

    // Empty cell: check move vs place
    // If my character is placed and adjacent cell is connected → offer move
    if (myCharPlaced && myChar?.pos && !myCharEliminated) {
      // Offer move (server validates connectivity)
      setPicker({ kind: 'move', toCoord: coord })
    } else {
      // Offer place tile
      const firstAvailable = TILE_TYPES.find(t =>
        myHand.some(h => h.tileType === t && !h.isZombieTile)
      ) ?? null
      if (myHand.filter(h => !h.isZombieTile).length > 0) {
        setPicker({ kind: 'place', coord, tileType: firstAvailable, rotation: 0 })
      }
    }
  }

  // ── Picker confirmation ──────────────────────────────────────────────────────

  function confirmPicker() {
    if (!picker) return
    if (picker.kind === 'place') {
      if (!picker.tileType) return
      send({ type: 'PlaceTile', coord: picker.coord, tileType: picker.tileType, rotation: picker.rotation })
    } else if (picker.kind === 'placeZombie') {
      send({ type: 'PlaceZombieTile', coord: picker.coord })
    } else if (picker.kind === 'rotate') {
      send({ type: 'RotateTile', coord: picker.coord, rotation: picker.rotation })
    } else if (picker.kind === 'move') {
      send({ type: 'MoveCharacter', coord: picker.toCoord })
    }
  }

  function cancelPicker() {
    setPicker(null)
  }

  // For rotate mode, look up the current tile type from the grid to drive the preview
  const pickerPreviewTileType: HexTileType | null = (() => {
    if (!picker) return null
    if (picker.kind === 'place') return picker.tileType
    if (picker.kind === 'rotate') return state.grid[picker.coord]?.tileType ?? null
    return null
  })()

  // ── Game Over screen ─────────────────────────────────────────────────────────

  if (state.phase === 'GameOver') {
    const escaped = state.outcome === 'Escaped'
    return (
      <div data-game-theme="hexescape" className={styles.root}>
        <div role="alert" aria-atomic="true" className="sr-only">
          {escaped
            ? 'Escaped! All characters reached the exit.'
            : 'Overrun! All characters were eliminated by zombies.'}
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
              ? 'All characters reached the exit. Great teamwork!'
              : 'The zombie horde eliminated all characters.'}
          </div>
          <div className={styles.gameOverStats}>
            <div className={styles.gameOverStat}>
              <div className={styles.gameOverStatLabel}>Level</div>
              <div className={[styles.gameOverStatValue, styles.gameOverStatValueSmall].join(' ')}>{state.levelName}</div>
            </div>
            <div className={styles.gameOverStat}>
              <div className={styles.gameOverStatLabel}>Rounds survived</div>
              <div className={styles.gameOverStatValue}>{state.roundNumber}</div>
            </div>
            <div className={styles.gameOverStat}>
              <div className={styles.gameOverStatLabel}>Characters at exit</div>
              <div className={styles.gameOverStatValue}>{state.exitConnectedCount}</div>
            </div>
          </div>
        </div>
      </div>
    )
  }

  // ── Playing screen ───────────────────────────────────────────────────────────

  // EndTurn disabled logic
  const endTurnDisabledReason: string | null = (() => {
    if (!isMyActiveTurn) return 'Not your turn'
    if (hasZombieObligation) return 'Place your zombie tile first'
    if (qualActions < MIN_ACTIONS_PER_TURN) return `Take at least ${MIN_ACTIONS_PER_TURN} actions first`
    return null
  })()

  // Draw disabled
  const drawDisabled = !isMyTurn || ap === 0 || hasZombieObligation || myHand.filter(h => !h.isZombieTile).length >= 3
  const drawDisabledReason: string | null = (() => {
    if (!isMyTurn) return 'Not your turn'
    if (hasZombieObligation) return 'Place your zombie tile first'
    if (myHand.filter(h => !h.isZombieTile).length >= 3) return 'Hand is full'
    if (ap === 0) return 'No action points remaining'
    return null
  })()

  return (
    <div data-game-theme="hexescape" className={styles.root}>
      {/* Screen-reader announcer — turn state (polite, does not interrupt) */}
      <div aria-live="polite" aria-atomic="true" className="sr-only">
        {isMyActiveTurn
          ? `Your turn — ${ap} AP remaining, ${qualActions} of ${MIN_ACTIONS_PER_TURN} qualifying actions taken.`
          : activeSeatPlayer
          ? `${activeSeatPlayer.displayName}'s turn.`
          : 'Waiting for a player to claim their turn.'}
      </div>

      {/* Screen-reader alert — urgent one-time events (assertive, interrupts) */}
      <div role="alert" aria-live="assertive" aria-atomic="true" className="sr-only">
        {justEliminated
          ? 'Your character has been eliminated by a zombie.'
          : exitJustRevealed
          ? `The exit has been revealed at cell ${state.exitCell ?? ''}. Move your character there to escape.`
          : null}
      </div>

      {/* Exit reveal ceremony banner — sighted players */}
      {exitBannerVisible && (
        <ExitRevealBanner onDismiss={() => setExitBannerVisible(false)} />
      )}

      {/* Zombie animation overlay */}
      {zombieAnimPhase === 'showing' && state.lastZombieRolls.length > 0 && (
        <ZombieRollOverlay
          rolls={state.lastZombieRolls}
          onDone={() => setZombieAnimPhase('idle')}
          onFocusChange={(focused) => { zombieOverlayFocusedRef.current = focused }}
        />
      )}

      {/* ── Header strip ── */}
      <div className={styles.header}>
        <div className={styles.levelName}>{state.levelName}</div>
        <div className={styles.roundInfo} aria-label={`Round ${state.roundNumber}`}>
          Round {state.roundNumber}
        </div>
        {state.exitRevealed && (
          <div className={styles.exitConnected} aria-label={`${state.exitConnectedCount} characters at exit`}>
            <span className={styles.exitConnectedLabel}>At exit</span>
            <span className={styles.exitConnectedCount}>{state.exitConnectedCount}</span>
          </div>
        )}
        <div className={styles.phaseTag} aria-label={`Phase: ${state.phase}`}>
          {state.phase === 'ZombieMovement' ? 'Zombie Move' : state.phase}
        </div>
      </div>

      {/* ── Two-column layout ── */}
      <div className={styles.gameLayout}>

        {/* ── Main column: turn bar + board ── */}
        <div className={styles.mainCol}>

          {/* Turn/AP panel */}
          <div className={styles.actionBar}>
            <div className={styles.turnBlock}>
              <span className={isMyActiveTurn ? styles.turnInfoYours : styles.turnInfo}>
                {isMyActiveTurn
                  ? 'Your turn'
                  : activeSeatPlayer
                  ? `${activeSeatPlayer.displayName}'s turn`
                  : myHasActedThisRound
                  ? 'Waiting for others…'
                  : 'Click a cell to claim your turn'}
              </span>
              {isMyActiveTurn && (
                <span className={styles.apDisplay} aria-label={`${ap} action points remaining`}>
                  {ap} AP
                </span>
              )}
              {isMyActiveTurn && (
                <span
                  className={qualActions >= MIN_ACTIONS_PER_TURN ? styles.qualCountMet : styles.qualCount}
                  aria-label={`${qualActions} of ${MIN_ACTIONS_PER_TURN} qualifying actions taken${qualActions >= MIN_ACTIONS_PER_TURN ? ', minimum met' : ''}`}
                >
                  {qualActions >= MIN_ACTIONS_PER_TURN ? '✓ ' : ''}{qualActions}/{MIN_ACTIONS_PER_TURN} actions
                </span>
              )}
            </div>

            <div className={styles.turnActions}>
              {/* Draw button */}
              <button
                className={styles.btnDraw}
                onClick={handleDrawTile}
                disabled={drawDisabled}
                aria-label={
                  drawDisabledReason
                    ? `Draw tile. Unavailable: ${drawDisabledReason}.`
                    : `Draw tile (1 AP). ${state.deckSize} tiles in deck.`
                }
                title={drawDisabledReason ?? undefined}
              >
                Draw ({state.deckSize})
              </button>

              {/* End turn */}
              <button
                className={styles.btnEndTurn}
                onClick={handleEndTurn}
                disabled={endTurnDisabledReason !== null}
                aria-label={endTurnDisabledReason ?? 'End your turn'}
                title={endTurnDisabledReason ?? undefined}
              >
                End Turn
              </button>
            </div>
          </div>

          {/* Zombie obligation banner */}
          {hasZombieObligation && (
            <div className={styles.zombieBanner} role="status" aria-live="polite">
              You drew a zombie tile — click a tiled, non-exit cell without a zombie to spawn one there.
            </div>
          )}

          {/* Board */}
          <HexBoard
            cells={state.cells}
            grid={state.grid}
            spawnZoneCells={state.spawnZoneCells}
            exitZoneCells={state.exitZoneCells}
            exitCell={state.exitCell}
            exitRevealed={state.exitRevealed}
            characterPositions={characterPositions}
            myPlayerId={myPlayerId}
            zombieCoords={zombieCoords}
            myReservedSpawnCell={myReservedSpawn}
            myCharacterPlaced={myCharPlaced}
            onCellClick={handleCellClick}
            canInteract={isMyTurn && ap > 0}
            actionableCoords={actionableCoords}
            exitJustRevealed={exitBannerVisible}
          />
        </div>

        {/* ── Sidebar ── */}
        <div className={styles.sideCol}>

          {/* My hand */}
          <div className={styles.sideSection} role="region" aria-label="Your hand">
            <div className={styles.sideTitle}>Your hand</div>
            {myHand.length === 0 ? (
              <div className={styles.emptyHandNote}>No tiles in hand. Draw to get started.</div>
            ) : (
              <div className={styles.handList}>
                {myHand.map((tile, idx) => (
                  <div
                    key={idx}
                    className={tile.isZombieTile ? styles.handTileZombie : styles.handTile}
                    aria-label={`${tile.isZombieTile ? 'Zombie tile' : TILE_LABELS[tile.tileType]}`}
                  >
                    <span className={styles.handTileType}>
                      {tile.isZombieTile ? 'ZOMBIE' : TILE_LABELS[tile.tileType]}
                    </span>
                    {tile.isZombieTile && (
                      <span className={styles.handTileObligation}>place first</span>
                    )}
                  </div>
                ))}
              </div>
            )}
            <div className={styles.deckInfo} aria-label={`${state.deckSize} tiles remaining in deck`}>
              Deck: {state.deckSize} left
            </div>
          </div>

          {/* Players */}
          <div className={styles.sideSection} role="region" aria-label="Players">
            <div className={styles.sideTitle}>Players</div>
            {state.players.map(p => {
              const hasActed = state.seatsActedThisRound.includes(p.seatIndex)
              const isActive = state.activeSeat === p.seatIndex
              const isMe = p.id === myPlayerId
              const charState = state.characters.find(c => c.playerId === p.id)
              const handCount = state.handSizes[p.id] ?? 0
              return (
                <PlayerRow
                  key={p.id}
                  player={p}
                  hasActed={hasActed}
                  isActive={isActive}
                  isMe={isMe}
                  charState={charState}
                  handCount={handCount}
                  apRemaining={isActive ? state.actionPointsRemaining : null}
                />
              )
            })}
          </div>

        </div>
      </div>

      {/* ── Pickers ── */}
      {picker && isMyTurn && (
        <ActionPicker
          picker={picker}
          myHand={myHand}
          previewTileType={pickerPreviewTileType}
          onSelectType={(t) => {
            if (picker.kind === 'place') setPicker({ ...picker, tileType: t })
          }}
          onSetRotation={(delta) => {
            if (picker.kind === 'place') {
              const next = ((picker.rotation + delta) % 6 + 6) % 6
              setPicker({ ...picker, rotation: next })
            } else if (picker.kind === 'rotate') {
              const next = ((picker.rotation + delta) % 6 + 6) % 6
              setPicker({ ...picker, rotation: next })
            }
          }}
          onConfirm={confirmPicker}
          onCancel={cancelPicker}
        />
      )}
    </div>
  )
}

// ── Exit reveal banner ─────────────────────────────────────────────────────────

interface ExitRevealBannerProps {
  onDismiss: () => void
}

function ExitRevealBanner({ onDismiss }: ExitRevealBannerProps) {
  return (
    <div
      className={styles.exitRevealBanner}
      role="status"
      aria-live="polite"
      aria-atomic="true"
    >
      <span className={styles.exitRevealBannerIcon} aria-hidden="true">E</span>
      <span className={styles.exitRevealBannerText}>EXIT REVEALED — reach it to escape!</span>
      <button
        className={styles.exitRevealBannerDismiss}
        type="button"
        onClick={onDismiss}
        aria-label="Dismiss exit revealed notification"
      >
        ✕
      </button>
    </div>
  )
}

// ── PlayerRow ─────────────────────────────────────────────────────────────────

interface PlayerRowProps {
  player: PlayerSlot
  hasActed: boolean
  isActive: boolean
  isMe: boolean
  charState: CharacterState | undefined
  handCount: number
  apRemaining: number | null
}

function PlayerRow({ player, hasActed, isActive, isMe, charState, handCount, apRemaining }: PlayerRowProps) {
  const eliminated = charState?.eliminated ?? false
  const placed = charState?.pos != null

  const rowClass = [
    styles.playerRow,
    isActive ? styles.playerRowActive : '',
    hasActed ? styles.playerRowActed : styles.playerRowWaiting,
    eliminated ? styles.playerRowEliminated : '',
  ].filter(Boolean).join(' ')

  return (
    <div
      className={rowClass}
      aria-label={[
        player.displayName,
        isMe ? '(you)' : '',
        isActive ? 'taking turn' : hasActed ? 'acted' : 'waiting',
        eliminated ? 'eliminated' : '',
        !placed ? 'not yet placed' : '',
        `${handCount} tile${handCount !== 1 ? 's' : ''} in hand`,
      ].filter(Boolean).join(', ')}
    >
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
        {isMe && <span className={styles.playerNameYou}> (you)</span>}
      </span>
      <div className={styles.playerMeta}>
        {eliminated && <span className={styles.badgeEliminated}>out</span>}
        {!placed && !eliminated && <span className={styles.badgeUnplaced}>unplaced</span>}
        {isActive && apRemaining !== null && (
          <span className={styles.badgeAP}>{apRemaining} AP</span>
        )}
        <span className={styles.playerHandCount}>{handCount}</span>
        <span className={hasActed ? styles.playerBadgeActed : styles.playerBadgePending}>
          {isActive ? 'active' : hasActed ? 'acted' : 'waiting'}
        </span>
      </div>
    </div>
  )
}

// ── TilePreview — inline SVG hex showing pipe edges at chosen rotation ─────────

// We derive edge midpoints using our own preview-size geometry rather than
// reusing HexBoard's size-bound helpers, keeping the preview self-contained.

const PREVIEW_HEX_SIZE = 28  // slightly smaller than board cells (HEX_SIZE = 32)
const PREVIEW_SQRT3 = Math.sqrt(3)
const PREVIEW_VIEWBOX_HALF = PREVIEW_HEX_SIZE + 6  // small padding around the hex

function previewHexCorners(cx: number, cy: number): string {
  const pts: string[] = []
  for (let i = 0; i < 6; i++) {
    const angleRad = (Math.PI / 180) * (60 * i)
    pts.push(`${(cx + PREVIEW_HEX_SIZE * Math.cos(angleRad)).toFixed(2)},${(cy + PREVIEW_HEX_SIZE * Math.sin(angleRad)).toFixed(2)}`)
  }
  return pts.join(' ')
}

function previewEdgeMidpoint(cx: number, cy: number, dir: number): { x: number; y: number } {
  const angle = (Math.PI / 180) * (60 * dir)
  const inradius = PREVIEW_HEX_SIZE * (PREVIEW_SQRT3 / 2)
  return { x: cx + inradius * Math.cos(angle), y: cy + inradius * Math.sin(angle) }
}

interface TilePreviewProps {
  tileType: HexTileType | null
  rotation: number
}

function TilePreview({ tileType, rotation }: TilePreviewProps) {
  const cx = 0
  const cy = 0
  const vbHalf = PREVIEW_VIEWBOX_HALF
  const corners = previewHexCorners(cx, cy)

  return (
    <svg
      className={styles.tilePreviewSvg}
      viewBox={`${-vbHalf} ${-vbHalf} ${vbHalf * 2} ${vbHalf * 2}`}
      aria-hidden="true"
    >
      <polygon points={corners} className={styles.tilePreviewHex} />
      {tileType && (
        <RoadTile
          cx={cx}
          cy={cy}
          ends={openEdges(tileType, rotation).map(dir => previewEdgeMidpoint(cx, cy, dir))}
          fixed={false}
        />
      )}
    </svg>
  )
}

// ── ActionPicker modal ────────────────────────────────────────────────────────

interface ActionPickerProps {
  picker: PickerMode
  myHand: HeldTile[]
  /** Tile type to show in the rotation preview (null for place mode before a type is chosen,
   *  or when the picker does not support a visual preview). */
  previewTileType: HexTileType | null
  onSelectType: (t: HexTileType) => void
  onSetRotation: (delta: number) => void
  onConfirm: () => void
  onCancel: () => void
}

function ActionPicker({ picker, myHand, previewTileType, onSelectType, onSetRotation, onConfirm, onCancel }: ActionPickerProps) {
  const cardRef = useRef<HTMLDivElement>(null)

  // Auto-focus first focusable element on open
  useEffect(() => {
    const first = cardRef.current?.querySelector<HTMLElement>(
      'button:not(:disabled), [tabindex]:not([tabindex="-1"])'
    )
    first?.focus()
  }, [])

  // Focus trap
  function handleKeyDown(e: React.KeyboardEvent<HTMLDivElement>) {
    if (e.key === 'Escape') { onCancel(); return }
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
      if (document.activeElement === first) { e.preventDefault(); last.focus() }
    } else {
      if (document.activeElement === last) { e.preventDefault(); first.focus() }
    }
  }

  const coordDisplay =
    picker.kind === 'move' ? picker.toCoord : picker.coord
  const titleId = `action-picker-${coordDisplay.replace(',', '-')}`

  function getTitle(): string {
    if (picker.kind === 'place') return `Place tile on ${picker.coord}`
    if (picker.kind === 'placeZombie') return `Spawn zombie on ${picker.coord}`
    if (picker.kind === 'rotate') return `Rotate tile on ${picker.coord}`
    return `Move to ${picker.toCoord}`
  }

  // Determine if confirm is allowed
  const canConfirm = (() => {
    if (picker.kind === 'place') return picker.tileType !== null
    return true
  })()

  // Available normal tiles for place mode
  const availableTiles: Map<HexTileType, number> = new Map()
  for (const tile of myHand) {
    if (!tile.isZombieTile) {
      availableTiles.set(tile.tileType, (availableTiles.get(tile.tileType) ?? 0) + 1)
    }
  }

  const currentRotation = picker.kind === 'place'
    ? picker.rotation
    : picker.kind === 'rotate'
    ? picker.rotation
    : null

  const showRotationControl = picker.kind === 'place' || picker.kind === 'rotate'

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
          {getTitle()}
        </div>

        {/* Tile type selector (place mode only) */}
        {picker.kind === 'place' && (
          <div className={styles.pickerGrid} role="group" aria-label="Select tile type">
            {TILE_TYPES.map(t => {
              const count = availableTiles.get(t) ?? 0
              const isAvailable = count > 0
              const isSelected = picker.tileType === t
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
                  aria-label={`${TILE_LABELS[t]}, ${count} in hand`}
                >
                  <span>{TILE_LABELS[t]}</span>
                  <span className={styles.tileBtnCount}>{count} left</span>
                </button>
              )
            })}
          </div>
        )}

        {/* Zombie placement confirmation */}
        {picker.kind === 'placeZombie' && (
          <div className={styles.zombiePickerNote}>
            Spawn a zombie at {picker.coord}. A zombie will appear here and may eliminate characters.
          </div>
        )}

        {/* Move confirmation */}
        {picker.kind === 'move' && (
          <div className={styles.movePickerNote}>
            Move your character to {picker.toCoord}.
            {' '}The move is only allowed along an open pipe connection.
          </div>
        )}

        {/* Rotation section: visual tile preview + rotation controls */}
        {showRotationControl && currentRotation !== null && (
          <div className={styles.rotationSection}>
            {/* Visual pipe preview — decorative, aria-hidden on the SVG itself */}
            <div className={styles.tilePreviewWrap}>
              <TilePreview tileType={previewTileType} rotation={currentRotation} />
            </div>

            <div className={styles.rotationWrap}>
              <span className={styles.rotLabel}>Rotation</span>
              <button
                className={styles.rotBtn}
                onClick={() => onSetRotation(-1)}
                aria-label="Rotate counter-clockwise"
              >
                &#8635;
              </button>
              <span className={styles.rotValue} aria-label={`Rotation: ${currentRotation} of 5`}>
                {currentRotation}
              </span>
              <button
                className={styles.rotBtn}
                onClick={() => onSetRotation(+1)}
                aria-label="Rotate clockwise"
              >
                &#8634;
              </button>
              <span className={styles.rotNote}>× 60°</span>
            </div>
          </div>
        )}

        {/* Actions */}
        <div className={styles.pickerActions}>
          <button className={styles.btnCancel} type="button" onClick={onCancel}>
            Cancel
          </button>
          <button
            className={styles.btnConfirm}
            type="button"
            onClick={onConfirm}
            disabled={!canConfirm}
          >
            {picker.kind === 'place' ? 'Place'
              : picker.kind === 'placeZombie' ? 'Spawn'
              : picker.kind === 'rotate' ? 'Rotate'
              : 'Move'}
          </button>
        </div>
      </div>
    </div>
  )
}

// ── Zombie Roll Overlay ────────────────────────────────────────────────────────

interface ZombieRollOverlayProps {
  rolls: ZombieRoll[]
  onDone: () => void
  onFocusChange: (focused: boolean) => void
}

const DIR_NAMES = ['E', 'NE', 'N', 'W', 'SW', 'S']

function ZombieRollOverlay({ rolls, onDone, onFocusChange }: ZombieRollOverlayProps) {
  const cardRef = useRef<HTMLDivElement>(null)
  const titleId = 'zombie-roll-overlay-title'

  // Focus trap — keep Tab within the dialog
  function handleKeyDown(e: React.KeyboardEvent<HTMLDivElement>) {
    if (e.key === 'Escape') { onDone(); return }
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
      if (document.activeElement === first) { e.preventDefault(); last.focus() }
    } else {
      if (document.activeElement === last) { e.preventDefault(); first.focus() }
    }
  }

  return (
    <div
      className={styles.zombieOverlay}
      role="dialog"
      aria-modal="true"
      aria-labelledby={titleId}
      onKeyDown={handleKeyDown}
      onFocus={() => onFocusChange(true)}
      onBlur={(e) => {
        // Only signal "not focused" when focus leaves the dialog entirely
        if (!e.currentTarget.contains(e.relatedTarget as Node | null)) {
          onFocusChange(false)
        }
      }}
    >
      <div className={styles.zombieOverlayCard} ref={cardRef}>
        <div id={titleId} className={styles.zombieOverlayTitle}>Zombie movement</div>
        <div className={styles.zombieRollList}>
          {rolls.map((roll) => (
            <div
              key={roll.zombieId}
              className={roll.moved ? styles.zombieRollMoved : styles.zombieRollStayed}
              aria-label={`Zombie rolled ${roll.dieFace}, direction ${DIR_NAMES[roll.direction]}, ${roll.moved ? 'moved' : 'blocked'}`}
            >
              <span className={styles.zombieRollDie} aria-hidden="true">{roll.dieFace}</span>
              <span className={styles.zombieRollDir} aria-hidden="true">{DIR_NAMES[roll.direction]}</span>
              <span className={styles.zombieRollResult} aria-hidden="true">
                {roll.moved ? 'moved' : 'blocked'}
              </span>
            </div>
          ))}
        </div>
        <button
          className={styles.btnDismiss}
          type="button"
          onClick={onDone}
          autoFocus
        >
          Continue
        </button>
      </div>
    </div>
  )
}
