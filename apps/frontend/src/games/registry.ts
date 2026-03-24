import type { GameModule } from '@meepliton/contracts'

// Each game exports a GameModule<TState> — the registry erases TState to keep the lookup type simple.
// Type safety is enforced inside each game's own Component, not at the registry level.
// eslint-disable-next-line @typescript-eslint/no-explicit-any
type AnyGameModule = GameModule<any>
export const gameRegistry: Record<string, () => Promise<{ default: AnyGameModule }>> = {
  skyline: () => import('./skyline'),
  liarsdice: () => import('./liarsdice'),
  deadmansswitch: () => import('./deadmansswitch'),
  fthat: () => import('./fthat'),
  // Add new games here ↓
}
