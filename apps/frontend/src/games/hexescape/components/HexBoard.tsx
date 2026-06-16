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
function openEdges(tileType: HexTileType, rotation: number): number[] {
  return BASE_EDGES[tileType].map(e => (e + rotation) % 6)
}

/**
 * For a flat-top hex, direction d points to angle (60 * d) degrees from centre.
 * The inradius is HEX_SIZE * sqrt(3)/2.
 */
function edgeMidpoint(cx: number, cy: number, dir: number): { x: number; y: number } {
  const angle = (Math.PI / 180) * (60 * dir)
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

          const isClickable = canInteract
          const cornersStr = hexCorners(x, y)

          return (
            <g
              key={key}
              className={[styles.hexBase, isClickable ? styles.hexClickable : ''].filter(Boolean).join(' ')}
              onClick={isClickable ? () => onCellClick(key) : undefined}
              role={isClickable ? 'button' : undefined}
              tabIndex={isClickable ? 0 : undefined}
              aria-label={hexAriaLabel(key, isSpawnZone, isExitZone, isExitCell, cell, zombieCount, charsHere, myPlayerId, isMyReservedSpawn)}
              onKeyDown={isClickable ? (e) => {
                if (e.key === 'Enter' || e.key === ' ') {
                  e.preventDefault()
                  onCellClick(key)
                }
              } : undefined}
            >
              <polygon points={cornersStr} className={polyClass} />

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

              {/* Cell coord label (small, non-distracting dev aid) */}
              <text x={x} y={y + HEX_SIZE * 0.65} className={styles.cellLabel} aria-hidden="true">
                {key}
              </text>
            </g>
          )
        })}
      </svg>
    </div>
  )
}

// ── Tile line renderer ────────────────────────────────────────────────────────

interface TileLinesProps {
  cx: number
  cy: number
  tileType: HexTileType
  rotation: number
  fixed: boolean
}

function TileLines({ cx, cy, tileType, rotation, fixed }: TileLinesProps) {
  const edges = openEdges(tileType, rotation)
  const pathClass = fixed ? styles.tilePathFixed : styles.tilePath

  return (
    <>
      {edges.map(dir => {
        const mid = edgeMidpoint(cx, cy, dir)
        return (
          <line
            key={dir}
            x1={cx.toFixed(2)}
            y1={cy.toFixed(2)}
            x2={mid.x.toFixed(2)}
            y2={mid.y.toFixed(2)}
            className={pathClass}
          />
        )
      })}
      <circle
        cx={cx}
        cy={cy}
        r={3}
        fill="var(--neon-cyan)"
        opacity={fixed ? 0.9 : 0.7}
      />
    </>
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
