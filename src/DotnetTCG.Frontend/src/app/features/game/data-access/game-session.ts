import { computed, inject, Injectable, signal } from '@angular/core';
import { firstValueFrom } from 'rxjs';
import { API_URL } from '../../../core/http/api-url.token';
import { SessionStore } from '../../../core/auth/session';
import { GameHubService } from '../../../core/realtime/game-hub.service';
import { SoundService } from '../../../core/sound/sound';
import { CardsApi } from '../../../shared/data-access/cards.api';
import { CardDto } from '../../../shared/models/card.models';
import { GameApi } from './game.api';
import {
  ActionResultDto,
  ChatMessageDto,
  GameActionRequest,
  GameLogEntryDto,
  GameStateViewDto,
} from '../models/game.models';

export type TargetRef = { readonly zone: 'active' | 'bench'; readonly index: number };

export type SelectMode =
  | { readonly kind: 'none' }
  | { readonly kind: 'attach'; readonly cardId: string }
  | { readonly kind: 'evolve'; readonly cardId: string }
  | { readonly kind: 'tool'; readonly cardId: string }
  | { readonly kind: 'retreat' };

export interface LogLine {
  readonly turn: number;
  readonly text: string;
}

/**
 * Estado vivo de una partida: escucha el hub, guarda la vista proyectada,
 * resuelve acciones y mantiene caché de cartas + selección. Una instancia
 * por página (providers del GamePage).
 */
@Injectable()
export class GameSession {
  private readonly hub = inject(GameHubService);
  private readonly api = inject(GameApi);
  private readonly cardsApi = inject(CardsApi);
  private readonly session = inject(SessionStore);
  private readonly sound = inject(SoundService);
  private readonly offFns: Array<() => void> = [];
  private readonly cardCache = new Map<string, CardDto>();

  readonly state = signal<GameStateViewDto | null>(null);
  readonly log = signal<readonly LogLine[]>([]);
  readonly chat = signal<readonly ChatMessageDto[]>([]);
  readonly paused = signal(false);
  readonly error = signal<string | null>(null);
  readonly selection = signal<{ readonly handCardId: string } | null>(null);
  readonly mode = signal<SelectMode>({ kind: 'none' });
  readonly previewUrl = signal<string | null>(null);

  readonly myOrder = computed(() => this.state()?.viewerPlayerIndex ?? null);
  readonly myBoard = computed(() => {
    const view = this.state();
    const order = this.myOrder();
    return view !== null && order !== null ? view.boards[order] : null;
  });
  readonly rivalBoard = computed(() => {
    const view = this.state();
    const order = this.myOrder();
    return view !== null && order !== null ? view.boards[1 - order] : null;
  });
  readonly myTurn = computed(() => {
    const view = this.state();
    return view !== null && view.viewerPlayerIndex !== null
      && view.currentPlayerIndex === view.viewerPlayerIndex
      && view.status === 'InProgress';
  });
  readonly pendingChoice = computed(() => this.state()?.pendingChoice ?? null);
  readonly finished = computed(() => this.state()?.status === 'Finished');
  readonly didIWin = computed(() => {
    const view = this.state();
    return view !== null && view.winnerPlayerIndex !== null
      && view.winnerPlayerIndex === view.viewerPlayerIndex;
  });

  async connect(gameId: string): Promise<void> {
    await this.hub.ensureConnected();
    this.offFns.push(
      this.hub.on<GameStateViewDto>('game-state', (view) => {
        if (view.gameId === gameId) {
          this.state.set(view);
          void this.warmCardCache(view);
        }
      }),
      this.hub.on<ActionResultDto>('action-result', (result) => {
        if (!result.success) {
          this.error.set(result.error ?? 'Acción rechazada.');
          this.sound.playError();
        }
        for (const event of result.events ?? []) {
          this.appendLog(event);
        }
      }),
      this.hub.on<ChatMessageDto>('chat', (message) => {
        this.chat.update((messages) => [...messages.slice(-99), message]);
      }),
      this.hub.on<{ code: string; message: string }>('error', (error) => {
        this.error.set(error.message);
        this.sound.playError();
      }),
      this.hub.on<{ gameId: string }>('game-paused', (event) => {
        if (event.gameId === gameId) {
          this.paused.set(true);
        }
      }),
    );
    await this.hub.invoke('Reconnect', gameId);
    const [view, logs] = await Promise.all([
      firstValueFrom(this.api.state(gameId)),
      firstValueFrom(this.api.logs(gameId)),
    ]);
    this.state.set(view);
    this.log.set(logs.map((entry) => ({ turn: entry.turnNumber, text: `${entry.action}: ${entry.result}` })));
    this.paused.set(view.status === 'Paused');
    await this.warmCardCache(view);
  }

  disconnect(): void {
    for (const off of this.offFns.splice(0)) {
      off();
    }
  }

  clearError(): void {
    this.error.set(null);
  }

  selectHandCard(handCardId: string | null): void {
    this.selection.set(handCardId === null ? null : { handCardId });
    this.mode.set({ kind: 'none' });
  }

  cancelMode(): void {
    this.mode.set({ kind: 'none' });
    this.selection.set(null);
  }

  async act(request: GameActionRequest): Promise<void> {
    const view = this.state();
    if (view === null) {
      return;
    }
    this.error.set(null);
    await this.hub.invoke('ExecuteAction', view.gameId, request);
  }

  async playHandCard(cardId: string): Promise<void> {
    const card = await this.card(cardId);
    if (card === null) {
      return;
    }
    if (card.supertype === 'Energy') {
      this.mode.set({ kind: 'attach', cardId });
      return;
    }
    if (card.supertype === 'Trainer' && card.trainerSubtype === 'Tool') {
      this.mode.set({ kind: 'tool', cardId });
      return;
    }
    if (card.supertype === 'Pokemon' && card.evolvesFrom !== null) {
      this.mode.set({ kind: 'evolve', cardId });
      return;
    }
    await this.act({ type: 'PlayCard', cardId });
    this.selectHandCard(null);
  }

  async fieldClick(zone: 'active' | 'bench', index: number): Promise<void> {
    const mode = this.mode();
    const selection = this.selection();
    switch (mode.kind) {
      case 'attach':
        await this.act({ type: 'AttachEnergy', cardId: mode.cardId, target: { zone, index } });
        break;
      case 'evolve':
        await this.act({ type: 'Evolve', cardId: mode.cardId, target: { zone, index } });
        break;
      case 'tool':
        await this.act({ type: 'PlayCard', cardId: mode.cardId, target: { zone, index } });
        break;
      case 'retreat':
        await this.retreatTo(index);
        break;
      case 'none':
        if (selection !== null) {
          await this.playHandCard(selection.handCardId);
        }
        return;
    }
    this.cancelMode();
  }

  startRetreat(): void {
    this.mode.set({ kind: 'retreat' });
    this.selection.set(null);
  }

  async attack(attackName: string): Promise<void> {
    await this.act({ type: 'Attack', attackName });
  }

  async useAbility(abilityName: string, user: TargetRef): Promise<void> {
    await this.act({ type: 'UseAbility', abilityName, target: user });
  }

  async passTurn(): Promise<void> {
    await this.act({ type: 'PassTurn' });
  }

  async concede(): Promise<void> {
    await this.act({ type: 'Concede' });
  }

  async mulligan(): Promise<void> {
    const view = this.state();
    if (view === null) {
      return;
    }
    this.error.set(null);
    await this.hub.invoke('Mulligan', view.gameId);
  }

  async resolveChoice(choiceId: string, selectedOptions: readonly string[]): Promise<void> {
    const pending = this.pendingChoice();
    const type = pending?.choiceId.startsWith('attack:') === true
      ? 'Attack'
      : pending?.choiceId.startsWith('ability:') === true
        ? 'UseAbility'
        : 'PlayCard';
    await this.act({ type, choiceId, selectedOptions: [...selectedOptions] });
  }

  async pause(): Promise<void> {
    const view = this.state();
    if (view === null) {
      return;
    }
    await this.hub.invoke('PauseGame', view.gameId);
  }

  async reconnect(): Promise<void> {
    const view = this.state();
    if (view === null) {
      return;
    }
    this.paused.set(false);
    await this.hub.invoke('Reconnect', view.gameId);
  }

  async sendChat(text: string): Promise<void> {
    const view = this.state();
    const trimmed = text.trim();
    if (view === null || trimmed.length === 0) {
      return;
    }
    await this.hub.invoke('SendChat', view.gameId, trimmed);
  }

  myLabel(userId: string): string {
    if (userId === this.session.session()?.userId) {
      return 'Yo';
    }
    return `Rival ${userId.slice(0, 6)}`;
  }

  imageUrlOf(cardId: string): string | null {
    return this.cardCache.get(cardId)?.imageUrl ?? null;
  }

  async card(cardId: string): Promise<CardDto | null> {
    const cached = this.cardCache.get(cardId);
    if (cached !== undefined) {
      return cached;
    }
    try {
      const fetched = await firstValueFrom(this.cardsApi.getById(cardId));
      this.cardCache.set(cardId, fetched);
      return fetched;
    } catch {
      return null;
    }
  }

  private async retreatTo(benchIndex: number): Promise<void> {
    const board = this.myBoard();
    const active = board?.active;
    if (board === null || active === undefined || active === null) {
      return;
    }
    const cost = (await this.card(active.cardId))?.retreatCost ?? 0;
    const picked = await this.pickEnergies(active.energies, cost);
    await this.act({
      type: 'Retreat',
      target: { zone: 'bench', index: benchIndex },
      energyIdsToDiscard: picked,
    });
  }

  private async pickEnergies(attached: readonly string[], cost: number): Promise<string[]> {
    const picked: string[] = [];
    let value = 0;
    for (const id of attached) {
      if (value >= cost) {
        break;
      }
      const card = await this.card(id);
      const energyValue = card?.specialEnergy === 'DoubleColorless' ? 2 : 1;
      picked.push(id);
      value += energyValue;
    }
    return picked;
  }

  private async warmCardCache(view: GameStateViewDto): Promise<void> {
    const ids = new Set<string>();
    for (const board of view.boards) {
      if (board.active !== null) {
        ids.add(board.active.cardId);
        for (const energy of board.active.energies) {
          ids.add(energy);
        }
        if (board.active.tool !== null) {
          ids.add(board.active.tool);
        }
      }
      for (const bench of board.bench) {
        ids.add(bench.cardId);
      }
      for (const card of board.hand ?? []) {
        ids.add(card.id);
      }
    }
    if (view.stadiumCardId !== null) {
      ids.add(view.stadiumCardId);
    }
    await Promise.all([...ids].filter((id) => !this.cardCache.has(id)).map((id) => this.card(id)));
  }

  private appendLog(event: string): void {
    const turn = this.state()?.turnNumber ?? 0;
    this.log.update((lines) => [...lines.slice(-199), { turn, text: prettifyEvent(event) }]);
  }
}

function prettifyEvent(event: string): string {
  const labels: Record<string, string> = {
    Knockout: '¡KO!',
    PrizeTaken: 'Premio tomado',
    SpecialConditionApplied: 'Condición especial',
    TurnStart: 'Inicio de turno',
    TurnEnd: 'Fin de turno',
    GameOver: 'Fin de la partida',
    EnergyAttached: 'Energía unida',
    Evolution: 'Evolución',
    Retreat: 'Retirada',
    Mulligan: 'Mulligan',
    SetupComplete: 'Preparación completa',
    GameStart: '¡Empieza la partida!',
    Attack: 'Ataque',
  };
  return labels[event] ?? event;
}
