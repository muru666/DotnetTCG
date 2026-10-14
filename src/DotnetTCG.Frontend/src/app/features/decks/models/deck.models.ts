/** Contratos de /v1/decks. */

export interface DeckCardDto {
  readonly cardId: string;
  readonly cardName: string;
  readonly quantity: number;
}

export interface DeckDto {
  readonly id: string;
  readonly name: string;
  readonly isValid: boolean;
  readonly version: number;
  readonly cards: readonly DeckCardDto[];
}

export interface DeckValidation {
  readonly valid: boolean;
  readonly errors: readonly string[];
}
