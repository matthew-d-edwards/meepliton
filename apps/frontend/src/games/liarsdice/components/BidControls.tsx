import { useState } from 'react'
import type { Bid, LiarsDiceAction, DicePlayer } from '../types'
import { DiceFace } from './DiceFace'
import styles from '../styles.module.css'

interface Props {
  currentBid: Bid | null
  players: DicePlayer[]
  me: DicePlayer
  dispatch: (action: LiarsDiceAction) => void
}

function isBidHigher(candidate: Bid, current: Bid | null): boolean {
  if (current === null) return true
  if (candidate.quantity > current.quantity) return true
  if (candidate.quantity === current.quantity && candidate.face > current.face) return true
  return false
}

const FACE_VALUES = [1, 2, 3, 4, 5, 6] as const

export function BidControls({ currentBid, players, me, dispatch }: Props) {
  const totalActiveDice = players.filter(p => p.active).reduce((sum, p) => sum + p.diceCount, 0)

  // Default to a bid just above current
  const defaultQuantity = currentBid?.quantity ?? 1
  const defaultFace = currentBid !== null
    ? (currentBid.face < 6 ? currentBid.face + 1 : currentBid.face)
    : 1

  const [quantity, setQuantity] = useState<number>(defaultQuantity)
  const [face, setFace] = useState<number>(defaultFace)

  const selectedBid: Bid = { quantity, face }
  const canPlaceBid = isBidHigher(selectedBid, currentBid)
  const canCallLiar = currentBid !== null
  const showPalifico = me.diceCount === 1 && !me.hasUsedPalifico && currentBid === null

  function handlePlaceBid() {
    if (!canPlaceBid) return
    const action: LiarsDiceAction = { type: 'PlaceBid', bid: selectedBid }
    dispatch(action)
  }

  function handleCallLiar() {
    if (!canCallLiar) return
    const action: LiarsDiceAction = { type: 'CallLiar' }
    dispatch(action)
  }

  function handlePalifico() {
    const action: LiarsDiceAction = { type: 'DeclarePalifico' }
    dispatch(action)
  }

  function incrementQty() {
    setQuantity(q => Math.min(q + 1, totalActiveDice))
  }

  function decrementQty() {
    setQuantity(q => Math.max(q - 1, 1))
  }

  return (
    <div className={styles.bidControls} aria-label="Bid controls">
      {/* Quantity stepper */}
      <div className={styles.bidSection}>
        <label className={styles.bidLabel} htmlFor="bid-quantity">Quantity</label>
        <div className={styles.bidStepper} id="bid-quantity">
          <button
            type="button"
            className={styles.bidStepBtn}
            onClick={decrementQty}
            disabled={quantity <= 1}
            aria-label="Decrease quantity"
            style={{ minWidth: 44, minHeight: 44 }}
          >
            −
          </button>
          <span className={styles.bidStepValue} aria-live="polite" aria-label={`Quantity: ${quantity}`}>
            {quantity}
          </span>
          <button
            type="button"
            className={styles.bidStepBtn}
            onClick={incrementQty}
            disabled={quantity >= totalActiveDice}
            aria-label="Increase quantity"
            style={{ minWidth: 44, minHeight: 44 }}
          >
            +
          </button>
        </div>
      </div>

      {/* Face selector */}
      <div className={styles.bidSection}>
        <div className={styles.bidLabel}>Face</div>
        <div className={styles.bidFaceSelector} role="group" aria-label="Select die face">
          {FACE_VALUES.map(f => (
            <button
              key={f}
              type="button"
              className={`${styles.bidFaceBtn} ${face === f ? styles.bidFaceBtnSelected : ''}`}
              onClick={() => setFace(f)}
              aria-label={`Face ${f}`}
              aria-pressed={face === f}
              style={{ minWidth: 44, minHeight: 44 }}
            >
              <DiceFace value={f} size="sm" />
            </button>
          ))}
        </div>
      </div>

      {/* Action buttons */}
      <div className={styles.bidActions}>
        <button
          type="button"
          className={`${styles.bidActionBtn} ${styles.bidActionBtnPlace}`}
          onClick={handlePlaceBid}
          disabled={!canPlaceBid}
          aria-disabled={!canPlaceBid}
          style={{ minHeight: 44 }}
        >
          Place Bid
        </button>

        <button
          type="button"
          className={`${styles.bidActionBtn} ${styles.bidActionBtnLiar}`}
          onClick={handleCallLiar}
          disabled={!canCallLiar}
          aria-disabled={!canCallLiar}
          style={{ minHeight: 44 }}
        >
          Call Liar
        </button>
      </div>

      {/* Palifico button — only shown when eligible */}
      {showPalifico && (
        <button
          type="button"
          className={`${styles.bidActionBtn} ${styles.bidActionBtnPalifico}`}
          onClick={handlePalifico}
          style={{ minHeight: 44, width: '100%' }}
        >
          Declare Palifico
        </button>
      )}
    </div>
  )
}
