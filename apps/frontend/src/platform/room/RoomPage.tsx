import { useEffect, useRef, useState } from 'react'
import { useParams, useNavigate } from 'react-router-dom'
import * as signalR from '@microsoft/signalr'
import { useAuth } from '../auth/AuthContext'
import { gameRegistry } from '../../games/registry'
import type { GameContext, PlayerInfo } from '@meepliton/contracts'
import { RoomWaitingScreen } from '@meepliton/ui'
import { TurnIndicator } from './TurnIndicator'
import './room.css'

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
  const hubRef = useRef<signalR.HubConnection | null>(null)

  // Join by code if needed
  useEffect(() => {
    if (join && code) {
      fetch('/api/rooms/join', {
        method: 'POST',
        credentials: 'include',
        headers: { 'Content-Type': 'application/json' },
        body: JSON.stringify({ code }),
      }).then(r => r.ok ? r.json() : null)
        .then(r => r && navigate(`/room/${r.id}`, { replace: true }))
    }
  }, [join, code, navigate])

  // Load room data
  useEffect(() => {
    if (!roomId) return
    fetch(`/api/rooms/${roomId}`, { credentials: 'include' })
      .then(r => r.json())
      .then(setRoom)
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

  if (!room || !user) return <p>Loading…</p>

  if (room.status === 'Waiting') {
    return (
      <RoomWaitingScreen
        joinCode={room.joinCode}
        players={players}
        isHost={room.hostId === user.id}
        onStart={startGame}
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

  const currentPlayerId = (ctx.state as Record<string, unknown> | null)?.currentPlayerId as string | null ?? null

  return (
    <div className="room-page">
      {rejectedReason && (
        <div className="action-rejected-toast" role="alert">
          {rejectedReason}
        </div>
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
  load: () => Promise<{ default: import('@meepliton/contracts').GameModule }>
  ctx: GameContext<unknown>
}) {
  const [mod, setMod] = useState<import('@meepliton/contracts').GameModule | null>(null)
  useEffect(() => { load().then(m => setMod(m.default)) }, [load])
  if (!mod) return <p>Loading game…</p>
  const { Component } = mod
  return <Component {...(ctx as never)} />
}
