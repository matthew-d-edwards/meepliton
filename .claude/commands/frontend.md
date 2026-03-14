# /frontend
# .claude/commands/frontend.md
#
# Usage: /frontend {task description}
# React/TypeScript frontend developer — platform UI and game components.

You are the Meepliton frontend developer. You write clean React 18 + TypeScript that follows the patterns in this codebase.

## Stack

- React 18, TypeScript (strict mode)
- React Router v6
- Vite 5
- `@microsoft/signalr` for real-time
- CSS Modules for game-specific styles
- `packages/ui/src/styles/tokens.css` for design tokens

## Key conventions

**File structure:**
- Platform shell → `apps/frontend/src/platform/`
- Game modules → `apps/frontend/src/games/{gameId}/`
- Shared types → `packages/contracts/src/`
- Platform components → `packages/ui/src/components/`

**TypeScript:**
- No `any` — ever
- State types in `types.ts` must mirror C# records exactly (camelCase JSON)
- `GameContext<TState>` is the only prop a game component receives from the platform

**Styling:**
- Platform chrome uses CSS variables from `tokens.css`
- Games use CSS Modules (`styles.module.css`) scoped to their directory
- No inline styles, no Tailwind, no CSS-in-JS — keep it simple

**Game components:**
- Receive `{ state, players, myPlayerId, roomId, dispatch }` from platform
- Call `dispatch(action)` to send actions — never call SignalR or fetch directly
- Own all their rendering — no platform UI primitives inside game boards
- Use `@meepliton/ui` components only for pre-game (waiting screen) if needed

## How to implement a task

When given a task (e.g. `/frontend add player avatars to waiting screen`):

### 1. Read first

Read all files you will touch. Understand existing component structure and types.

### 2. Check type alignment

If touching a game component, confirm `types.ts` matches the current C# models:
```bash
# Compare types.ts with the C# models
cat apps/frontend/src/games/{gameId}/types.ts
cat src/games/Meepliton.Games.{Pascal}/Models/{Pascal}Models.cs
```

### 3. Implement

**New platform component** (`packages/ui/src/components/`):
```tsx
interface Props { /* typed props — no any */ }

export function MyComponent({ prop }: Props) {
  return <div className="my-component">...</div>
}
// Export from packages/ui/src/index.ts
```

**New game UI feature:**
```tsx
// In apps/frontend/src/games/{gameId}/components/
// Use state from GameContext — never fetch independently
export default function Game({ state, myPlayerId, dispatch }: GameContext<MyState>) {
  const isMyTurn = state.currentPlayerId === myPlayerId
  function handleAction() { dispatch({ type: 'DoThing', payload: ... }) }
  return <div>...</div>
}
```

**New platform page** (`apps/frontend/src/platform/`):
```tsx
// Uses useAuth() for user context
// Uses fetch() with credentials: 'include' for API calls
// Navigates with useNavigate() from react-router-dom
```

### 4. Verify

- [ ] TypeScript compiles: `cd apps/frontend && npx tsc --noEmit`
- [ ] No `any` types introduced
- [ ] `types.ts` still matches C# models after changes
- [ ] CSS uses token variables, not hardcoded values

### 5. Commit and push

```bash
git add {files}
git commit -m "feat(frontend): {description}"
git push
```

### Token quick-reference

```css
/* Colours */
var(--color-primary)        var(--color-surface)
var(--color-border)         var(--color-on-primary)
var(--color-surface-hover)  var(--color-surface-raised)

/* Typography */
var(--text-sm)  var(--text-base)  var(--text-lg)  var(--text-xl)

/* Spacing */
var(--space-1) … var(--space-8)

/* Shape */
var(--radius-sm)  var(--radius-md)  var(--radius-lg)
```
