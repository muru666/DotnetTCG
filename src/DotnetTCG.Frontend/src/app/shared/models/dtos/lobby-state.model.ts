/** Estado del lobby. Fuente: GET/POST /v1/games + eventos "lobby" del hub. */

export interface LobbyPlayer {
  playerId: string;
  username: string;
  avatarUrl: string | null;
  ready: boolean;
  hasDeck: boolean;
  deckId: string | null;
  deckName: string | null;
  playerOrder: number;
}

export interface LobbyState {
  gameId: string;
  creatorId: string;
  players: LobbyPlayer[];
  countdownActive: boolean;
  countdownEndsAt: number | null;
  event: string | null;
  isPublic: boolean;
  acceptsSpectators: boolean;
}

/** Respuesta de GET /v1/games/my-resumable (GameSummaryDto). */
export interface ResumableGame {
  id: string;
  status: string;
  turnNumber: number;
  phase: string;
  currentPlayerIndex: number;
  winnerId: string | null;
  players: LobbyPlayer[];
}
