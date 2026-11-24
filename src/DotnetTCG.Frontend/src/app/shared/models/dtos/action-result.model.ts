/** Resultado del evento "action-result" del hub (payload real del backend). */
export interface GameEventDTO {
  eventType: string;
  playerId: string | null;
  playerName: string | null;
  details: string;
  timestamp: string;
}
