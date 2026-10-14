import { GameState, PendingChoice } from './game-state.model';

/** Resultado de POST /v1/games/{id}/actions y del evento "action-result" del hub. */
export interface GameEventDTO {
  eventType: string;
  playerId: string | null;
  playerName: string | null;
  details: string;
  timestamp: string;
}

export interface CoinFlipEventDTO {
  result: 'HEADS' | 'TAILS';
  label: string;
  results?: string[];
}

export interface ActionResultResponse {
  success: boolean;
  message: string;
  state: GameState | null;
  pendingChoice?: PendingChoice | null;
  events?: string[];
  targetPlayerId?: string;
  coinFlip?: CoinFlipEventDTO;
}
