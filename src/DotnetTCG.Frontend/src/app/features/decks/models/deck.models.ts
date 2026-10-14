/** Contratos de /v1/decks y /v1/cards. */

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

export interface AttackDto {
  readonly name: string;
  readonly description: string;
  readonly damage: string;
  readonly cost: readonly string[];
}

export interface CardDto {
  readonly id: string;
  readonly name: string;
  readonly supertype: 'Pokemon' | 'Energy' | 'Trainer';
  readonly subtypes: readonly string[];
  readonly setId: string;
  readonly imageUrl: string;
  readonly hp: number | null;
  readonly retreatCost: number | null;
  readonly evolvesFrom: string | null;
  readonly pokemonType: string | null;
  readonly attacks: readonly AttackDto[];
  readonly energyType: string | null;
  readonly specialEnergy: string | null;
  readonly trainerSubtype: string | null;
}

export interface PagedResult<T> {
  readonly items: readonly T[];
  readonly total: number;
  readonly page: number;
  readonly pageSize: number;
}
