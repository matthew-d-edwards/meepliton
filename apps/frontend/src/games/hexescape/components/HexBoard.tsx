import type { HexEscapeState, HexTileType, HexCell } from '../types'
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
 * For a flat-top hex, direction d points to the angle (60 * d) degrees from centre.
 * The edge midpoint toward direction d is at angle (60 * d)° from centre at distance HEX_SIZE.
 * We draw a line from centre to that midpoint.
 */
function edgeMidpoint(cx: number, cy: number, dir: number): { x: number; y: number } {
  // In flat-top, corner i is at 60*i degrees.
  // The midpoint of the edge between corners i and (i+1) is at (60*i + 30) degrees at radius HEX_SIZE * cos(30°).
  // But we want the line from centre to the midpoint of the relevant side.
  // For axial direction d, the neighbour is at offset DIR_OFFSETS[d].
  // The edge shared with direction-d neighbour lies between the two corners bounding that side.
  // In flat-top: the six edges correspond to directions 0-5.
  // Edge for direction d lies between corners d and (d+1)%6 (using flat-top corner angles).
  const angle = (Math.PI / 180) * (60 * dir)
  // Inradius of flat-top hex = HEX_SIZE * cos(30°) = HEX_SIZE * sqrt(3)/2
  const inradius = HEX_SIZE * (SQRT3 / 2)
  return {
    x: cx + inradius * Math.cos(angle),
    y: cy + inradius * Math.sin(angle),
  }
}

/** Parse "q,r" → {q, r} */
function parseCoord(key: string): { q: number; r: number } {
  const [qs, rs] = key.split(',')
  return { q: parseInt(qs, 10), r: parseInt(rs, 10) }
}

interface HexBoardProps {
  state: HexEscapeState
  onCellClick: (coord: string) => void
  canInteract: boolean
}

export function HexBoard({ state, onCellClick, canInteract }: HexBoardProps) {
  const { cells, walls, grid, survivorStartCells, exitCell } = state

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

  const wallSet = new Set(walls)
  const survivorSet = new Set(survivorStartCells)

  return (
    <div className={styles.boardWrap}>
      <svg
        className={styles.boardSvg}
        viewBox={`${vbX.toFixed(1)} ${vbY.toFixed(1)} ${vbW.toFixed(1)} ${vbH.toFixed(1)}`}
        width={Math.min(600, vbW * 2)}
        height={Math.min(500, vbH * 2)}
        aria-label="Hex Escape board"
        role="group"
      >
        <title>Hex Escape game board</title>

        {cells.map(key => {
          const pos = positions.get(key)
          if (!pos) return null
          const { x, y } = pos

          const isWall = wallSet.has(key)
          const isSurvivor = survivorSet.has(key)
          const isExit = key === exitCell
          const cell: HexCell | undefined = grid[key]
          const isOccupied = cell !== undefined
          const isFixed = cell?.fixed ?? false

          // Determine polygon class
          let polyClass = styles.hexEmpty
          if (isWall) polyClass = styles.hexWall
          else if (isFixed) polyClass = styles.hexFixed
          else if (isOccupied) polyClass = styles.hexPlaced
          else if (isSurvivor) polyClass = styles.hexSurvivor
          else if (isExit) polyClass = styles.hexExit

          const isClickable = canInteract && !isWall
          const cornersStr = hexCorners(x, y)

          return (
            <g
              key={key}
              className={[styles.hexBase, isClickable ? styles.hexClickable : ''].filter(Boolean).join(' ')}
              onClick={isClickable ? () => onCellClick(key) : undefined}
              role={isClickable ? 'button' : undefined}
              tabIndex={isClickable ? 0 : undefined}
              aria-label={isClickable ? hexAriaLabel(key, isWall, isSurvivor, isExit, cell) : undefined}
              onKeyDown={isClickable ? (e) => {
                if (e.key === 'Enter' || e.key === ' ') {
                  e.preventDefault()
                  onCellClick(key)
                }
              } : undefined}
            >
              <polygon
                points={cornersStr}
                className={polyClass}
              />

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

              {/* Survivor marker */}
              {isSurvivor && !isOccupied && (
                <text x={x} y={y} className={styles.survivorIcon} aria-hidden="true">S</text>
              )}
              {isSurvivor && isOccupied && (
                <text x={x} y={y - HEX_SIZE * 0.5} className={styles.survivorIcon} aria-hidden="true" style={{ fontSize: '8px' }}>S</text>
              )}

              {/* Exit marker */}
              {isExit && !isOccupied && (
                <text x={x} y={y} className={styles.exitIcon} aria-hidden="true">E</text>
              )}
              {isExit && isOccupied && (
                <text x={x} y={y - HEX_SIZE * 0.5} className={styles.exitIcon} aria-hidden="true" style={{ fontSize: '8px' }}>E</text>
              )}

              {/* Wall marker */}
              {isWall && (
                <text x={x} y={y} className={styles.wallX} aria-hidden="true">X</text>
              )}

              {/* Cell coord label (dev aid — small, non-distracting) */}
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

  // For each open edge, draw a line from center to the edge midpoint
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
      {/* Central dot */}
      <circle
        cx={cx}
        cy={cy}
        r={3}
        fill={fixed ? 'var(--neon-cyan)' : 'var(--neon-cyan)'}
        opacity={fixed ? 0.9 : 0.7}
      />
    </>
  )
}

// ── Aria label helper ─────────────────────────────────────────────────────────

function hexAriaLabel(
  key: string,
  isWall: boolean,
  isSurvivor: boolean,
  isExit: boolean,
  cell: HexCell | undefined
): string {
  const parts = [`Cell ${key}`]
  if (isWall) parts.push('wall')
  if (isSurvivor) parts.push('survivor start')
  if (isExit) parts.push('exit')
  if (cell) {
    parts.push(`${cell.tileType} tile, rotation ${cell.rotation}`)
    if (cell.fixed) parts.push('fixed')
  } else if (!isWall) {
    parts.push('empty')
  }
  return parts.join(', ')
}

// Re-export for use in Game component
export { parseCoord }
