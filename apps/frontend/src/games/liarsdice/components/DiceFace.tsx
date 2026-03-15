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

export function DiceFace({ value, size = 'md', highlighted = false, wild = false }: Props) {
  const px = SIZE_PX[size]
  const pipR = px * 0.08
  const cornerR = px * 0.1
  const pips = PIP_LAYOUTS[value]

  return (
    <svg
      width={px}
      height={px}
      viewBox={`0 0 ${px} ${px}`}
      aria-label={`Die showing ${value}`}
      role="img"
      className={`${styles.diceFace} ${highlighted ? styles.diceFaceHighlighted : ''} ${wild ? styles.diceFaceWild : ''}`}
    >
      {/* Die body */}
      <rect
        x={1}
        y={1}
        width={px - 2}
        height={px - 2}
        rx={cornerR}
        ry={cornerR}
        className={styles.diceFaceBody}
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
        />
      )}

      {/* Pips */}
      {pips.map(([cx, cy], i) => (
        <circle
          key={i}
          cx={cx * px}
          cy={cy * px}
          r={pipR}
          className={styles.diceFacePip}
        />
      ))}

      {/* Wild indicator: small star/asterisk in corner when value===1 and wild */}
      {wild && (
        <text
          x={px - 4}
          y={9}
          textAnchor="end"
          fontSize={px * 0.2}
          className={styles.diceFaceWildMark}
          aria-label="wild"
        >
          ★
        </text>
      )}
    </svg>
  )
}
