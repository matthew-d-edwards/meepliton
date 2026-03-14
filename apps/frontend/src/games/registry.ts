import type { GameModule } from '@meepliton/contracts'

// ← Only this file changes when a new game is added
export const gameRegistry: Record<string, () => Promise<{ default: GameModule }>> = {
  skyline: () => import('./skyline'),
  // Add new games here ↓
}
