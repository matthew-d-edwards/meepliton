import { useState } from 'react'
import type { GameContext } from '@meepliton/contracts'
import type {
  SkylineState,
  SkylineAction,
  PlayerState,
  Hotel,
} from '../types'
import { HOTELS, HOTEL_LABELS } from '../types'
import '../skyline.css'
import styles from '../styles.module.css'

// ── Helpers ───────────────────────────────────────────────────────────────────

/** Stock price lookup by tier index. Tiers: 0=none,1=2tiles,2=3,3=4,4=5,5=6,6=7-10,7=11-20,8=21-30,9=31-40,10=41+ */
const LUXOR_TOWER_PRICES =        [0, 200, 300, 400, 500, 600, 700, 800, 900, 1000, 1100]
const AMERICAN_FESTIVAL_WORLDWIDE = [0, 300, 400, 500, 600, 700, 800, 900, 1000, 1100, 1200]
const CONTINENTAL_IMPERIAL =        [0, 400, 500, 600, 700, 800, 900, 1000, 1100, 1200, 1300]

function sizeTier(size: number): number {
  if (size <= 0)  return 0
  if (size <= 2)  return 1
  if (size <= 3)  return 2
  if (size <= 4)  return 3
  if (size <= 5)  return 4
  if (size <= 6)  return 5
  if (size <= 10) return 6
  if (size <= 20) return 7
  if (size <= 30) return 8
  if (size <= 40) return 9
  return 10
}

function stockPrice(hotel: string, chains: SkylineState['chains']): number {
  const chain = chains[hotel]
  if (!chain?.active) return 0
  const tier = sizeTier(chain.size)
  if (hotel === 'luxor' || hotel === 'tower')
    return LUXOR_TOWER_PRICES[tier]
  if (hotel === 'continental' || hotel === 'imperial')
    return CONTINENTAL_IMPERIAL[tier]
  return AMERICAN_FESTIVAL_WORLDWIDE[tier]
}

function netWorth(p: PlayerState, state: SkylineState): number {
  const stockVal = HOTELS.reduce((sum, h) => {
    const qty = p.stocks[h] ?? 0
    return sum + qty * stockPrice(h, state.chains)
  }, 0)
  return p.cash + stockVal
}

// ── Main component ────────────────────────────────────────────────────────────

export default function Game({ state, myPlayerId, dispatch }: GameContext<SkylineState>) {
  const [selectedHandTile, setSelectedHandTile] = useState<string | null>(null)
  const [selectedHotel, setSelectedHotel] = useState<string | null>(null)
  const [selectedSurvivor, setSelectedSurvivor] = useState<string | null>(null)
  const [buyQty, setBuyQty] = useState<Record<string, number>>({})

  // ── Derived state ──────────────────────────────────────────────────────────

  const myIdx = state.players.findIndex(p => p.id === myPlayerId)
  const me = myIdx >= 0 ? state.players[myIdx] : null
  const currentPlayer = state.players[state.currentPlayer]
  const isMyTurn = currentPlayer?.id === myPlayerId

  // ── Dispatch helpers ───────────────────────────────────────────────────────

  function send(action: SkylineAction) {
    dispatch(action)
  }

  function placeTile(tileId: string) {
    if (!isMyTurn || state.phase !== 'place') return
    send({ type: 'PlaceTile', tileId })
    setSelectedHandTile(null)
  }

  function foundHotel(hotel: string) {
    if (!isMyTurn || state.phase !== 'found') return
    send({ type: 'FoundHotel', hotel })
    setSelectedHotel(null)
  }

  function chooseSurvivor(hotel: string) {
    if (!isMyTurn || state.phase !== 'merge') return
    send({ type: 'ChooseSurvivor', hotel })
    setSelectedSurvivor(null)
  }

  function confirmSurvivor() {
    if (!isMyTurn || state.phase !== 'merge') return
    send({ type: 'ConfirmSurvivor' })
  }

  function dispose(sell: number, trade: number) {
    if (!isMyTurn || state.phase !== 'dispose') return
    send({ type: 'Dispose', sell, trade })
  }

  function buyStocks() {
    if (!isMyTurn || state.phase !== 'buy') return
    const hasPurchases = Object.values(buyQty).some(q => q > 0)
    if (!hasPurchases) {
      send({ type: 'BuyStocks', purchases: {} })
      return
    }
    send({ type: 'BuyStocks', purchases: buyQty })
    setBuyQty({})
  }

  function endTurn() {
    if (!isMyTurn || state.phase !== 'buy') return
    send({ type: 'EndTurn' })
    setBuyQty({})
  }

  function endGame() {
    if (!isMyTurn) return
    send({ type: 'EndGame' })
  }

  // ── Game over ──────────────────────────────────────────────────────────────

  if (state.gameOver) {
    const ranked = state.rankedOrder ?? state.players.map((_, i) => i)
    return (
      <div data-game-theme="skyline" className={styles.goCard}>
        <div className={styles.goTitle}>GAME OVER</div>
        <div className={styles.goWinner}>
          {ranked[0] !== undefined && state.players[ranked[0]]
            ? `${state.players[ranked[0]].name} wins!`
            : 'Game over'}
        </div>
        <div className={styles.goSubtitle}>Final standings</div>
        {ranked.map((idx, rank) => {
          const p = state.players[idx]
          if (!p) return null
          const nw = netWorth(p, state)
          return (
            <div key={p.id} className={styles.goRow}>
              <div className={styles.goRank}>{rank === 0 ? '🥇' : rank === 1 ? '🥈' : rank === 2 ? '🥉' : `#${rank + 1}`}</div>
              <div className={styles.goName}>{p.name}</div>
              <div className={styles.goNet}>${nw.toLocaleString()}</div>
              <div className={styles.goBreakdown}>
                <span className={styles.goBdItem}>
                  <span className={styles.goBdLabel}>Cash</span>
                  <span className={styles.goBdVal}>${p.cash.toLocaleString()}</span>
                </span>
                {HOTELS.filter(h => (p.stocks[h] ?? 0) > 0).map(h => (
                  <span key={h} className={styles.goBdItem}>
                    <span className={styles.goBdLabel}>{HOTEL_LABELS[h]}</span>
                    <span className={styles.goBdVal}>{p.stocks[h]}×</span>
                  </span>
                ))}
              </div>
            </div>
          )
        })}
      </div>
    )
  }

  // ── Phase label ────────────────────────────────────────────────────────────

  const PHASE_ORDER: SkylineState['phase'][] = ['place', 'found', 'merge', 'dispose', 'buy', 'draw']
  const PHASE_LABELS: Record<SkylineState['phase'], string> = {
    place: 'Place Tile', found: 'Found Hotel', merge: 'Merge', dispose: 'Dispose Stocks',
    buy: 'Buy Stocks', draw: 'Draw Tile',
  }

  // ── Waiting note (non-active player) ──────────────────────────────────────

  const waitingNote = !isMyTurn ? (
    <p className={styles.waitingNote}>
      Waiting for {currentPlayer?.name ?? 'opponent'} to {PHASE_LABELS[state.phase].toLowerCase()}…
    </p>
  ) : null

  // ── Board rendering ────────────────────────────────────────────────────────

  // Build a 9×12 grid. Rows A–I (9), Cols 1–12.
  const ROWS = ['A','B','C','D','E','F','G','H','I']
  const COLS = [1,2,3,4,5,6,7,8,9,10,11,12]

  function tileClass(tileId: string): string {
    const placed = state.board[tileId]
    if (!placed) return styles.tileEmpty
    if (placed === 'neutral') return styles.tileNeutral
    const key = `tile${placed.charAt(0).toUpperCase()}${placed.slice(1)}` as keyof typeof styles
    return styles[key] ?? styles.tileNeutral
  }

  const isPlacingPhase = isMyTurn && state.phase === 'place'

  // ── Hand section ───────────────────────────────────────────────────────────

  // In multiplayer, opponents' hands are always [] (backend projection hides them).
  // We still show face-down placeholder tiles for UX.
  const HAND_SIZE = 6

  // ── Buy stocks state ───────────────────────────────────────────────────────

  const totalBuyQty = Object.values(buyQty).reduce((s, v) => s + v, 0)
  const buyCost = HOTELS.reduce((sum, h) => sum + (buyQty[h] ?? 0) * stockPrice(h, state.chains), 0)

  // ── Dispose state ──────────────────────────────────────────────────────────
  // Dispose: current player in queue
  const disposeQueue = state.pending?.disposeQueue ?? []
  const disposeIdx = state.pending?.disposeIdx ?? 0
  const disposeItem = disposeQueue[disposeIdx]
  const disposeDefunct = disposeItem?.defunct ?? state.pending?.defunct?.[0] ?? ''
  const isMyDisposeTurn = isMyTurn && state.phase === 'dispose' && disposeItem?.playerIdx === myIdx
  const [disposeLocal, setDisposeLocal] = useState({ sell: 0, trade: 0 })
  const myDefunctStocks = me ? (me.stocks[disposeDefunct] ?? 0) : 0
  const survivorName = state.pending?.survivor ?? ''

  return (
    <div data-game-theme="skyline" className={styles.gameLayout}>
      {/* ── Main column ─────────────────────────────────────── */}
      <div className={styles.mainCol}>

        {/* Phase strip */}
        <div className={styles.phaseStrip}>
          <div className={styles.phDots}>
            {PHASE_ORDER.map((ph, i) => {
              const phIdx = PHASE_ORDER.indexOf(state.phase)
              return (
                <div
                  key={ph}
                  className={`${styles.phDot} ${i < phIdx ? styles.phDotDone : ''} ${i === phIdx ? styles.phDotActive : ''}`}
                />
              )
            })}
          </div>
          <span className={styles.phLabel}>{PHASE_LABELS[state.phase]}</span>
          <span className={styles.phCounter}>
            {isMyTurn ? 'Your turn' : `${currentPlayer?.name ?? '?'}'s turn`}
          </span>
        </div>

        {waitingNote}

        {/* ── Action panel (only shown when it's viewer's turn) ─ */}
        {isMyTurn && (
          <ActionPanel
            state={state}
            me={me}
            myIdx={myIdx}
            selectedHotel={selectedHotel}
            setSelectedHotel={setSelectedHotel}
            selectedSurvivor={selectedSurvivor}
            setSelectedSurvivor={setSelectedSurvivor}
            buyQty={buyQty}
            setBuyQty={setBuyQty}
            totalBuyQty={totalBuyQty}
            buyCost={buyCost}
            disposeLocal={disposeLocal}
            setDisposeLocal={setDisposeLocal}
            isMyDisposeTurn={isMyDisposeTurn}
            disposeDefunct={disposeDefunct}
            myDefunctStocks={myDefunctStocks}
            survivorName={survivorName}
            onFoundHotel={foundHotel}
            onChooseSurvivor={chooseSurvivor}
            onConfirmSurvivor={confirmSurvivor}
            onDispose={dispose}
            onBuyStocks={buyStocks}
            onEndTurn={endTurn}
            onEndGame={endGame}
          />
        )}

        {/* ── Board ──────────────────────────────────────────── */}
        <div className={styles.boardWrap}>
          <div className={styles.boardLabel}>Board</div>
          <div className={styles.board}>
            {ROWS.flatMap(row =>
              COLS.map(col => {
                const tileId = `${row}${col}`
                const placed = state.board[tileId]
                const isInHand = me?.hand.includes(tileId) ?? false
                const canPlace = isPlacingPhase && isInHand && selectedHandTile === tileId
                return (
                  <div
                    key={tileId}
                    className={`${styles.tile} ${tileClass(tileId)} ${canPlace ? styles.tilePlayable : ''} ${selectedHandTile === tileId && isPlacingPhase ? styles.tileSelected : ''}`}
                    onClick={() => canPlace ? placeTile(tileId) : undefined}
                    role={canPlace ? 'button' : undefined}
                    aria-label={placed ? `${tileId}: ${placed}` : isInHand && isPlacingPhase ? `Place ${tileId}` : tileId}
                  >
                    <span className={styles.tileId}>{tileId}</span>
                  </div>
                )
              })
            )}
          </div>
        </div>

        {/* ── My hand ────────────────────────────────────────── */}
        <div className={styles.handSection}>
          <div className={styles.handLabel}>
            {isMyTurn && state.phase === 'place'
              ? 'Your Hand — select a tile to place'
              : isMyTurn
              ? 'Your Hand'
              : `Waiting for ${currentPlayer?.name ?? 'opponent'}'s turn…`}
          </div>
          <div className={styles.handRow}>
            {me ? (
              me.hand.length > 0
                ? me.hand.map(tileId => (
                    <div
                      key={tileId}
                      className={`${styles.htile} ${isPlacingPhase && selectedHandTile === tileId ? styles.htileSel : ''} ${!isMyTurn || state.phase !== 'place' ? styles.htileEmpty : ''}`}
                      onClick={() => {
                        if (!isMyTurn || state.phase !== 'place') return
                        setSelectedHandTile(prev => prev === tileId ? null : tileId)
                      }}
                      role="button"
                      aria-label={`Hand tile ${tileId}`}
                      aria-pressed={selectedHandTile === tileId}
                      aria-disabled={!isMyTurn || state.phase !== 'place'}
                    >
                      {tileId}
                    </div>
                  ))
                : Array.from({ length: HAND_SIZE }, (_, i) => (
                    <div key={i} className={`${styles.htile} ${styles.htileEmpty}`} aria-label="Empty slot" />
                  ))
            ) : null}
          </div>
        </div>

        {/* ── Opponents' hidden hands ─────────────────────────── */}
        {state.players
          .filter(p => p.id !== myPlayerId)
          .map(opponent => (
            <OpponentHand key={opponent.id} opponent={opponent} handSize={HAND_SIZE} />
          ))}

      </div>

      {/* ── Sidebar ─────────────────────────────────────────── */}
      <div className={styles.sideCol}>
        <HotelChips state={state} />
        <PlayerCards state={state} myPlayerId={myPlayerId} />
        <LogSection log={state.log} />
      </div>
    </div>
  )
}

// ── Action panel ──────────────────────────────────────────────────────────────

interface ActionPanelProps {
  state: SkylineState
  me: PlayerState | null
  myIdx: number
  selectedHotel: string | null
  setSelectedHotel: (h: string | null) => void
  selectedSurvivor: string | null
  setSelectedSurvivor: (h: string | null) => void
  buyQty: Record<string, number>
  setBuyQty: (q: Record<string, number>) => void
  totalBuyQty: number
  buyCost: number
  disposeLocal: { sell: number; trade: number }
  setDisposeLocal: (d: { sell: number; trade: number }) => void
  isMyDisposeTurn: boolean
  disposeDefunct: string
  myDefunctStocks: number
  survivorName: string
  onFoundHotel: (h: string) => void
  onChooseSurvivor: (h: string) => void
  onConfirmSurvivor: () => void
  onDispose: (sell: number, trade: number) => void
  onBuyStocks: () => void
  onEndTurn: () => void
  onEndGame: () => void
}

function ActionPanel(props: ActionPanelProps) {
  const { state, me, selectedHotel, setSelectedHotel, selectedSurvivor, setSelectedSurvivor,
          buyQty, setBuyQty, totalBuyQty, buyCost, disposeLocal, setDisposeLocal,
          isMyDisposeTurn, disposeDefunct, myDefunctStocks, survivorName,
          onFoundHotel, onChooseSurvivor, onConfirmSurvivor, onDispose, onBuyStocks,
          onEndTurn, onEndGame } = props

  const MAX_BUY = 3

  // ── Found Hotel ────────────────────────────────────────────────────────────
  if (state.phase === 'found') {
    const available = HOTELS.filter(h => !state.chains[h]?.active)
    return (
      <div className={styles.actionCard}>
        <div className={styles.actionTitle}>Found a Hotel Chain</div>
        <div className={styles.actionSub}>Select the hotel chain to found at this location.</div>
        <div className={styles.foundGrid}>
          {available.map(h => (
            <button
              key={h}
              className={`${styles.foundBtn} ${selectedHotel === h ? styles.foundBtnSel : ''}`}
              style={{ color: `var(--hotel-${h})`, borderColor: selectedHotel === h ? `var(--hotel-${h})` : 'transparent' }}
              onClick={() => setSelectedHotel(selectedHotel === h ? null : h)}
            >
              {HOTEL_LABELS[h as Hotel]}
            </button>
          ))}
        </div>
        <div className={styles.btnRow}>
          <button
            className={`${styles.btn} ${styles.btnPrimary}`}
            disabled={!selectedHotel}
            onClick={() => selectedHotel && onFoundHotel(selectedHotel)}
          >
            Found {selectedHotel ? HOTEL_LABELS[selectedHotel as Hotel] : '…'}
          </button>
        </div>
      </div>
    )
  }

  // ── Choose Survivor ────────────────────────────────────────────────────────
  if (state.phase === 'merge' && !state.pending?.survivorChosen) {
    const survivors = state.pending?.survivors ?? []
    return (
      <div className={styles.actionCard}>
        <div className={styles.actionTitle}>Choose Surviving Chain</div>
        <div className={styles.actionSub}>Tie — choose which chain absorbs the others.</div>
        {survivors.map(h => (
          <div
            key={h}
            className={`${styles.mergeOpt} ${selectedSurvivor === h ? styles.mergeOptSel : ''}`}
            onClick={() => setSelectedSurvivor(selectedSurvivor === h ? null : h)}
            role="button"
            aria-pressed={selectedSurvivor === h}
          >
            <div className={styles.mergeDot} style={{ background: `var(--hotel-${h})` }} />
            <span>{HOTEL_LABELS[h as Hotel]}</span>
            <span style={{ marginLeft: 'auto', fontSize: '0.75rem', opacity: 0.7 }}>
              {state.chains[h]?.size ?? 0} tiles
            </span>
          </div>
        ))}
        <div className={styles.btnRow}>
          <button
            className={`${styles.btn} ${styles.btnPrimary}`}
            disabled={!selectedSurvivor}
            onClick={() => selectedSurvivor && onChooseSurvivor(selectedSurvivor)}
          >
            Confirm survivor
          </button>
        </div>
      </div>
    )
  }

  // ── Confirm Survivor ───────────────────────────────────────────────────────
  if (state.phase === 'merge' && state.pending?.survivorChosen && !state.pending?.defunct?.length) {
    return (
      <div className={styles.actionCard}>
        <div className={styles.actionTitle}>Merge Confirmed</div>
        <div className={styles.actionSub}>Survivor: {survivorName ? HOTEL_LABELS[survivorName as Hotel] ?? survivorName : '?'}</div>
        <div className={styles.btnRow}>
          <button className={`${styles.btn} ${styles.btnPrimary}`} onClick={onConfirmSurvivor}>
            Continue
          </button>
        </div>
      </div>
    )
  }

  // ── Dispose Stocks ─────────────────────────────────────────────────────────
  if (state.phase === 'dispose') {
    if (!isMyDisposeTurn) {
      return (
        <div className={styles.actionCard}>
          <div className={styles.actionTitle}>Disposing Stocks</div>
          <p className={styles.disposeWaiting}>Waiting for other players to dispose their stocks…</p>
        </div>
      )
    }
    const maxTrade = Math.floor(Math.min(disposeLocal.sell + (myDefunctStocks - disposeLocal.sell - disposeLocal.trade), myDefunctStocks) / 2) * 2
    const remaining = myDefunctStocks - disposeLocal.sell - disposeLocal.trade
    return (
      <div className={styles.actionCard}>
        <div className={styles.actionTitle}>Dispose {disposeDefunct ? HOTEL_LABELS[disposeDefunct as Hotel] ?? disposeDefunct : ''} Stocks</div>
        <div className={styles.actionSub}>
          You hold {myDefunctStocks} shares. Sell for ${stockPrice(disposeDefunct, state.chains).toLocaleString()} each, trade 2:1 for {survivorName ? HOTEL_LABELS[survivorName as Hotel] ?? survivorName : '?'}, or keep.
        </div>
        <div className={styles.disposeGrid}>
          <span className={styles.disposeLabel}>Sell</span>
          <div className={styles.qtyCtrl}>
            <button className={styles.qbtn} onClick={() => setDisposeLocal({ ...disposeLocal, sell: Math.max(0, disposeLocal.sell - 1) })}>−</button>
            <span className={styles.qval}>{disposeLocal.sell}</span>
            <button className={styles.qbtn} onClick={() => setDisposeLocal({ ...disposeLocal, sell: Math.min(myDefunctStocks - disposeLocal.trade, disposeLocal.sell + 1) })}>+</button>
          </div>
          <span style={{ fontSize: '0.75rem', opacity: 0.7 }}>
            +${(disposeLocal.sell * stockPrice(disposeDefunct, state.chains)).toLocaleString()}
          </span>
          <span className={styles.disposeLabel}>Trade (2:1)</span>
          <div className={styles.qtyCtrl}>
            <button className={styles.qbtn} onClick={() => setDisposeLocal({ ...disposeLocal, trade: Math.max(0, disposeLocal.trade - 2) })}>−</button>
            <span className={styles.qval}>{disposeLocal.trade}</span>
            <button className={styles.qbtn} onClick={() => {
              const next = disposeLocal.trade + 2
              if (next <= maxTrade) setDisposeLocal({ ...disposeLocal, trade: next })
            }}>+</button>
          </div>
          <span style={{ fontSize: '0.75rem', opacity: 0.7 }}>
            +{disposeLocal.trade / 2} {survivorName ? HOTEL_LABELS[survivorName as Hotel] ?? survivorName : '?'}
          </span>
          <span className={styles.disposeLabel}>Keep</span>
          <span className={styles.qval} style={{ padding: '0 8px' }}>{remaining}</span>
          <span />
        </div>
        <div className={styles.btnRow}>
          <button
            className={`${styles.btn} ${styles.btnPrimary}`}
            onClick={() => onDispose(disposeLocal.sell, disposeLocal.trade)}
          >
            Confirm
          </button>
        </div>
      </div>
    )
  }

  // ── Buy Stocks ─────────────────────────────────────────────────────────────
  if (state.phase === 'buy') {
    const activeHotels = HOTELS.filter(h => state.chains[h]?.active)
    const canAfford = (me?.cash ?? 0) >= buyCost
    return (
      <div className={styles.actionCard}>
        <div className={styles.actionTitle}>Buy Stocks</div>
        <div className={styles.buySummaryBar}>
          <span>Budget: ${(me?.cash ?? 0).toLocaleString()}</span>
          <span>Buying: {totalBuyQty}/{MAX_BUY}</span>
          <span>Cost: ${buyCost.toLocaleString()}</span>
        </div>
        {activeHotels.map(h => {
          const price = stockPrice(h, state.chains)
          const qty = buyQty[h] ?? 0
          const bank = state.stockBank[h] ?? 0
          const canBuyMore = totalBuyQty < MAX_BUY && bank > 0 && (me?.cash ?? 0) >= buyCost + price
          return (
            <div key={h} className={styles.buyRow}>
              <div className={styles.buyHotel}>
                <div className={styles.mergeDot} style={{ background: `var(--hotel-${h})`, width: 10, height: 10, borderRadius: '50%', flexShrink: 0 }} />
                {HOTEL_LABELS[h as Hotel]}
              </div>
              <span className={styles.buyPrice}>${price.toLocaleString()} · {bank} left</span>
              <div className={styles.qtyCtrl}>
                <button className={styles.qbtn} disabled={qty === 0} onClick={() => setBuyQty({ ...buyQty, [h]: Math.max(0, qty - 1) })}>−</button>
                <span className={styles.qval}>{qty}</span>
                <button className={styles.qbtn} disabled={!canBuyMore} onClick={() => setBuyQty({ ...buyQty, [h]: qty + 1 })}>+</button>
              </div>
            </div>
          )
        })}
        {activeHotels.length === 0 && (
          <p style={{ fontSize: '0.8rem', opacity: 0.6, margin: '8px 0 16px' }}>No active hotel chains — skip buying.</p>
        )}
        <div className={styles.btnRow}>
          <button
            className={`${styles.btn} ${styles.btnPrimary}`}
            disabled={!canAfford}
            onClick={onBuyStocks}
          >
            {totalBuyQty > 0 ? `Buy ${totalBuyQty} stock${totalBuyQty > 1 ? 's' : ''}` : 'Skip buy'}
          </button>
          <button className={`${styles.btn} ${styles.btnGhost} ${styles.btnSm}`} onClick={onEndTurn}>
            End Turn
          </button>
        </div>
      </div>
    )
  }

  // ── Waiting for draw phase ─────────────────────────────────────────────────
  if (state.phase === 'draw') {
    return (
      <div className={styles.actionCard}>
        <div className={styles.actionTitle}>Drawing Tile</div>
        <div className={styles.actionSub}>A new tile will be drawn to your hand.</div>
        {state.bag.length <= 6 && (
          <div className={styles.btnRow}>
            <button className={`${styles.btn} ${styles.btnGhost} ${styles.btnSm}`} onClick={onEndGame}>
              End Game (tiles low)
            </button>
          </div>
        )}
      </div>
    )
  }

  return null
}

// ── Opponent hidden hand ──────────────────────────────────────────────────────

function OpponentHand({ opponent, handSize }: { opponent: PlayerState; handSize: number }) {
  // Backend projects opponents' hands as [] — show face-down placeholders.
  // If hand is somehow non-empty (pass-and-play dev mode), still hide the tile IDs.
  const count = opponent.hand.length > 0 ? opponent.hand.length : handSize
  return (
    <div className={styles.handSection}>
      <div className={styles.handLabel}>{opponent.name}'s hand</div>
      <div className={styles.handRow}>
        {Array.from({ length: count }, (_, i) => (
          <div key={i} className={`${styles.htile} ${styles.htileEmpty}`} aria-label="Hidden tile" />
        ))}
      </div>
    </div>
  )
}

// ── Hotel chips sidebar ───────────────────────────────────────────────────────

function HotelChips({ state }: { state: SkylineState }) {
  return (
    <div className={styles.sideSection}>
      <div className={styles.sideTitle}>Hotel Chains</div>
      {HOTELS.map(h => {
        const chain = state.chains[h]
        const active = chain?.active ?? false
        const price = stockPrice(h, state.chains)
        return (
          <div
            key={h}
            className={`${styles.hotelChip} ${active ? '' : styles.hotelChipOff}`}
            style={active ? {
              borderColor: `var(--hotel-${h})`,
              background: `color-mix(in srgb, var(--hotel-${h}) 8%, var(--surface-float))`,
              boxShadow: `0 0 10px var(--hotel-${h}-glow)`,
            } : undefined}
          >
            <div className={styles.hcRow}>
              <span className={styles.hcName} style={active ? { color: `var(--hotel-${h})` } : undefined}>
                {HOTEL_LABELS[h as Hotel]}
              </span>
              <div className={styles.hcRight}>
                {active && <span className={styles.hcSize}>{chain.size} tiles</span>}
                {active && <span className={styles.hcPrice}>${price.toLocaleString()}</span>}
                {!active && <span className={styles.hcTag}>inactive</span>}
              </div>
            </div>
            {active && (
              <div className={styles.hcBarWrap}>
                <div
                  className={styles.hcBarFill}
                  style={{ width: `${Math.min(100, (chain.size / 41) * 100)}%`, background: `var(--hotel-${h})` }}
                />
              </div>
            )}
          </div>
        )
      })}
    </div>
  )
}

// ── Player cards sidebar ──────────────────────────────────────────────────────

function PlayerCards({ state, myPlayerId }: { state: SkylineState; myPlayerId: string }) {
  return (
    <div className={styles.sideSection}>
      <div className={styles.sideTitle}>Players</div>
      {state.players.map((p, idx) => {
        const isActive = idx === state.currentPlayer
        const nw = netWorth(p, state)
        return (
          <div key={p.id} className={`${styles.playerCard} ${isActive ? styles.playerCardActive : ''}`}>
            <div className={styles.pcHeader}>
              <div
                className={styles.pcAvatar}
                style={{ background: p.color, color: '#000' }}
              >
                {p.name.charAt(0).toUpperCase()}
              </div>
              <div>
                <div className={styles.pcName}>{p.name}{p.id === myPlayerId ? ' (You)' : ''}</div>
                <div className={styles.pcCash}>${p.cash.toLocaleString()} cash</div>
              </div>
              <div style={{ marginLeft: 'auto', textAlign: 'right' }}>
                <div className={styles.pcNet}>${nw.toLocaleString()}</div>
                <span className={styles.pcNetLabel}>net worth</span>
              </div>
            </div>
            <div className={styles.pcStocks}>
              {HOTELS.filter(h => (p.stocks[h] ?? 0) > 0).map(h => (
                <span
                  key={h}
                  className={styles.stockPip}
                  style={{ background: `color-mix(in srgb, var(--hotel-${h}) 20%, var(--surface-float))`, color: `var(--hotel-${h})` }}
                >
                  {HOTEL_LABELS[h as Hotel][0]} {p.stocks[h]}
                </span>
              ))}
            </div>
          </div>
        )
      })}
    </div>
  )
}

// ── Log section sidebar ───────────────────────────────────────────────────────

function LogSection({ log }: { log: string[] }) {
  const [showAll, setShowAll] = useState(false)
  const displayLog = showAll ? log : log.slice(-8)
  return (
    <div className={styles.sideSection}>
      <div className={styles.sideTitle}>Game Log</div>
      {displayLog.map((entry, i) => (
        <div key={i} className={styles.logEntry}>{entry}</div>
      ))}
      {log.length > 8 && !showAll && (
        <button className={styles.logMoreBtn} onClick={() => setShowAll(true)}>
          Show all {log.length} entries
        </button>
      )}
    </div>
  )
}
