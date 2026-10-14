/** Vistas proyectadas del backend (GET state / evento game-state). */

export interface FieldPokemonDto {
  readonly cardId: string;
  readonly name: string;
  readonly hp: number;
  readonly hpRemaining: number;
  readonly damageCounters: number;
  readonly energies: readonly string[];
  readonly tool: string | null;
  readonly statuses: readonly string[];
  readonly totalEnergy: number;
}

export interface BoardViewDto {
  readonly active: FieldPokemonDto | null;
  readonly bench: readonly FieldPokemonDto[];
  readonly deckCount: number;
  readonly prizeCount: number;
  readonly discard: readonly string[];
  readonly hand: readonly HandCardDto[] | null;
  readonly handCount: number;
}

export interface HandCardDto {
  readonly id: string;
  readonly name: string;
  readonly supertype: 'Pokemon' | 'Energy' | 'Trainer';
  readonly subtypes: readonly string[];
  readonly imageUrl: string;
  readonly specialEnergy?: string | null;
  readonly trainerSubtype?: string | null;
  readonly evolvesFrom?: string | null;
}

export interface ChoiceOptionDto {
  readonly optionId: string;
  readonly label: string;
  readonly imageUrl?: string | null;
}

export interface PendingChoiceDto {
  readonly choiceId: string;
  readonly playerIndex: number;
  readonly abilityName: string | null;
  readonly prompt: string;
  readonly options: readonly ChoiceOptionDto[];
  readonly context?: string | null;
}

export interface GameStateViewDto {
  readonly gameId: string;
  readonly status: string;
  readonly phase: string;
  readonly turnNumber: number;
  readonly currentPlayerIndex: number;
  readonly viewerPlayerIndex: number | null;
  readonly boards: readonly BoardViewDto[];
  readonly stadiumCardId: string | null;
  readonly pendingChoice: PendingChoiceDto | null;
  readonly winnerPlayerIndex: number | null;
  readonly suddenDeath: boolean;
}

export interface GameActionRequest {
  readonly type: string;
  readonly cardId?: string;
  readonly target?: { readonly zone: 'active' | 'bench'; readonly index: number };
  readonly attackName?: string;
  readonly energyIdsToDiscard?: readonly string[];
  readonly choiceId?: string;
  readonly selectedOptions?: readonly string[];
  readonly abilityName?: string;
}

export interface ActionResultDto {
  readonly success: boolean;
  readonly error?: string | null;
  readonly pendingChoice?: PendingChoiceDto | null;
  readonly events?: readonly string[];
}

export interface ChatMessageDto {
  readonly senderId: string;
  readonly text: string;
  readonly at: string;
}

export interface GameLogEntryDto {
  readonly id: number;
  readonly gameId: string;
  readonly turnNumber: number;
  readonly actingUserId: string | null;
  readonly action: string;
  readonly result: string;
  readonly createdAt: string;
}

export const STATUS_LABELS: Record<string, string> = {
  Burned: 'Quemado',
  Poisoned: 'Envenenado',
  Asleep: 'Dormido',
  Paralyzed: 'Paralizado',
  Confused: 'Confundido',
};

export const PHASE_LABELS: Record<string, string> = {
  Draw: 'Robo',
  Main: 'Principal',
  Attack: 'Ataque',
  BetweenTurns: 'Entre turnos',
};
