// HexBoard — axial-to-pixel hex rendering.
// Geometry is unchanged from v1; Props updated for v2 state shape.

import type { HexTileType, HexCell } from '../types'
import styles from '../styles.module.css'

// ── Geometry constants ────────────────────────────────────────────────────────
// Flat-top hexagon: width = size*2, height = size*sqrt(3)
// Axial → pixel (flat-top layout):
//   x = size * 3/2 * q
//   y = size * (sqrt(3)/2 * q + sqrt(3) * r)

const HEX_SIZE = 32
const SQRT3 = Math.sqrt(3)

/** Axial (q, r) → SVG pixel centre, flat-top orientation */
function hexToPixel(q: number, r: number): { x: number; y: number } {
  return {
    x: HEX_SIZE * (3 / 2) * q,
    y: HEX_SIZE * (SQRT3 / 2 * q + SQRT3 * r),
  }
}

/** Six corners of a flat-top hex centred at (cx, cy) */
function hexCorners(cx: number, cy: number): string {
  const pts: string[] = []
  for (let i = 0; i < 6; i++) {
    const angleDeg = 60 * i // flat-top: 0° points right
    const angleRad = (Math.PI / 180) * angleDeg
    const x = cx + HEX_SIZE * Math.cos(angleRad)
    const y = cy + HEX_SIZE * Math.sin(angleRad)
    pts.push(`${x.toFixed(2)},${y.toFixed(2)}`)
  }
  return pts.join(' ')
}

/** Base edges for each tile type (before rotation) */
const BASE_EDGES: Record<HexTileType, number[]> = {
  Straight: [0, 3],
  Elbow:    [0, 1],
  Tee:      [0, 1, 2],
  Cross:    [0, 1, 2, 3],
  Deadend:  [0],
}

/** Open edges after applying rotation k */
export function openEdges(tileType: HexTileType, rotation: number): number[] {
  return BASE_EDGES[tileType].map(e => (e + rotation) % 6)
}

/**
 * Physical screen angle (degrees, SVG y-down) of each axial direction, in the same
 * order as the backend's Directions table [E, NE, N, W, SW, S].
 *
 * For a FLAT-TOP hex the six neighbours sit at the EDGE MIDPOINTS, not the corners.
 * These angles are derived from the hexToPixel deltas of each direction:
 *   dir 0 (+1, 0)  → 30°    dir 1 (+1,-1) → -30°   dir 2 (0,-1) → -90°
 *   dir 3 (-1, 0)  → -150°  dir 4 (-1,+1) → 150°   dir 5 (0,+1) →  90°
 * (Using 60*dir here was the old bug — it pointed roads at the corners.)
 */
export const DIR_ANGLE_DEG = [30, -30, -90, -150, 150, 90]

/**
 * Midpoint of the hex edge facing direction `dir`. Roads run from the cell centre
 * to this point, so connected tiles meet exactly at the shared edge.
 * The inradius (centre→edge distance) of a flat-top hex is HEX_SIZE * sqrt(3)/2.
 */
export function edgeMidpoint(cx: number, cy: number, dir: number): { x: number; y: number } {
  const angle = (Math.PI / 180) * DIR_ANGLE_DEG[dir]
  const inradius = HEX_SIZE * (SQRT3 / 2)
  return {
    x: cx + inradius * Math.cos(angle),
    y: cy + inradius * Math.sin(angle),
  }
}

/** Parse "q,r" → {q, r} */
export function parseCoord(key: string): { q: number; r: number } {
  const [qs, rs] = key.split(',')
  return { q: parseInt(qs, 10), r: parseInt(rs, 10) }
}

/**
 * Axial direction offsets (Δq, Δr), indexed the same as the backend's
 * HexEscapeModule.Directions table: 0=E 1=NE 2=N 3=W 4=SW 5=S.
 */
export const DIRECTIONS: ReadonlyArray<readonly [number, number]> = [
  [+1, 0], [+1, -1], [0, -1], [-1, 0], [-1, +1], [0, +1],
]

/**
 * Legal one-step move destinations from `fromCoord`: adjacent cells that are on
 * the board, hold a placed tile, and share an open road edge in both directions.
 * Mirrors the backend's HexEscapeModule.AreConnected so the UI only offers moves
 * the server will accept. Fixed tiles (e.g. the exit Cross) are valid targets —
 * stepping onto the exit is how the game is won.
 */
export function connectedMoveTargets(
  grid: Record<string, HexCell>,
  cells: Set<string>,
  fromCoord: string,
): Set<string> {
  const targets = new Set<string>()
  const fromTile = grid[fromCoord]
  if (!fromTile) return targets

  const fromEdges = new Set(openEdges(fromTile.tileType, fromTile.rotation))
  const { q, r } = parseCoord(fromCoord)

  for (let d = 0; d < 6; d++) {
    if (!fromEdges.has(d)) continue
    const [dq, dr] = DIRECTIONS[d]
    const toCoord = `${q + dq},${r + dr}`
    if (!cells.has(toCoord)) continue
    const toTile = grid[toCoord]
    if (!toTile) continue
    const opposite = (d + 3) % 6
    if (openEdges(toTile.tileType, toTile.rotation).includes(opposite)) {
      targets.add(toCoord)
    }
  }
  return targets
}

/** HEX_SIZE constant exported for use by TilePreview */
export const HEX_PREVIEW_SIZE = HEX_SIZE

// ── HexBoard Props ────────────────────────────────────────────────────────────

export interface HexBoardProps {
  cells: string[]
  grid: Record<string, HexCell>
  spawnZoneCells: string[]
  exitZoneCells: string[]
  exitCell: string | null
  exitRevealed: boolean
  /** Per-player character positions: playerId → coord */
  characterPositions: Record<string, string>
  /** Player id whose character is mine (highlight differently) */
  myPlayerId: string
  /** Zombie positions: array of coord strings (may have duplicates for stacked zombies) */
  zombieCoords: string[]
  /** Reserved spawn cell for my player (highlighted until placed) */
  myReservedSpawnCell: string | null
  /** Has my character been placed yet? */
  myCharacterPlaced: boolean
  onCellClick: (coord: string) => void
  canInteract: boolean
  /** Coords the active player can actually act on right now. When provided,
   *  cells not in the set are announced as disabled (focusable to read, but inert). */
  actionableCoords?: Set<string>
  /** Show debug coordinate labels. Defaults to import.meta.env.DEV. */
  showCoords?: boolean
  /** When true, the exit cell renders a pulsing highlight (exit-just-revealed ceremony). */
  exitJustRevealed?: boolean
}

export function HexBoard({
  cells,
  grid,
  spawnZoneCells,
  exitZoneCells,
  exitCell,
  exitRevealed,
  characterPositions,
  myPlayerId,
  zombieCoords,
  myReservedSpawnCell,
  myCharacterPlaced,
  onCellClick,
  canInteract,
  actionableCoords,
  showCoords = import.meta.env.DEV,
  exitJustRevealed = false,
}: HexBoardProps) {
  // Build pixel positions for each cell
  const positions = new Map<string, { x: number; y: number }>()
  for (const key of cells) {
    const { q, r } = parseCoord(key)
    positions.set(key, hexToPixel(q, r))
  }

  // Compute bounding box for SVG viewBox
  let minX = Infinity, minY = Infinity, maxX = -Infinity, maxY = -Infinity
  for (const pos of positions.values()) {
    minX = Math.min(minX, pos.x - HEX_SIZE)
    minY = Math.min(minY, pos.y - HEX_SIZE)
    maxX = Math.max(maxX, pos.x + HEX_SIZE)
    maxY = Math.max(maxY, pos.y + HEX_SIZE)
  }
  const padding = HEX_SIZE * 0.5
  const vbX = minX - padding
  const vbY = minY - padding
  const vbW = maxX - minX + padding * 2
  const vbH = maxY - minY + padding * 2

  const spawnSet = new Set(spawnZoneCells)
  const exitZoneSet = new Set(exitZoneCells)

  // Build zombie count map
  const zombieCountMap = new Map<string, number>()
  for (const coord of zombieCoords) {
    zombieCountMap.set(coord, (zombieCountMap.get(coord) ?? 0) + 1)
  }

  // Build character map: coord → playerIds
  const charAtCell = new Map<string, string[]>()
  for (const [pid, coord] of Object.entries(characterPositions)) {
    const existing = charAtCell.get(coord) ?? []
    existing.push(pid)
    charAtCell.set(coord, existing)
  }

  return (
    <div className={styles.boardWrap}>
      <svg
        className={styles.boardSvg}
        viewBox={`${vbX.toFixed(1)} ${vbY.toFixed(1)} ${vbW.toFixed(1)} ${vbH.toFixed(1)}`}
        width={Math.min(660, vbW * 2)}
        height={Math.min(540, vbH * 2)}
        aria-label="Hex Escape board"
        role="group"
      >
        <title>Hex Escape game board</title>

        {cells.map(key => {
          const pos = positions.get(key)
          if (!pos) return null
          const { x, y } = pos

          const isSpawnZone = spawnSet.has(key)
          const isExitZone = exitZoneSet.has(key)
          const isExitCell = exitRevealed && key === exitCell
          const isPulsingExit = isExitCell && exitJustRevealed
          const cell: HexCell | undefined = grid[key]
          const isOccupied = cell !== undefined
          const isFixed = cell?.fixed ?? false
          const isMyReservedSpawn = !myCharacterPlaced && key === myReservedSpawnCell
          const zombieCount = zombieCountMap.get(key) ?? 0
          const charsHere = charAtCell.get(key) ?? []
          const myCharHere = charsHere.includes(myPlayerId)

          // Polygon class priority: exit zone > exit cell > placed > spawn zone > empty
          let polyClass = styles.hexEmpty
          if (isExitCell) polyClass = styles.hexExit
          else if (isExitZone) polyClass = styles.hexExitZone
          else if (isFixed) polyClass = styles.hexFixed
          else if (isOccupied) polyClass = styles.hexPlaced
          else if (isSpawnZone) polyClass = styles.hexSpawn
          else if (isMyReservedSpawn) polyClass = styles.hexMySpawn

          // When interacting, every cell is focusable so a keyboard/screen-reader
          // user can read it, but only cells the player can actually act on fire
          // and are announced as enabled; the rest carry aria-disabled.
          const cellActionable = canInteract && (actionableCoords ? actionableCoords.has(key) : true)
          const cornersStr = hexCorners(x, y)

          return (
            <g
              key={key}
              className={[styles.hexBase, cellActionable ? styles.hexClickable : ''].filter(Boolean).join(' ')}
              onClick={cellActionable ? () => onCellClick(key) : undefined}
              role={canInteract ? 'button' : undefined}
              tabIndex={canInteract ? 0 : undefined}
              aria-disabled={canInteract && !cellActionable ? true : undefined}
              aria-label={hexAriaLabel(key, isSpawnZone, isExitZone, isExitCell, cell, zombieCount, charsHere, myPlayerId, isMyReservedSpawn)}
              onKeyDown={cellActionable ? (e) => {
                if (e.key === 'Enter' || e.key === ' ') {
                  e.preventDefault()
                  onCellClick(key)
                }
              } : undefined}
            >
              <polygon points={cornersStr} className={polyClass} />

              {/* Exit-reveal pulse ring (animates on reveal; respects prefers-reduced-motion via CSS) */}
              {isPulsingExit && (
                <polygon
                  points={cornersStr}
                  className={styles.hexExitPulse}
                  aria-hidden="true"
                />
              )}

              {/* City blocks: solid buildings fill the hex; streets carve through.
                  Drawn before the roads so the streets (and their sidewalk curbs)
                  overlay them cleanly. */}
              {cell && (
                <TileBlocks
                  cx={x}
                  cy={y}
                  coord={key}
                  tileType={cell.tileType}
                  rotation={cell.rotation}
                />
              )}

              {/* Tile connection lines */}
              {cell && (
                <TileLines
                  cx={x}
                  cy={y}
                  tileType={cell.tileType}
                  rotation={cell.rotation}
                  fixed={cell.fixed}
                />
              )}

              {/* My reserved spawn marker */}
              {isMyReservedSpawn && !isOccupied && (
                <text x={x} y={y} className={styles.mySpawnIcon} aria-hidden="true">S</text>
              )}

              {/* Spawn-zone marker (non-colour cue) for other spawn cells */}
              {isSpawnZone && !isMyReservedSpawn && !isOccupied && (
                <text x={x} y={y} className={styles.spawnZoneIcon} aria-hidden="true">S</text>
              )}

              {/* Exit zone marker (unrevealed) */}
              {isExitZone && !isOccupied && !isExitCell && (
                <text x={x} y={y} className={styles.exitZoneIcon} aria-hidden="true">X</text>
              )}

              {/* Exit cell marker */}
              {isExitCell && (
                <text
                  x={x}
                  y={isOccupied ? y - HEX_SIZE * 0.45 : y}
                  className={styles.exitIcon}
                  aria-hidden="true"
                >
                  E
                </text>
              )}

              {/* Zombie tokens */}
              {zombieCount > 0 && (
                <g aria-hidden="true">
                  <circle
                    cx={x + (charsHere.length > 0 ? -8 : 0)}
                    cy={y - (charsHere.length > 0 ? 8 : 0)}
                    r={7}
                    className={styles.zombieToken}
                  />
                  {zombieCount > 1 && (
                    <text
                      x={x + (charsHere.length > 0 ? -8 : 0)}
                      y={y - (charsHere.length > 0 ? 8 : 0)}
                      className={styles.zombieCount}
                    >
                      {zombieCount}
                    </text>
                  )}
                </g>
              )}

              {/* Character tokens */}
              {charsHere.map((pid, idx) => {
                const isMe = pid === myPlayerId
                const offsetX = charsHere.length > 1 ? (idx - (charsHere.length - 1) / 2) * 10 : 0
                return (
                  <circle
                    key={pid}
                    cx={x + offsetX + (zombieCount > 0 ? 8 : 0)}
                    cy={y + (zombieCount > 0 ? 8 : 0)}
                    r={6}
                    className={isMe ? styles.charTokenMe : styles.charToken}
                    aria-hidden="true"
                  />
                )
              })}

              {/* My character highlight ring */}
              {myCharHere && (
                <circle
                  cx={x + (zombieCount > 0 ? 8 : 0)}
                  cy={y + (zombieCount > 0 ? 8 : 0)}
                  r={9}
                  className={styles.charTokenMeRing}
                  aria-hidden="true"
                />
              )}

              {/* Cell coord label — dev-only aid; hidden in production */}
              {showCoords && (
                <text x={x} y={y + HEX_SIZE * 0.65} className={styles.cellLabel} aria-hidden="true">
                  {key}
                </text>
              )}
            </g>
          )
        })}
      </svg>
    </div>
  )
}

// ── Tile line renderer ────────────────────────────────────────────────────────

export interface TileLinesProps {
  cx: number
  cy: number
  tileType: HexTileType
  rotation: number
  fixed: boolean
}

// ── Road tile renderer ──────────────────────────────────────────────────────
// Tiles render as city streets: a wide asphalt band with lighter curbs and a
// dashed centre lane line, all meeting at a central intersection. Each open edge
// is one road segment running from the cell centre to that edge's midpoint, so
// roads on connected tiles meet exactly at the shared edge.

const ROAD_CASING_W = 19       // curb/sidewalk band width
const ROAD_SURFACE_W = 11.5    // asphalt width (narrower → wider sidewalk strip)
const ROAD_HUB_CASING_R = 9.5  // intersection curb radius
const ROAD_HUB_SURFACE_R = 5.75 // intersection asphalt radius
const LANE_START_T = 0.34      // lane dashes start this far out from centre…
const LANE_END_T = 0.94        // …and stop just short of the edge

export interface RoadTileProps {
  cx: number
  cy: number
  /** Edge midpoints (one per open edge) the roads run to. */
  ends: { x: number; y: number }[]
  fixed: boolean
}

/** Draws a hex tile as a set of city streets meeting at a central intersection. */
export function RoadTile({ cx, cy, ends, fixed }: RoadTileProps) {
  const casingClass = fixed ? styles.roadCasingFixed : styles.roadCasing
  const surfaceClass = fixed ? styles.roadSurfaceFixed : styles.roadSurface
  const hubCasingClass = fixed ? styles.roadHubCasingFixed : styles.roadHubCasing
  const hubClass = fixed ? styles.roadHubFixed : styles.roadHub

  return (
    <>
      {/* Curb / sidewalk casing (widest, painted first) */}
      {ends.map((end, i) => (
        <line key={`c${i}`} x1={cx} y1={cy} x2={end.x.toFixed(2)} y2={end.y.toFixed(2)}
              className={casingClass} strokeWidth={ROAD_CASING_W} />
      ))}
      <circle cx={cx} cy={cy} r={ROAD_HUB_CASING_R} className={hubCasingClass} />

      {/* Asphalt surface */}
      {ends.map((end, i) => (
        <line key={`s${i}`} x1={cx} y1={cy} x2={end.x.toFixed(2)} y2={end.y.toFixed(2)}
              className={surfaceClass} strokeWidth={ROAD_SURFACE_W} />
      ))}
      <circle cx={cx} cy={cy} r={ROAD_HUB_SURFACE_R} className={hubClass} />

      {/* Dashed centre lane markings (skip the cluttered intersection itself) */}
      {ends.map((end, i) => {
        const sx = cx + (end.x - cx) * LANE_START_T
        const sy = cy + (end.y - cy) * LANE_START_T
        const ex = cx + (end.x - cx) * LANE_END_T
        const ey = cy + (end.y - cy) * LANE_END_T
        return (
          <line key={`l${i}`} x1={sx.toFixed(2)} y1={sy.toFixed(2)} x2={ex.toFixed(2)} y2={ey.toFixed(2)}
                className={styles.roadLane} />
        )
      })}
    </>
  )
}

export function TileLines({ cx, cy, tileType, rotation, fixed }: TileLinesProps) {
  const ends = openEdges(tileType, rotation).map(dir => edgeMidpoint(cx, cy, dir))
  return <RoadTile cx={cx} cy={cy} ends={ends} fixed={fixed} />
}

// ── City blocks (top-down, Zombies!!!-style) ──────────────────────────────────
// A placed tile is a top-down city block. The whole hex is built up: the wedges
// between the streets are filled as SOLID building blocks, and the streets are
// carved through on top. The street's light curb band reads as the sidewalk that
// lines each building, so no separate sidewalk geometry is needed. Block shades
// vary deterministically per cell so adjacent blocks read as distinct buildings.

const CITY_BLOCK_CLASSES = [
  styles.cityBlock0, styles.cityBlock1, styles.cityBlock2, styles.cityBlock3, styles.cityBlock4,
]

/** Stable hash of a coord string → small unsigned int. */
function hashCoord(coord: string): number {
  let h = 0
  for (let i = 0; i < coord.length; i++) h = (h * 31 + coord.charCodeAt(i)) >>> 0
  return h
}

/** Distance from centre to the flat-top hex boundary at screen angle `deg`. */
function hexBoundaryRadius(deg: number): number {
  const m = 30 + 60 * Math.round((deg - 30) / 60)  // nearest edge-midpoint angle
  return (HEX_SIZE * (SQRT3 / 2)) / Math.cos((deg - m) * (Math.PI / 180))
}

/** A point on the hex boundary at screen angle `deg`, as an "x,y" string. */
function boundaryPoint(cx: number, cy: number, deg: number): string {
  const r = hexBoundaryRadius(deg)
  const a = deg * (Math.PI / 180)
  return `${(cx + r * Math.cos(a)).toFixed(2)},${(cy + r * Math.sin(a)).toFixed(2)}`
}

/** Hex corner angles (multiples of 60°) strictly between a and b. */
function cornersBetween(a: number, b: number): number[] {
  const out: number[] = []
  for (let k = -1; k <= 7; k++) {
    const c = 60 * k
    if (c > a + 0.01 && c < b - 0.01) out.push(c)
  }
  return out
}

/** Angular block lots between consecutive streets, as [start, end] screen angles. */
function tileWedges(openDirs: number[]): { a: number; b: number }[] {
  const angles = [...new Set(openDirs.map(d => ((DIR_ANGLE_DEG[d] % 360) + 360) % 360))]
    .sort((x, y) => x - y)
  if (angles.length === 0) return [{ a: 0, b: 360 }]
  const out: { a: number; b: number }[] = []
  for (let i = 0; i < angles.length; i++) {
    const a = angles[i]
    const b = i + 1 < angles.length ? angles[i + 1] : angles[0] + 360
    out.push({ a, b })
  }
  return out
}

interface TileBlocksProps {
  cx: number
  cy: number
  coord: string
  tileType: HexTileType
  rotation: number
}

/** Solid building blocks filling the hex between the streets (drawn under the roads). */
function TileBlocks({ cx, cy, coord, tileType, rotation }: TileBlocksProps) {
  const wedges = tileWedges(openEdges(tileType, rotation))
  const seed = hashCoord(coord)
  return (
    <g aria-hidden="true">
      {wedges.map(({ a, b }, i) => {
        // Sector polygon: centre → boundary(a) → hex corners between → boundary(b).
        // The two straight sides run along the street centrelines and are covered
        // by the asphalt drawn on top, leaving each block bounded by sidewalks.
        const points = [
          `${cx.toFixed(2)},${cy.toFixed(2)}`,
          boundaryPoint(cx, cy, a),
          ...cornersBetween(a, b).map(c => boundaryPoint(cx, cy, c)),
          boundaryPoint(cx, cy, b),
        ].join(' ')
        const shade = CITY_BLOCK_CLASSES[(seed + i) % CITY_BLOCK_CLASSES.length]
        return <polygon key={i} points={points} className={`${styles.cityBlock} ${shade}`} />
      })}
    </g>
  )
}

// ── Aria label helper ─────────────────────────────────────────────────────────

function hexAriaLabel(
  key: string,
  isSpawnZone: boolean,
  isExitZone: boolean,
  isExitCell: boolean,
  cell: HexCell | undefined,
  zombieCount: number,
  charsHere: string[],
  myPlayerId: string,
  isMyReservedSpawn: boolean,
): string {
  const parts = [`Cell ${key}`]
  if (isMyReservedSpawn) parts.push('your reserved spawn')
  else if (isSpawnZone) parts.push('spawn zone')
  if (isExitCell) parts.push('exit')
  else if (isExitZone) parts.push('exit zone reserved')
  if (cell) {
    parts.push(`${cell.tileType} tile, rotation ${cell.rotation}`)
    if (cell.fixed) parts.push('fixed')
  } else if (!isExitZone) {
    parts.push('empty')
  }
  if (zombieCount === 1) parts.push('1 zombie')
  if (zombieCount > 1) parts.push(`${zombieCount} zombies`)
  if (charsHere.includes(myPlayerId)) parts.push('your character')
  else if (charsHere.length > 0) parts.push(`${charsHere.length} character${charsHere.length > 1 ? 's' : ''}`)
  return parts.join(', ')
}
