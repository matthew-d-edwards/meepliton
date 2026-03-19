import { useCallback, useEffect, useRef, useState } from 'react'
import { useParams, useNavigate, Link } from 'react-router-dom'
import * as signalR from '@microsoft/signalr'
import { useAuth } from '../auth/AuthContext'
import { gameRegistry } from '../../games/registry'
import type { GameContext, PlayerInfo } from '@meepliton/contracts'
import { TurnIndicator } from './TurnIndicator'
import './room.css'
import { ThemeToggle } from '../theme/ThemeToggle'
import { RoomWaitingScreen, ActionRejectedToast } from '@meepliton/ui'

interface RoomData {
  id: string
  gameId: string
  hostId: string
  joinCode: string
  status: 'Waiting' | 'InProgress' | 'Finished'
  gameState: unknown
  stateVersion: number
}

export default function RoomPage({ join }: { join?: boolean }) {
  const { roomId, code } = useParams()
  const { user } = useAuth()
  const navigate = useNavigate()

  const [room, setRoom] = useState<RoomData | null>(null)
  const [players, setPlayers] = useState<PlayerInfo[]>([])
  const [gameState, setGameState] = useState<unknown>(null)
  const [rejectedReason, setRejectedReason] = useState<string | null>(null)
  const [minPlayers, setMinPlayers] = useState(2)
  const hubRef = useRef<signalR.HubConnection | null>(null)

  // Join by code if needed
  useEffect(() => {
    if (join && code) {
      fetch('/api/rooms/join', {
        method: 'POST',
        credentials: 'include',
        headers: { 'Content-Type': 'application/json' },
        body: JSON.stringify({ code }),
      }).then(async r => {
        if (!r.ok) {
          navigate(`/room-not-found?code=${encodeURIComponent(code)}`, { replace: true })
          return
        }
        const data = await r.json() as { id: string }
        navigate(`/room/${data.id}`, { replace: true })
      }).catch(() => {
        navigate(`/room-not-found?code=${encodeURIComponent(code)}`, { replace: true })
      })
    }
  }, [join, code, navigate])

  // Load room data, initial player list, and game info
  useEffect(() => {
    if (!roomId) return
    fetch(`/api/rooms/${roomId}`, { credentials: 'include' })
      .then(r => {
        if (!r.ok) { navigate('/lobby', { replace: true }); return Promise.reject() }
        return r.json()
      })
      .then((loadedRoom: RoomData) => {
        setRoom(loadedRoom)

        // Load initial player list
        fetch(`/api/rooms/${roomId}/players`, { credentials: 'include' })
          .then(r => r.json())
          .then((data: Array<Omit<PlayerInfo, 'connected'>>) =>
            setPlayers(data.map(p => ({ ...p, connected: true })))
          )
          .catch(() => { /* player list is non-fatal — game can still load */ })

        // Load game info to get minPlayers
        fetch('/api/lobby', { credentials: 'include' })
          .then(r => r.json())
          .then((data: { games?: Array<{ gameId: string; minPlayers: number }> }) => {
            const game = data.games?.find(g => g.gameId === loadedRoom.gameId)
            if (game) setMinPlayers(game.minPlayers)
          })
          .catch(() => { /* minPlayers is non-fatal — defaults to 2 */ })
      })
      .catch(() => { /* navigation already handled above for non-ok responses */ })
  }, [roomId])

  // Connect SignalR
  useEffect(() => {
    if (!roomId) return
    const hub = new signalR.HubConnectionBuilder()
      .withUrl('/hubs/game', { withCredentials: true })
      .withAutomaticReconnect()
      .build()

    hub.on('StateUpdated', (state) => { setGameState(state); setRejectedReason(null) })
    hub.on('ActionRejected', ({ reason }: { reason: string }) => setRejectedReason(reason))
    hub.on('PlayerJoined', (p: PlayerInfo) => setPlayers(prev => [...prev.filter(x => x.id !== p.id), p]))
    hub.on('PlayerLeft', (playerId: string) => setPlayers(prev => prev.filter(x => x.id !== playerId)))
    hub.on('PlayerConnected', (playerId: string) =>
      setPlayers(prev => prev.map(p => p.id === playerId ? { ...p, connected: true } : p))
    )
    hub.on('PlayerDisconnected', (playerId: string) =>
      setPlayers(prev => prev.map(p => p.id === playerId ? { ...p, connected: false } : p))
    )
    hub.on('GameStarted', () => setRoom(r => r ? { ...r, status: 'InProgress' } : r))

    hub.start().then(() => hub.invoke('JoinRoom', roomId))
    hubRef.current = hub

    return () => { hub.stop() }
  }, [roomId])

  const dismissRejection = useCallback(() => setRejectedReason(null), [])

  function dispatch(action: unknown) {
    hubRef.current?.invoke('SendAction', roomId, action)
  }

  async function startGame() {
    await fetch(`/api/rooms/${roomId}/start`, { method: 'POST', credentials: 'include' })
  }

  async function removePlayer(playerId: string) {
    await fetch(`/api/rooms/${roomId}/players/${playerId}`, {
      method: 'DELETE',
      credentials: 'include',
    })
  }
          
  if (!room || !user) return <RoomLoadingScreen />

  if (room.status === 'Waiting') {
    return (
      <RoomWaitingScreen
        joinCode={room.joinCode}
        players={players}
        isHost={room.hostId === user.id}
        onStart={startGame}
        minPlayers={minPlayers}
        onRemovePlayer={room.hostId === user.id ? removePlayer : undefined}
      />
    )
  }

  // Lazy-load the game component
  const loadGame = gameRegistry[room.gameId]
  if (!loadGame) return <UnknownGameScreen gameId={room.gameId} />

  const ctx: GameContext<unknown> = {
    state:      gameState ?? room.gameState,
    players,
    myPlayerId: user.id,
    roomId:     room.id,
    dispatch,
  }

  const currentPlayerId = (ctx.state as Record<string, unknown> | null)?.currentPlayerId as string | null ?? null

  return (
    <div className="room-page">
      <div style={{ display: 'flex', justifyContent: 'flex-end', padding: 'var(--space-2) var(--space-4)' }}>
        <ThemeToggle />
      </div>
      {rejectedReason && (
        <ActionRejectedToast
          reason={rejectedReason}
          onDismiss={dismissRejection}
        />
      )}
      <TurnIndicator currentPlayerId={currentPlayerId} players={players} myPlayerId={user.id} />
      <GameLoader load={loadGame} ctx={ctx} />
    </div>
  )
}

function GameLoader({
  load,
  ctx,
}: {
  // eslint-disable-next-line @typescript-eslint/no-explicit-any
  load: () => Promise<{ default: import('@meepliton/contracts').GameModule<any> }>
  ctx: GameContext<unknown>
}) {
  // eslint-disable-next-line @typescript-eslint/no-explicit-any
  const [mod, setMod] = useState<import('@meepliton/contracts').GameModule<any> | null>(null)
  useEffect(() => { load().then(m => setMod(m.default)) }, [load])
  if (!mod) return <RoomLoadingScreen label="Loading game…" />
  const { Component } = mod
  // eslint-disable-next-line @typescript-eslint/no-explicit-any
  return <Component {...(ctx as any)} />
}

/**
 * Styled full-page loading screen shown while room data or a game module loads.
 * Uses design token CSS variables — no raw values.
 */
function RoomLoadingScreen({ label = 'Loading room…' }: { label?: string }) {
  return (
    <div
      style={{
        display: 'flex',
        flexDirection: 'column',
        alignItems: 'center',
        justifyContent: 'center',
        minHeight: '60vh',
        gap: 'var(--space-5)',
      }}
      role="status"
      aria-live="polite"
      aria-label={label}
    >
      {/* Spinner ring */}
      <span
        aria-hidden="true"
        className="room-spinner"
      />
      <span
        aria-hidden="true"
        style={{
          fontFamily: 'var(--font-display)',
          fontSize: '.75rem',
          fontWeight: 700,
          letterSpacing: '3px',
          textTransform: 'uppercase',
          color: 'var(--text-primary)',
        }}
      >
        {label}
      </span>
    </div>
  )
}

/**
 * Error screen shown when a room's gameId has no registered frontend module.
 */
function UnknownGameScreen({ gameId }: { gameId: string }) {
  return (
    <main
      className="container"
      style={{
        paddingTop: 'var(--space-12)',
        paddingBottom: 'var(--space-12)',
        display: 'flex',
        flexDirection: 'column',
        alignItems: 'center',
        gap: 'var(--space-6)',
        textAlign: 'center',
      }}
    >
      <div
        aria-hidden="true"
        style={{
          fontFamily: 'var(--font-display)',
          fontSize: '2.5rem',
          fontWeight: 900,
          color: 'var(--neon-orange)',
          textShadow:
            'var(--glow-sm) var(--neon-orange), var(--glow-md) color-mix(in srgb, var(--neon-orange) 30%, transparent)',
          letterSpacing: '3px',
        }}
      >
        !
      </div>

      <h1
        style={{
          fontFamily: 'var(--font-display)',
          fontWeight: 700,
          fontSize: 'clamp(1.1rem, 3vw, 1.5rem)',
          color: 'var(--text-bright)',
          letterSpacing: '2px',
          textTransform: 'uppercase',
        }}
      >
        Game not available
      </h1>

      <p
        style={{
          fontFamily: 'var(--font-body)',
          color: 'var(--text-primary)',
          fontSize: '1rem',
          maxWidth: '380px',
          lineHeight: 1.6,
        }}
      >
        The game{' '}
        <span
          style={{
            fontFamily: 'var(--font-mono)',
            color: 'var(--accent)',
            textShadow: 'var(--glow-sm) var(--accent-glow)',
          }}
        >
          {gameId}
        </span>{' '}
        isn't installed. Ask the host to check the game ID, or go back to the lobby.
      </p>

      <Link to="/lobby" className="btn btn-secondary">
        Back to lobby
      </Link>
    </main>
  )
}
