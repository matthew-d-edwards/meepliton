import { useEffect, useState } from 'react'

interface AvatarProps {
  url: string | null | undefined
  displayName: string
  size: 'sm' | 'md'
}

const SIZE_PX: Record<AvatarProps['size'], number> = {
  sm: 32,
  md: 48,
}

function getInitials(displayName: string): string {
  const parts = displayName.trim().split(/\s+/).filter(Boolean)
  if (parts.length === 0) return '?'
  if (parts.length === 1) return parts[0].slice(0, 2).toUpperCase()
  return (parts[0][0] + parts[parts.length - 1][0]).toUpperCase()
}

/**
 * Avatar — displays a user's profile image or an initials fallback.
 *
 * - When `url` is present, renders an <img> with alt="" (decorative).
 * - If the image fails to load, or `url` is null/undefined, renders an
 *   initials placeholder with aria-hidden="true". The parent is responsible
 *   for providing an accessible label (e.g. the player's display name nearby).
 */
export function Avatar({ url, displayName, size }: AvatarProps) {
  const [imgFailed, setImgFailed] = useState(false)
  useEffect(() => setImgFailed(false), [url])
  const px = SIZE_PX[size]

  const baseStyle: React.CSSProperties = {
    width: px,
    height: px,
    borderRadius: 'var(--radius-pill)',
    flexShrink: 0,
    display: 'inline-flex',
    alignItems: 'center',
    justifyContent: 'center',
    overflow: 'hidden',
  }

  if (url && !imgFailed) {
    return (
      <img
        src={url}
        alt=""
        width={px}
        height={px}
        style={baseStyle}
        onError={() => setImgFailed(true)}
      />
    )
  }

  const fontSize = size === 'sm' ? '0.6875rem' : '1rem'

  return (
    <div
      aria-hidden="true"
      style={{
        ...baseStyle,
        background: 'var(--accent-dim)',
        color: 'var(--text-bright)',
        fontFamily: 'var(--font-display)',
        fontSize,
        fontWeight: 700,
        letterSpacing: '0.03em',
        userSelect: 'none',
      }}
    >
      {getInitials(displayName)}
    </div>
  )
}
