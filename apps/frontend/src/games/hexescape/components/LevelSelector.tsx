import styles from '../styles.module.css'

// Known level catalogue — matches the static C# level data in HexEscapeModule.
// When the backend adds more levels, add them here.
export const KNOWN_LEVELS: Array<{ id: string; name: string; description: string }> = [
  {
    id: 'tutorial-01',
    name: 'Tutorial',
    description: 'Learn the ropes. Small grid, low threat.',
  },
]

export const DEFAULT_LEVEL_ID = 'tutorial-01'

interface LevelSelectorProps {
  selectedLevelId: string
  onSelect: (levelId: string) => void
  /** Whether the current user is the host (only host can select) */
  isHost: boolean
}

/**
 * Level selector shown to the host in the pre-game lobby.
 *
 * PLATFORM NOTE (AD-9): The platform's create-room flow (LobbyPage.tsx) currently
 * sends POST /api/rooms with only { gameId } — it does not pass req.Options.
 * The backend's RoomEndpoints.cs does not yet propagate req.Options to room.GameOptions.
 * Until AD-9 is fixed by the backend agent, the levelId chosen here cannot be
 * transmitted to CreateInitialState. The backend falls back to "tutorial-01" (AC-8).
 *
 * This component is wired to dispatch a level selection over SignalR once the
 * platform wires up the options pathway. For now it shows the host the current
 * default and lets them see what levels will be available.
 */
export function LevelSelector({ selectedLevelId, onSelect, isHost }: LevelSelectorProps) {
  return (
    <div className={styles.levelSelector}>
      <div className={styles.levelSelectorTitle}>Choose Level</div>
      {!isHost && (
        <p className={styles.levelSelectorHint}>
          The host is selecting a level.
        </p>
      )}
      {isHost && (
        <p className={styles.levelSelectorHint}>
          Select a level to play. The game will start with your chosen level.
        </p>
      )}
      <ul className={styles.levelList} role="listbox" aria-label="Available levels">
        {KNOWN_LEVELS.map(level => {
          const isSelected = selectedLevelId === level.id
          return (
            <li key={level.id} role="option" aria-selected={isSelected}>
              <button
                className={[
                  styles.levelOption,
                  isSelected ? styles.levelOptionActive : '',
                ].filter(Boolean).join(' ')}
                onClick={() => isHost && onSelect(level.id)}
                disabled={!isHost}
                aria-pressed={isSelected}
                aria-label={`${level.name} — ${level.description}`}
              >
                <div>
                  <div style={{ fontFamily: 'var(--font-body)', fontSize: '0.9rem', color: 'var(--text-bright)', fontWeight: 600 }}>
                    {level.name}
                  </div>
                  <div className={styles.levelOptionId}>{level.description}</div>
                </div>
                {isSelected && (
                  <span className={styles.levelOptionCheck} aria-hidden="true">&#10003;</span>
                )}
              </button>
            </li>
          )
        })}
      </ul>
    </div>
  )
}
