import { useEffect, useRef, useState } from 'react'
import { useParams, useNavigate } from 'react-router-dom'
import * as signalR from '@microsoft/signalr'
import { useAuth } from '../auth/AuthContext'
import { gameRegistry } from '../../games/registry'
import type { GameContext, PlayerInfo } from '@meepliton/contracts'
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
  const [roomNotFound, setRoomNotFound] = useState(false)
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
        if (r.status === 404) {
          setRoomNotFound(true)
          return
        }
        if (r.ok) {
          const data = await r.json() as { id: string }
          navigate(`/room/${data.id}`, { replace: true })
        }
      })
    }
  }, [join, code, navigate])

  // Load room data, initial player list, and game info
  useEffect(() => {
    if (!roomId) return
    fetch(`/api/rooms/${roomId}`, { credentials: 'include' })
      .then(r => r.json())
      .then((loadedRoom: RoomData) => {
        setRoom(loadedRoom)

        // Load initial player list
        fetch(`/api/rooms/${roomId}/players`, { credentials: 'include' })
          .then(r => r.json())
          .then((data: Array<Omit<PlayerInfo, 'connected'>>) =>
            setPlayers(data.map(p => ({ ...p, connected: true })))
          )

        // Load game info to get minPlayers
        fetch('/api/lobby', { credentials: 'include' })
          .then(r => r.json())
          .then((data: { games?: Array<{ gameId: string; minPlayers: number }> }) => {
            const game = data.games?.find(g => g.gameId === loadedRoom.gameId)
            if (game) setMinPlayers(game.minPlayers)
          })
      })
  }, [roomId])

  // Auto-dismiss action rejected toast after 4 seconds
  useEffect(() => {
    if (!rejectedReason) return
    const t = setTimeout(() => setRejectedReason(null), 4000)
    return () => clearTimeout(t)
  }, [rejectedReason])

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
          
  if (roomNotFound) {
    return (
      <main className="room-not-found">
        <h2>Room not found</h2>
        <p>This join link is invalid or the room no longer exists.</p>
        <a href="/lobby">Back to lobby</a>
      </main>
    )
  }

  if (!room || !user) return <p>Loading…</p>

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
  if (!loadGame) return <p>Unknown game: {room.gameId}</p>

  const ctx: GameContext<unknown> = {
    state:      gameState ?? room.gameState,
    players,
    myPlayerId: user.id,
    roomId:     room.id,
    dispatch,
  }

  return (
    <div className="room-page">
      <div style={{ display: 'flex', justifyContent: 'flex-end', padding: 'var(--space-2) var(--space-4)' }}>
        <ThemeToggle />
      </div>
      {rejectedReason && (
        <ActionRejectedToast
          reason={rejectedReason}
          onDismiss={() => setRejectedReason(null)}
        />
      )}
      <GameLoader load={loadGame} ctx={ctx} />
    </div>
  )
}

function GameLoader({
  load,
  ctx,
}: {
  load: () => Promise<{ default: import('@meepliton/contracts').GameModule }>
  ctx: GameContext<unknown>
}) {
  const [mod, setMod] = useState<import('@meepliton/contracts').GameModule | null>(null)
  useEffect(() => { load().then(m => setMod(m.default)) }, [load])
  if (!mod) return <p>Loading game…</p>
  const { Component } = mod
  return <Component {...(ctx as never)} />
}
