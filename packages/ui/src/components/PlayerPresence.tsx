import type { PlayerInfo } from '@meepliton/contracts'

interface Props { players: PlayerInfo[] }

export function PlayerPresence({ players }: Props) {
  return (
    <ul className="player-presence">
      {players.map(p => (
        <li key={p.id} className={`player-presence__player ${p.connected ? 'connected' : 'disconnected'}`}>
          {p.avatarUrl && <img src={p.avatarUrl} alt="" className="player-presence__avatar" />}
          <span>{p.displayName}</span>
          <span className="player-presence__dot" aria-hidden="true" />
          <span className="sr-only">{p.connected ? 'Online' : 'Offline'}</span>
        </li>
      ))}
    </ul>
  )
}
