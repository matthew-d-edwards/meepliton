import { Link } from 'react-router-dom'
import { Avatar } from '@meepliton/ui'
import type { PlayerInfo } from '@meepliton/contracts'

interface RoomSubNavProps {
  gameName: string
  joinCode: string
  players: PlayerInfo[]
}

export function RoomSubNav({ gameName, joinCode, players }: RoomSubNavProps) {
  return (
    <nav className="room-subnav" aria-label="Room navigation">
      <Link to="/lobby" className="room-subnav__back" aria-label="Back to lobby">
        <svg
          xmlns="http://www.w3.org/2000/svg"
          width="14"
          height="14"
          viewBox="0 0 24 24"
          fill="none"
          stroke="currentColor"
          strokeWidth="2.5"
          strokeLinecap="round"
          strokeLinejoin="round"
          aria-hidden="true"
          focusable="false"
        >
          <polyline points="15 18 9 12 15 6" />
        </svg>
        Lobby
      </Link>

      <span className="room-subnav__title" aria-label={`Room: ${gameName}, code ${joinCode}`}>
        {gameName}
        <span className="room-subnav__code" aria-hidden="true"> · {joinCode}</span>
      </span>

      <ul className="room-subnav__players" aria-label="Players">
        {players.map(player => (
          <li key={player.id} className="room-subnav__player-slot" title={player.displayName}>
            <Avatar url={player.avatarUrl} displayName={player.displayName} size="sm" />
            <span
              className={`room-subnav__dot${player.connected ? ' room-subnav__dot--online' : ''}`}
              aria-label={player.connected ? `${player.displayName} online` : `${player.displayName} offline`}
            />
          </li>
        ))}
      </ul>
    </nav>
  )
}
