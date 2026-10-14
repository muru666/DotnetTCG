export interface GameHistoryEvent {
  id: string;
  turn: number;
  action: string;
  player: string;
  timestamp: Date;
  details?: string;
}
