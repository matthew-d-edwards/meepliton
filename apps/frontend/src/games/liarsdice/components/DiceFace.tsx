import { useId } from 'react'
import styles from '../styles.module.css'

type DiceValue = 1 | 2 | 3 | 4 | 5 | 6
type DiceSize = 'sm' | 'md' | 'lg'

interface Props {
  value: DiceValue
  size?: DiceSize
  highlighted?: boolean
  wild?: boolean
}

const SIZE_PX: Record<DiceSize, number> = {
  sm: 32,
  md: 48,
  lg: 64,
}

// Pip positions as [cx, cy] in a 0–1 coordinate space (scaled to die size)
// The die face is 1×1 with 0.15 padding on each side, pip positions are relative
const PIP_LAYOUTS: Record<DiceValue, [number, number][]> = {
  1: [[0.5, 0.5]],
  2: [
    [0.25, 0.25],
    [0.75, 0.75],
  ],
  3: [
    [0.25, 0.25],
    [0.5, 0.5],
    [0.75, 0.75],
  ],
  4: [
    [0.25, 0.25],
    [0.75, 0.25],
    [0.25, 0.75],
    [0.75, 0.75],
  ],
  5: [
    [0.25, 0.25],
    [0.75, 0.25],
    [0.5, 0.5],
    [0.25, 0.75],
    [0.75, 0.75],
  ],
  6: [
    [0.25, 0.25],
    [0.75, 0.25],
    [0.25, 0.5],
    [0.75, 0.5],
    [0.25, 0.75],
    [0.75, 0.75],
  ],
}

// Skull indicator dimensions per die size — positioned at top-right corner
// md (48px) baseline: eyes at (36,8) and (42,8) r=3, jaw arc M 33 14 Q 39 18 45 14
// Scaled proportionally for sm and lg
interface SkullDims {
  eye1: [number, number]
  eye2: [number, number]
  eyeR: number
  jawPath: string
}

function getSkullDims(px: number): SkullDims {
  const scale = px / 48
  const eye1x = 36 * scale
  const eye1y = 8 * scale
  const eye2x = 42 * scale
  const eye2y = 8 * scale
  const eyeR = 3 * scale
  const j = {
    x1: 33 * scale,
    y1: 14 * scale,
    cx: 39 * scale,
    cy: 18 * scale,
    x2: 45 * scale,
    y2: 14 * scale,
  }
  const jawPath = `M ${j.x1} ${j.y1} Q ${j.cx} ${j.cy} ${j.x2} ${j.y2}`
  return { eye1: [eye1x, eye1y], eye2: [eye2x, eye2y], eyeR, jawPath }
}

export function DiceFace({ value, size = 'md', highlighted = false, wild = false }: Props) {
  const uid = useId()
  const px = SIZE_PX[size]
  const pipR = px * 0.08
  const cornerR = px * 0.1
  const pips = PIP_LAYOUTS[value]
  const label = wild ? `Die showing ${value}, wild` : `Die showing ${value}`
  const skull = getSkullDims(px)

  return (
    <svg
      width={px}
      height={px}
      viewBox={`0 0 ${px} ${px}`}
      aria-label={label}
      role="img"
      className={`${styles.diceFace} ${highlighted ? styles.diceFaceHighlighted : ''} ${wild ? styles.diceFaceWild : ''}`}
    >
      <defs>
        <radialGradient id={`${uid}-body`} cx="35%" cy="30%" r="65%">
          <stop offset="0%" stopColor="var(--pirate-bone-light, #f5ead6)" />
          <stop offset="50%" stopColor="var(--pirate-bone-mid, #d4b896)" />
          <stop offset="100%" stopColor="var(--pirate-bone-shadow, #8a6a3e)" />
        </radialGradient>
        <radialGradient id={`${uid}-pip`}>
          <stop offset="0%" stopColor="var(--pirate-ink, #2a1506)" />
          <stop offset="70%" stopColor="#5c3010" />
          <stop offset="100%" stopColor="transparent" />
        </radialGradient>
        {highlighted && (
          <filter id={`${uid}-glow`}>
            <feGaussianBlur stdDeviation="2" result="blur" />
            <feMerge>
              <feMergeNode in="blur" />
              <feMergeNode in="SourceGraphic" />
            </feMerge>
          </filter>
        )}
      </defs>

      {/* Die body */}
      <rect
        x={1}
        y={1}
        width={px - 2}
        height={px - 2}
        rx={cornerR}
        ry={cornerR}
        fill={`url(#${uid}-body)`}
        stroke="var(--color-border, var(--edge-strong))"
        strokeWidth="1.5"
      />

      {/* Highlighted border overlay */}
      {highlighted && (
        <rect
          x={1}
          y={1}
          width={px - 2}
          height={px - 2}
          rx={cornerR}
          ry={cornerR}
          fill="none"
          className={styles.diceFaceHighlightBorder}
          filter={`url(#${uid}-glow)`}
        />
      )}

      {/* Pips */}
      {pips.map(([cx, cy], i) => (
        <circle
          key={i}
          cx={cx * px}
          cy={cy * px}
          r={pipR}
          fill={wild ? 'var(--pirate-candle, #e8a430)' : `url(#${uid}-pip)`}
        />
      ))}

      {/* Wild indicator: small inline skull at top-right of die */}
      {wild && (
        <g aria-hidden="true">
          <circle
            cx={skull.eye1[0]}
            cy={skull.eye1[1]}
            r={skull.eyeR}
            fill="var(--pirate-candle, #e8a430)"
          />
          <circle
            cx={skull.eye2[0]}
            cy={skull.eye2[1]}
            r={skull.eyeR}
            fill="var(--pirate-candle, #e8a430)"
          />
          <path
            d={skull.jawPath}
            stroke="var(--pirate-candle, #e8a430)"
            strokeWidth={skull.eyeR * 0.6}
            fill="none"
          />
        </g>
      )}
    </svg>
  )
}
