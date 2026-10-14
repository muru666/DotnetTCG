/**
 * ChatMessage - Represents a single chat message in the game chat.
 */
export interface ChatMessage {
  id?: string;
  senderId: string;
  senderName?: string;
  content: string;
  timestamp: Date;
}

/**
 * ChatMessageDto - DTO recibido del hub ("chat": { senderId, text, at }).
 */
export interface ChatMessageDto {
  senderId: string;
  text: string;
  at: string;
  content?: string;
  timestamp?: number | string;
  senderName?: string;
}
