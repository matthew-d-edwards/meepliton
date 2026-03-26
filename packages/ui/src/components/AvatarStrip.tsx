import type { PlayerInfo } from '@meepliton/contracts'
import { Avatar } from './Avatar'
import styles from './AvatarStrip.module.css'

interface AvatarStripProps {
  players: PlayerInfo[]
  max?: number
}

export function AvatarStrip({ players, max = 5 }: AvatarStripProps) {
  const visiblePlayers = players.length > max ? players.slice(0, max - 1) : players
  const overflowCount = players.length > max ? players.length - (max - 1) : 0

  return (
    <>
      <div className={styles.strip} role="presentation">
        {visiblePlayers.map(player => (
          <div key={player.id} className={styles.unit}>
            <Avatar
              url={player.avatarUrl}
              displayName={player.displayName}
              size="sm"
            />
            <span
              aria-hidden="true"
              className={`${styles.dot} ${player.connected ? styles['dot--online'] : styles['dot--offline']}`}
            />
          </div>
        ))}
        {overflowCount > 0 && (
          <div
            className={`${styles.unit} ${styles.overflow}`}
            aria-label={`and ${overflowCount} more player${overflowCount !== 1 ? 's' : ''}`}
          >
            +{overflowCount}
          </div>
        )}
      </div>
      <ul className={styles.srOnly}>
        {players.map(player => (
          <li key={player.id}>
            {player.displayName} — {player.connected ? 'Online' : 'Offline'}
          </li>
        ))}
      </ul>
    </>
  )
}
