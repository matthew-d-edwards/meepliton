// Mirror of SkylineModels.cs — keep in sync with C# records

export type Hotel =
  | 'luxor'
  | 'tower'
  | 'american'
  | 'festival'
  | 'worldwide'
  | 'continental'
  | 'imperial';

export const HOTELS: Hotel[] = [
  'luxor',
  'tower',
  'american',
  'festival',
  'worldwide',
  'continental',
  'imperial',
];

export const HOTEL_LABELS: Record<Hotel, string> = {
  luxor: 'Luxor',
  tower: 'Tower',
  american: 'American',
  festival: 'Festival',
  worldwide: 'Worldwide',
  continental: 'Continental',
  imperial: 'Imperial',
};

export interface ChainState {
  active: boolean;
  size: number;
  tiles: string[];
}

export interface DisposeQueueItem {
  defunct: string;
  playerIdx: number;
}

export interface DisposeDecision {
  sell: number;
  trade: number;
}

export interface PendingState {
  type: 'found' | 'merge';
  // found
  tiles?: string[];
  chosen?: string;
  // merge
  tid?: string;
  hotels?: string[];
  survivors?: string[];
  survivor?: string;
  defunct?: string[];
  survivorChosen?: boolean;
  defunctSizes?: Record<string, number>;
  // dispose
  disposeQueue?: DisposeQueueItem[];
  disposeIdx?: number;
  disposeDecisions?: Record<string, DisposeDecision>;
}

export interface PlayerState {
  id: string;
  name: string;
  color: string;
  cash: number;
  stocks: Record<string, number>;
  hand: string[];
}

export interface SkylineState {
  players: PlayerState[];
  currentPlayer: number;           // index into players
  board: Record<string, string>;   // tileId -> hotel | "neutral"
  chains: Record<string, ChainState>;
  stockBank: Record<string, number>;
  bag: string[];
  log: string[];
  gameOver: boolean;
  winner?: string;
  rankedOrder?: number[];
  phase: 'place' | 'found' | 'merge' | 'dispose' | 'buy' | 'draw';
  pending?: PendingState;
}

export interface SkylineAction {
  type:
    | 'PlaceTile'
    | 'FoundHotel'
    | 'ChooseSurvivor'
    | 'ConfirmSurvivor'
    | 'Dispose'
    | 'BuyStocks'
    | 'EndTurn'
    | 'EndGame';
  tileId?: string;
  hotel?: string;
  sell?: number;
  trade?: number;
  purchases?: Record<string, number>;
}
