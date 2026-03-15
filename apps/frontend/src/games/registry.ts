import type { GameModule } from '@meepliton/contracts'

// ← Only this file changes when a new game is added
export const gameRegistry: Record<string, () => Promise<{ default: GameModule }>> = {
  skyline: () => import('./skyline'),
  liarsdice: () => import('./liarsdice'),
  // Add new games here ↓
}
