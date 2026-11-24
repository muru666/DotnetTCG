/**
 * Modelos del estado de juego.
 * Los nombres de campo se conservan compatibles con los componentes;
 * GameService mapea las respuestas del backend .NET a estas formas.
 * Fuente de verdad: GET /v1/games/{id}/state + eventos del GameHub.
 */

export interface GameState {
  gameId: string;
  status: string;
  currentPlayerIndex: number;
  turnNumber: number;
  phase: string;
  players: string[];
  playerNames: Record<string, string>;
  myBoard: BoardDto;
  opponentBoard: BoardDto;
  winner: string | null;
  currentSetupPlayerIndex: number;
  startingPlayerIndex?: number;
  pendingChoice: PendingChoice | null;
  mulliganCounts?: number[];
  stadiumCard: TrainerCard | null;
  spectator?: boolean;
}

export interface PendingChoice {
  choiceId: string;
  playerId: string;
  abilityName: string;
  promptLabel: string;
  options: ChoiceOption[];
}

export interface ChoiceOption {
  optionId: string;
  label: string;
  value: string;
  imageUrl?: string;
}

export interface BoardDto {
  activePokemon: PokemonInPlay | null;
  bench: PokemonInPlay[];
  hand: Card[] | number;
  deck: number;
  prizeCards: number;
  discardPile: number;
}

export interface Card {
  id: string;
  name: string;
  imageUrl: string;
  supertype: string;
  subtypes: string[];
  rules: string[];
  setId: string | null;
  evolvesFrom?: string | null;
  energyType?: string | null;
  specialEnergyType?: string | null;
  trainerSubtype?: string | null;
}

export interface PokemonCard extends Card {
  hp: number;
  evolvesFrom: string | null;
  type: string;
  weaknesses: { type: string; value: string }[];
  resistances: { type: string; value: string }[];
  retreatCost: number;
  attacks: Attack[];
  abilities: PokemonAbility[];
}

export interface EnergyCard extends Card {
  energyType: string;
  specialEnergyType?: string;
}

export interface TrainerCard extends Card {
}

export interface Attack {
  name: string;
  description: string;
  damage: string;
  cost: string[];
}

export interface PokemonAbility {
  name: string;
  description: string;
  type: string;
}

export interface PokemonInPlay {
  pokemonCard: PokemonCard;
  damageCounters: number;
  attachedEnergies: EnergyCard[];
  attachedTool: TrainerCard | null;
  specialConditions: string[];
  active: boolean;
  justPlayed: boolean;
  position: number | null;
  turnsInPlay: number | null;
}

export interface GameActionResponse {
  type: 'GAME_STATE_UPDATE' | 'ERROR';
  gameState?: GameState;
  error?: string;
  event?: GameEvent;
}

export interface GameEvent {
  type: 'TURN_STARTED' | 'POKEMON_KO' | 'PRIZE_TAKEN' | 'STATUS_APPLIED' | 'GAME_ENDED' | 'CARD_PLAYED';
  playerId?: string;
  playerName?: string;
  details: string;
  timestamp: Date;
}

export interface GameLogEntry {
  turnNumber: number;
  action: string;
  result: string | null;
  actionType: string;
  actingPlayerUsername: string | null;
  createdAt: number; // epoch millis
}
