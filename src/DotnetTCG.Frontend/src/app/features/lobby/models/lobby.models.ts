/** Contratos de /v1/games (lobby) y eventos del GameHub. */

export interface LobbyPlayerDto {
  readonly userId: string;
  readonly order: number;
  readonly deckId: string | null;
  readonly isReady: boolean;
}

export interface LobbyDto {
  readonly id: string;
  readonly creatorId: string;
  readonly isPublic: boolean;
  readonly acceptsSpectators: boolean;
  readonly players: readonly LobbyPlayerDto[];
}

export interface GameStartedEvent {
  readonly gameId: string;
}

export interface HubError {
  readonly code: string;
  readonly message: string;
}
