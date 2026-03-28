import type { CSSProperties } from 'react'
import type { GameContext } from '@meepliton/contracts'
import type { LiarsDiceState, LiarsDiceAction } from '../types'
import { DiceCup } from './DiceCup'
import { BidControls } from './BidControls'
import { GameStatus } from './GameStatus'
import '../liarsdice.css'
import styles from '../styles.module.css'
import feltUrl from '../felt-pattern.svg?url'

export default function LiarsDiceGame({ state, myPlayerId, dispatch }: GameContext<LiarsDiceState>) {
  const currentPlayer = state.players[state.currentPlayerIndex]
  const isMyTurn = currentPlayer?.id === myPlayerId
  const me = state.players.find(p => p.id === myPlayerId)

  function send(action: LiarsDiceAction) {
    dispatch(action)
  }

  // Build a map of playerId → revealed dice for the Reveal phase
  const revealMap = new Map<string, number[]>()
  if (state.lastReveal !== null) {
    for (const pr of state.lastReveal.players) {
      revealMap.set(pr.playerId, pr.dice)
    }
  }

  // Render opponents first, me last
  const opponents = state.players.filter(p => p.id !== myPlayerId)
  const mePlayer = state.players.find(p => p.id === myPlayerId)
  const orderedPlayers = [...opponents, ...(mePlayer ? [mePlayer] : [])]

  return (
    <div data-game-theme="pirates" className={styles.root}>
      {/* Game status panel */}
      <GameStatus state={state} myPlayerId={myPlayerId} />

      {/* Player cups area */}
      <div
        className={styles.cupsGrid}
        style={{
          '--felt-texture-url': `url(${feltUrl})`,
          '--seat-index': 0,
        } as CSSProperties}
        data-stagger
      >
        {orderedPlayers.map(player => {
          const isMe = player.id === myPlayerId
          const revealDice = revealMap.get(player.id)

          return (
            <DiceCup
              key={player.id}
              player={player}
              isMe={isMe}
              phase={state.phase}
              currentBid={state.currentBid}
              palificoActive={state.palificoActive}
              isCurrentPlayer={player.id === currentPlayer?.id}
              revealDice={revealDice}
              style={{ '--seat-index': player.seatIndex } as CSSProperties}
            />
          )
        })}
      </div>

      {/* Bid controls — only shown when it is the local player's turn during Bidding */}
      {state.phase === 'Bidding' && isMyTurn && me !== undefined && me.active && (
        <BidControls
          currentBid={state.currentBid}
          players={state.players}
          me={me}
          dispatch={send}
        />
      )}

      {/* Next Round button — visible to all active players during Reveal */}
      {state.phase === 'Reveal' && me !== undefined && me.active && (
        <div className={styles.nextRoundArea}>
          <button
            type="button"
            className={`${styles.bidActionBtn} ${styles.bidActionBtnNext}`}
            onClick={() => send({ type: 'StartNextRound' })}
            style={{ minHeight: 44 }}
          >
            Next Round
          </button>
        </div>
      )}

      {/* Finished state */}
      {state.phase === 'Finished' && (
        <div className={styles.finishedBanner}>
          Game over
        </div>
      )}
    </div>
  )
}
