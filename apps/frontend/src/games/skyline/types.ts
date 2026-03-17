// Mirror of SkylineModels.cs — keep in sync with C# records

export type SkylinePhase = 'PlacingTile' | 'GameOver';

export interface PlayerState {
  id: string;
  displayName: string;
  avatarUrl: string | null;
  seatIndex: number;
  score: number;
  /** Tile values in this player's hand.
   *  In multiplayer, the backend projects state per viewer:
   *  only the viewing player's own hand is populated.
   *  All opponents' hands are projected as [] (empty). */
  hand: number[];
}

export interface SkylineState {
  players: PlayerState[];
  /** 2-D board — board[row][col], null = empty cell */
  board: (number | null)[][];
  currentPlayerId: string;
  phase: SkylinePhase;
  turn: number;
  winnerId: string | null;
}

export interface PlaceTilePayload {
  row: number;
  col: number;
  tileValue: number;
}

export interface SkylineAction {
  type: 'PlaceTile' | 'Undo';
  placeTile?: PlaceTilePayload;
}
