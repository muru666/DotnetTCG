/** Tipos de carta del catálogo (/v1/cards). Compartido: lo usan mazos y juego. */

export interface AttackDto {
  readonly name: string;
  readonly description: string;
  readonly damage: string;
  readonly cost: readonly string[];
}

export interface AbilityDto {
  readonly name: string;
  readonly text: string;
  readonly type: string;
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
  readonly abilities: readonly AbilityDto[];
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
