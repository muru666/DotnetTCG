import { Injectable, signal, computed, inject } from '@angular/core';
import { HttpClient } from '@angular/common/http';
import { Subject, Observable, of, throwError, timer, race, Subscription, firstValueFrom } from 'rxjs';
import { tap, catchError, map, first, switchMap } from 'rxjs/operators';
import { environment } from '../../../../environments/environment';
import { GameState, GameActionResponse, BoardDto, GameLogEntry, Attack, PokemonInPlay, PokemonCard, EnergyCard, TrainerCard, Card, PendingChoice } from '../../models/dtos/game-state.model';
import { LobbyState } from '../../models/dtos/lobby-state.model';
import { ChatMessage, ChatMessageDto } from '../../models/chat/chat-message.model';
import { GameEventDTO } from '../../models/dtos/action-result.model';
import { AuthService } from '../../../../app/core/services/auth.service';
import { GameSignalService, HubGameActionRequest } from '../websocket/game-signal.service';

export interface GameListItem {
  gameId: string;
  players: { playerId: string; order: number; username: string }[];
  status: string;
  /** El backend no expone fecha de creación en la lista de lobbies. */
  createdAt: string | null;
  isPublic: boolean;
  /** Si el lobby tiene password (el FE lo pide por acá, no por isPublic). */
  hasPassword: boolean;
  acceptsSpectators: boolean;
  creatorUsername: string | null;
}

interface BackendGameActionRequest {
  type: string;
  cardId?: string;
  target?: { zone: string; index: number };
  attackName?: string;
  energyIdsToDiscard?: string[];
  choiceId?: string;
  selectedOptions?: string[];
  abilityName?: string;
}

/** Vista cruda del backend (GameStateViewDto). */
interface BackendBoardView {
  active: BackendPokemon | null;
  bench: BackendPokemon[];
  deckCount: number;
  prizeCount: number;
  discard: string[];
  hand: BackendHandCard[] | null;
  handCount: number;
}

interface BackendPokemon {
  cardId: string;
  name: string;
  hp: number;
  hpRemaining: number;
  damageCounters: number;
  energies: string[];
  tool: string | null;
  statuses: string[];
  totalEnergy: number;
}

interface BackendHandCard {
  id: string;
  name: string;
  supertype: string;
  subtypes: string[];
  imageUrl: string;
  setId?: string | null;
  evolvesFrom?: string | null;
  hp?: number | null;
  retreatCost?: number | null;
  pokemonType?: string | null;
  attacks?: { name: string; description: string; damage: string; cost: string[] }[];
  abilities?: { name: string; text: string; type: string }[];
  energyType?: string | null;
  specialEnergy?: string | null;
  trainerSubtype?: string | null;
}

interface BackendStateView {
  gameId: string;
  status: string;
  phase: string;
  turnNumber: number;
  currentPlayerIndex: number;
  viewerPlayerIndex: number | null;
  boards: BackendBoardView[];
  stadiumCardId: string | null;
  pendingChoice: {
    choiceId: string;
    playerIndex: number;
    abilityName: string | null;
    prompt: string;
    options: { optionId: string; label: string; imageUrl?: string | null }[];
    context?: string | null;
  } | null;
  winnerPlayerIndex: number | null;
  suddenDeath: boolean;
  startingPlayerIndex?: number;
}

interface BackendLobbyPlayer {
  userId: string;
  order: number;
  deckId: string | null;
  isReady: boolean;
  username: string;
}

interface BackendLobby {
  id: string;
  creatorId: string;
  isPublic: boolean;
  acceptsSpectators: boolean;
  hasPassword?: boolean;
  players: BackendLobbyPlayer[];
}

const STATUS_MAP: Record<string, string> = {
  InLobby: 'IN_LOBBY',
  Setup: 'SETUP',
  InProgress: 'IN_PROGRESS',
  Finished: 'FINISHED',
  Cancelled: 'CANCELLED',
  Paused: 'PAUSED',
  Resuming: 'RESUMING',
};

const PHASE_MAP: Record<string, string> = {
  Draw: 'DRAW',
  Main: 'MAIN',
  Attack: 'ATTACK',
  BetweenTurns: 'BETWEEN_TURNS',
};

const CONDITION_MAP: Record<string, string> = {
  Burned: 'BURNED',
  Poisoned: 'POISONED',
  Asleep: 'ASLEEP',
  Paralyzed: 'PARALYZED',
  Confused: 'CONFUSED',
};

const EVENT_MAP: Record<string, string> = {
  Knockout: 'POKEMON_KO',
  PrizeTaken: 'PRIZE_TAKEN',
  SpecialConditionApplied: 'STATUS_APPLIED',
  TurnStart: 'TURN_STARTED',
  TurnEnd: 'TURN_END',
  GameOver: 'GAME_ENDED',
  EnergyAttached: 'ENERGY_ATTACHED',
  Evolution: 'EVOLUTION',
  Retreat: 'RETREAT',
  Mulligan: 'MULLIGAN',
  SetupComplete: 'SETUP_COMPLETE',
  GameStart: 'GAME_START',
  Attack: 'ATTACK',
};

const GAME_STATUS_MAP: Record<string, string> = {
  InLobby: 'IN_LOBBY',
  Setup: 'SETUP',
  InProgress: 'IN_PROGRESS',
  Finished: 'FINISHED',
  Cancelled: 'CANCELLED',
  Paused: 'PAUSED',
  Resuming: 'RESUMING',
};

function mapGameStatus(status: string): string {
  return GAME_STATUS_MAP[status] ?? status.toUpperCase();
}

function matchesFilter(lobby: BackendLobby, filter?: string): boolean {
  if (!filter || filter === 'todas') return true;
  if (filter === 'publicas') return lobby.isPublic;
  if (filter === 'privadas') return !lobby.isPublic;
  if (filter === 'espectar') return lobby.acceptsSpectators;
  return true;
}

interface BackendGameSummary {
  id: string;
  status: string;
  players: { userId: string; order: number; username: string }[];
}

@Injectable({
  providedIn: 'root'
})
export class GameService {
  private apiUrl = environment.apiUrl;

  gameState = signal<GameState | null>(null);
  gameEvents = signal<GameEventDTO[]>([]);
  playerIndex = signal<number>(0);
  gameId = signal<string | null>(null);
  isLoading = signal<boolean>(false);
  error = signal<string | null>(null);

  chatMessages = signal<ChatMessage[]>([]);

  currentGame = computed(() => this.gameState());
  isMyTurn = computed(() => {
    const game = this.gameState();
    if (!game) return false;
    return game.currentPlayerIndex === this.playerIndex();
  });

  private gameStateSubject = new Subject<GameState | null>();
  public gameState$ = this.gameStateSubject.asObservable();

  private actionResultSubject = new Subject<{ gameState?: GameState; error?: string; events?: GameEventDTO[] }>();
  private lobbyStateSubject = new Subject<LobbyState>();
  /** Lobby mapeado al modelo UI: el evento 'lobby' del hub trae BackendLobby crudo. */
  public lobbyState$ = this.lobbyStateSubject.asObservable();
  private hubSubscription?: Subscription;
  private lobbyPlayers: { userId: string; order: number; username: string }[] = [];
  private myMulligans = 0;
  private readonly cardCache = new Map<string, Card>();
  private readonly deckNames = new Map<string, string | null>();

  private readonly authService = inject(AuthService);

  public playerId(): string | null {
    return this.authService.getUserId();
  }

  constructor(
    private http: HttpClient,
    private signalService: GameSignalService
  ) {
    this.setupHubListeners();
  }

  public createGame(): Observable<string> {
    this.isLoading.set(true);
    this.error.set(null);

    return this.http.post<{ id: string }>(`${this.apiUrl}/v1/games`, { isPublic: true, acceptsSpectators: true }).pipe(
      tap(res => {
        this.gameId.set(res.id);
        this.isLoading.set(false);
      }),
      map(res => res.id),
      catchError(err => {
        const msg = this.extractError(err);
        this.error.set(msg);
        this.isLoading.set(false);
        return throwError(() => new Error(msg));
      })
    );
  }

  public loadGame(gameId: string): Observable<GameState> {
    this.isLoading.set(true);
    this.error.set(null);
    this.gameId.set(gameId);
    return this.loadGameState(gameId, false).pipe(tap(() => this.isLoading.set(false)));
  }

  public loadSpectatorGame(gameId: string): Observable<GameState> {
    this.gameId.set(gameId);
    return this.loadGameState(gameId, true).pipe(tap(() => this.isLoading.set(false)));
  }

  public loadGameSilent(gameId: string): Observable<GameState> {
    this.error.set(null);
    this.gameId.set(gameId);
    return this.loadGameState(gameId, true);
  }

  public getGameState(gameId: string): Observable<GameState> {
    return this.http.get<BackendStateView>(`${this.apiUrl}/v1/games/${gameId}/state`).pipe(
      switchMap(view => this.mapState(view)),
      catchError(err => {
        const msg = this.extractError(err);
        this.error.set(msg);
        return throwError(() => new Error(msg));
      })
    );
  }

  private loadGameState(gameId: string, spectator: boolean): Observable<GameState> {
    this.spectatorMode = spectator;
    this.myMulligans = 0;
    return this.http.get<BackendLobby>(`${this.apiUrl}/v1/games/${gameId}/lobby`).pipe(
      tap(lobby => {
        this.lobbyPlayers = lobby.players.map(p => ({ userId: p.userId, order: p.order, username: p.username }));
        const userId = this.authService.getUserId();
        const me = lobby.players.find(p => p.userId === userId);
        this.playerIndex.set(me ? me.order : 0);
      }),
      switchMap(() => this.http.get<BackendStateView>(`${this.apiUrl}/v1/games/${gameId}/state`)),
      switchMap(view => this.mapState(view)),
      tap(state => {
        this.gameState.set(state);
        this.gameStateSubject.next(state);
        // Unirse al grupo para recibir broadcasts (lobby/chat/action-result).
        // Reconnect también reanuda si estaba pausada; si no, es no-op.
        void this.signalService.connect()
          .then(() => spectator
            ? this.signalService.joinAsSpectator(gameId)
            : this.signalService.reconnect(gameId))
          .catch(err => console.error('Signal connect failed:', err));
      }),
      catchError(err => {
        const msg = this.extractError(err);
        this.error.set(msg);
        return throwError(() => new Error(msg));
      })
    );
  }

  public joinGame(gameId: string, password?: string | null): Observable<BackendLobby> {
    return this.http.post<BackendLobby>(`${this.apiUrl}/v1/games/${gameId}/join`,
      { password: password ?? null }).pipe(
      catchError(err => {
        const msg = this.extractError(err);
        return throwError(() => new Error(msg));
      })
    );
  }

  public checkPassword(gameId: string, password: string): Observable<void> {
    return this.joinGame(gameId, password).pipe(map(() => undefined));
  }

  public getGameInfo(gameId: string): Observable<{ gameId: string; status: string }> {
    // Sin snapshot la partida sigue en lobby (el backend responde 404 en /state).
    return this.http.get<BackendStateView>(`${this.apiUrl}/v1/games/${gameId}/state`).pipe(
      map(view => ({ gameId, status: mapGameStatus(view.status) })),
      catchError(err => {
        if (err?.status === 404) {
          return of({ gameId, status: 'IN_LOBBY' });
        }
        return throwError(() => new Error(this.extractError(err)));
      })
    );
  }

  public listGames(filter?: string): Observable<GameListItem[]> {
    return this.http.get<BackendLobby[]>(`${this.apiUrl}/v1/games`).pipe(
      map(lobbies => lobbies
        .filter(l => matchesFilter(l, filter))
        .map(l => ({
          gameId: l.id,
          players: l.players.map(p => ({ playerId: p.userId, order: p.order, username: p.username })),
          status: 'IN_LOBBY',
          createdAt: null,
          isPublic: l.isPublic,
          hasPassword: l.hasPassword ?? false,
          acceptsSpectators: l.acceptsSpectators,
          creatorUsername: l.players.find(p => p.userId === l.creatorId)?.username ?? null
        }))),
      catchError(err => {
        console.error('Failed to list games', err);
        return of([]);
      })
    );
  }

  public getMyActiveGame(): Observable<{ gameId: string; status: string } | null> {
    return this.http.get<BackendGameSummary[]>(`${this.apiUrl}/v1/games/my-resumable`).pipe(
      map(games => games.length > 0
        ? { gameId: games[0].id, status: mapGameStatus(games[0].status) }
        : null),
      catchError(err => {
        console.error('Failed to check active game', err);
        return of(null);
      })
    );
  }

  public listMyResumableGames(): Observable<GameListItem[]> {
    return this.http.get<BackendGameSummary[]>(`${this.apiUrl}/v1/games/my-resumable`).pipe(
      map(games => games.map(g => ({
        gameId: g.id,
        players: (g.players ?? []).map(p => ({ playerId: p.userId, order: p.order, username: p.username })),
        status: mapGameStatus(g.status),
        createdAt: null,
        isPublic: true,
        // Partidas en curso: la contraseña del lobby ya no aplica para retomar.
        hasPassword: false,
        acceptsSpectators: true,
        creatorUsername: null
      }))),
      catchError(err => {
        console.error('Failed to list resumable games', err);
        return of([]);
      })
    );
  }

  public getLobbyState(gameId: string): Observable<LobbyState> {
    return this.http.get<BackendLobby>(`${this.apiUrl}/v1/games/${gameId}/lobby`).pipe(
      switchMap(lobby => this.mapLobby(lobby)),
      catchError(err => {
        console.error('Failed to get lobby state', err);
        return throwError(() => new Error(this.extractError(err)));
      })
    );
  }

  private async resolveDeckName(deckId: string | null): Promise<string | null> {
    if (!deckId) return null;
    const cached = this.deckNames.get(deckId);
    if (cached !== undefined) return cached;
    try {
      const deck = await firstValueFrom(
        this.http.get<{ name: string }>(`${this.apiUrl}/v1/decks/${deckId}`));
      this.deckNames.set(deckId, deck.name);
      return deck.name;
    } catch {
      // Cachea también el fallo (404 por IDOR en mazos ajenos): sin esto,
      // cada broadcast del lobby reintentaba el mismo 404 (FE-017).
      this.deckNames.set(deckId, null);
      return null;
    }
  }

  public getGameLogs(gameId: string): Observable<GameLogEntry[]> {
    return this.http.get<{ turnNumber: number; action: string; result: string | null; actingUserId: string | null; createdAt: string }[]>(
      `${this.apiUrl}/v1/games/${gameId}/logs`
    ).pipe(
      map(logs => logs.map(l => ({
        turnNumber: l.turnNumber,
        action: l.action,
        result: l.result,
        actionType: l.action,
        actingPlayerUsername: null,
        createdAt: new Date(l.createdAt).getTime()
      }))),
      catchError(err => {
        console.error('Failed to get game logs', err);
        return throwError(() => new Error(this.extractError(err)));
      })
    );
  }

  public executeAction(actionRequest: BackendGameActionRequest): Observable<GameActionResponse> {
    const gameId = this.gameId();
    if (!gameId || !this.gameState()) {
      const errorMsg = 'Game not loaded';
      this.error.set(errorMsg);
      return of({ type: 'ERROR', error: errorMsg });
    }

    this.isLoading.set(true);
    this.error.set(null);

    const request = this.toHubRequest(actionRequest);
    this.signalService.executeAction(gameId, request).catch(err => {
      this.isLoading.set(false);
      const msg = err instanceof Error ? err.message : 'Error de conexión';
      this.error.set(msg);
      this.actionResultSubject.next({ error: msg });
    });

    const timeout$ = timer(30000).pipe(
      map(() => ({ error: 'La acción no respondió en 30 segundos', events: undefined } as { gameState?: GameState; error?: string; events?: GameEventDTO[] }))
    );

    return race(this.actionResultSubject.pipe(first()), timeout$).pipe(
      tap(result => {
        this.isLoading.set(false);
        if (result.error) {
          this.error.set(result.error);
        } else if (result.gameState) {
          this.gameState.set(result.gameState);
          this.gameStateSubject.next(result.gameState);
        }
        // Los events ya los setea el listener de action-result (setupHubListeners):
        // hacerlo acá duplicaba los toasts de la acción propia (FE-013).
      }),
      map(result => {
        if (result.error) {
          return { type: 'ERROR', error: result.error } as GameActionResponse;
        }
        return { type: 'GAME_STATE_UPDATE', gameState: result.gameState! } as GameActionResponse;
      })
    );
  }

  public sendStompCommand(destination: string): void {
    const gameId = this.gameId();
    if (!gameId) return;
    if (destination === 'reconnect') {
      this.signalService.reconnect(gameId).catch(err => console.error('Reconnect failed:', err));
    }
  }

  public playCard(cardIndex: number, targetIndex?: number): Observable<GameActionResponse> {
    const game = this.gameState();
    const hand = game?.myBoard?.hand;
    if (!Array.isArray(hand) || hand.length === 0) return of({ type: 'ERROR', error: 'No game state' });

    const card = hand[cardIndex];
    if (!card) return of({ type: 'ERROR', error: 'Card not found in hand' });

    if (card.supertype === 'ENERGY') {
      return this.attachEnergyByCard(card.id, targetIndex);
    }
    if (card.supertype === 'TRAINER') {
      const subtype = card.trainerSubtype ?? '';
      if (subtype === 'Supporter') {
        return this.executeAction({ type: 'PlaySupporter', cardId: card.id });
      }
      if (subtype === 'Stadium') {
        return this.executeAction({ type: 'PlayStadium', cardId: card.id });
      }
      return this.executeAction({ type: 'PlayCard', cardId: card.id });
    }
    return this.executeAction({ type: 'PlayCard', cardId: card.id });
  }

  public evolve(benchIndex: number, evolutionCardIndex: number): Observable<GameActionResponse> {
    const game = this.gameState();
    if (!game?.myBoard) return of({ type: 'ERROR', error: 'No game state' });
    const hand = game.myBoard.hand;
    if (!Array.isArray(hand)) return of({ type: 'ERROR', error: 'No game state' });

    const evolutionCard = hand[evolutionCardIndex];
    if (!evolutionCard) return of({ type: 'ERROR', error: 'Evolution card not found in hand' });

    const target = benchIndex === -1
      ? { zone: 'active', index: 0 }
      : { zone: 'bench', index: benchIndex };
    return this.executeAction({ type: 'Evolve', cardId: evolutionCard.id, target });
  }

  public attachEnergy(cardIndex: number, targetIndex: number): Observable<GameActionResponse> {
    const game = this.gameState();
    const hand = game?.myBoard?.hand;
    if (!Array.isArray(hand) || hand.length === 0) return of({ type: 'ERROR', error: 'No game state' });

    const card = hand[cardIndex];
    if (!card) return of({ type: 'ERROR', error: 'Energy card not found in hand' });

    return this.attachEnergyByCard(card.id, targetIndex);
  }

  private attachEnergyByCard(cardId: string, targetIndex?: number): Observable<GameActionResponse> {
    const target = targetIndex === undefined || targetIndex === -1
      ? { zone: 'active', index: 0 }
      : { zone: 'bench', index: targetIndex };
    return this.executeAction({ type: 'AttachEnergy', cardId, target });
  }

  public retreat(benchPokemonIndex: number): Observable<GameActionResponse> {
    const game = this.gameState();
    if (!game?.myBoard?.activePokemon) return of({ type: 'ERROR', error: 'No active Pokemon' });

    const active = game.myBoard.activePokemon;
    const energiesToDiscard = this.pickRetreatEnergies(active);
    const target = game.myBoard.bench[benchPokemonIndex];
    if (!target) return of({ type: 'ERROR', error: 'No hay Pokémon en esa banca' });

    return this.executeAction({
      type: 'Retreat',
      target: { zone: 'bench', index: benchPokemonIndex },
      energyIdsToDiscard: energiesToDiscard
    });
  }

  public attack(attackName: string): Observable<GameActionResponse> {
    const game = this.gameState();
    if (!game?.myBoard?.activePokemon) return of({ type: 'ERROR', error: 'No active Pokemon' });

    const attack = game.myBoard.activePokemon.pokemonCard?.attacks?.find(a => a.name === attackName);
    if (!attack) return of({ type: 'ERROR', error: `Attack '${attackName}' not found` });

    return this.executeAction({ type: 'Attack', attackName });
  }

  public useAbility(abilityName: string, targetPokemonId?: string): Observable<GameActionResponse> {
    const game = this.gameState();
    const target = targetPokemonId ? this.locateOwnPokemon(targetPokemonId) : undefined;
    return this.executeAction({ type: 'UseAbility', abilityName, target });
  }

  public resolveChoice(choiceId: string, selectedOptionIds: string[]): Observable<GameActionResponse> {
    const type = choiceId.startsWith('attack:') ? 'Attack'
      : choiceId.startsWith('ability:') ? 'UseAbility' : 'PlayCard';
    return this.executeAction({ type, choiceId, selectedOptions: selectedOptionIds });
  }

  public endTurn(): Observable<GameActionResponse> {
    return this.executeAction({ type: 'PassTurn' });
  }

  public concede(): Observable<GameActionResponse> {
    return this.executeAction({ type: 'Concede' });
  }

  public setupPlaceActive(cardId: string): Observable<GameActionResponse> {
    return this.executeAction({ type: 'PlayCard', cardId });
  }

  public setupPlaceBench(cardId: string): Observable<GameActionResponse> {
    return this.executeAction({ type: 'PlayCard', cardId });
  }

  public setupConfirm(): Observable<GameActionResponse> {
    return this.executeAction({ type: 'PassTurn' });
  }

  public mulligan(): Observable<GameActionResponse> {
    const gameId = this.gameId();
    if (!gameId) return of({ type: 'ERROR', error: 'No game loaded' });
    this.isLoading.set(true);
    return new Observable<GameActionResponse>(subscriber => {
      this.signalService.mulligan(gameId)
        .then(() => this.refreshState(gameId))
        .then(state => {
          this.isLoading.set(false);
          this.myMulligans++;
          subscriber.next({ type: 'GAME_STATE_UPDATE', gameState: state });
          subscriber.complete();
        })
        .catch((err: unknown) => {
          this.isLoading.set(false);
          const msg = err instanceof Error ? err.message : 'Mulligan fallido';
          this.error.set(msg);
          subscriber.next({ type: 'ERROR', error: msg });
          subscriber.complete();
        });
    });
  }

  public clearError(): void {
    this.error.set(null);
  }

  public disconnect(): void {
    this.hubSubscription?.unsubscribe();
    this.hubSubscription = undefined;
    void this.signalService.disconnect();
    this.gameId.set(null);
    this.gameState.set(null);
    this.lobbyPlayers = [];
    this.myMulligans = 0;
    // No arrastrar chat/eventos/error de la partida anterior a la siguiente (FE-012).
    this.chatMessages.set([]);
    this.gameEvents.set([]);
    this.error.set(null);
  }

  public sendChatMessage(content: string, senderName: string): void {
    const gameId = this.gameId();
    if (!gameId) return;
    const playerId = this.playerId() ?? 'local';
    void this.signalService.sendChat(gameId, content).catch(() => {});
    const localMsg: ChatMessage = {
      id: crypto.randomUUID(),
      senderId: playerId,
      senderName,
      content,
      timestamp: new Date()
    };
    this.chatMessages.update(m => [...m, localMsg]);
  }

  // ---------- mapeo backend -> modelos UI ----------

  private toHubRequest(request: BackendGameActionRequest): HubGameActionRequest {
    return {
      type: request.type,
      cardId: request.cardId,
      target: request.target as { zone: string; index: number } | undefined,
      attackName: request.attackName,
      energyIdsToDiscard: request.energyIdsToDiscard,
      choiceId: request.choiceId,
      selectedOptions: request.selectedOptions,
      abilityName: request.abilityName,
    };
  }

  private locateOwnPokemon(cardId: string): { zone: 'active' | 'bench'; index: number } | undefined {
    const game = this.gameState();
    if (!game) return undefined;
    if (game.myBoard.activePokemon?.pokemonCard?.id === cardId) {
      return { zone: 'active', index: 0 };
    }
    const benchIndex = game.myBoard.bench.findIndex(p => p.pokemonCard?.id === cardId);
    return benchIndex >= 0 ? { zone: 'bench', index: benchIndex } : undefined;
  }

  private pickRetreatEnergies(active: PokemonInPlay): string[] {
    const cost = active.pokemonCard?.retreatCost ?? 0;
    const picked: string[] = [];
    let value = 0;
    for (const energy of active.attachedEnergies ?? []) {
      if (value >= cost) break;
      picked.push(energy.id);
      value += energy.specialEnergyType === 'DOUBLE_COLORLESS' ? 2 : 1;
    }
    return picked;
  }

  private async mapLobby(lobby: BackendLobby): Promise<LobbyState> {
    const players = await Promise.all(lobby.players.map(async p => ({
      playerId: p.userId,
      username: p.username,
      avatarUrl: null,
      ready: p.isReady,
      hasDeck: p.deckId !== null,
      deckId: p.deckId,
      deckName: await this.resolveDeckName(p.deckId),
      playerOrder: p.order,
    })));
    return {
      gameId: lobby.id,
      creatorId: lobby.creatorId,
      players,
      countdownActive: false,
      countdownEndsAt: null,
      event: null,
      isPublic: lobby.isPublic,
      acceptsSpectators: lobby.acceptsSpectators,
    };
  }

  private setupHubListeners(): void {
    this.hubSubscription?.unsubscribe();
    this.hubSubscription = this.signalService.actionResult$.subscribe(result => {
      if (!result.success) {
        this.isLoading.set(false);
        if (result.pendingChoice) {
          this.actionResultSubject.next({ events: this.mapEvents(result.events) });
        } else {
          this.error.set(result.error ?? 'Acción rechazada');
          this.actionResultSubject.next({ error: result.error ?? 'Acción rechazada' });
        }
      } else {
        this.gameEvents.set(this.mapEvents(result.events));
        void this.refreshState(this.gameId()!).then(
          state => this.actionResultSubject.next({ gameState: state, events: this.mapEvents(result.events) }),
          (err: unknown) => this.actionResultSubject.next({
            error: err instanceof Error ? err.message : 'Error actualizando estado'
          }),
        );
      }
    });
    const stateSub = this.signalService.gameState$.subscribe(view => {
      void this.applyPushedState(view);
    });
    const errorSub = this.signalService.hubError$.subscribe(err => {
      this.error.set(err.message);
      this.actionResultSubject.next({ error: err.message });
    });
    const chatSub = this.signalService.chat$.subscribe(dto => this.handleChatMessage(dto));
    const lobbyPushSub = this.signalService.lobby$.subscribe(payload => {
      void this.mapLobby(payload as BackendLobby)
        .then(mapped => this.lobbyStateSubject.next(mapped))
        .catch(() => {});
    });
    this.hubSubscription.add(lobbyPushSub);
    this.hubSubscription.add(stateSub);
    this.hubSubscription.add(errorSub);
    this.hubSubscription.add(chatSub);
  }

  private async applyPushedState(view: unknown): Promise<void> {
    const gameId = this.gameId();
    if (!gameId) return;
    try {
      const mapped = await this.mapState(view as BackendStateView);
      const current = this.gameState();
      if (current && this.isStateStale(current, mapped)) {
        return;
      }
      this.gameState.set(mapped);
      this.gameStateSubject.next(mapped);
    } catch (err) {
      console.error('Failed to apply pushed state:', err);
    }
  }

  private async refreshState(gameId: string): Promise<GameState> {
    const view = await firstValueFrom(
      this.http.get<BackendStateView>(`${this.apiUrl}/v1/games/${gameId}/state`));
    const mapped = await this.mapState(view);
    this.gameState.set(mapped);
    this.gameStateSubject.next(mapped);
    return mapped;
  }

  private isStateStale(current: GameState, fetched: GameState): boolean {
    if (fetched.status === 'PAUSED' && current.status !== 'PAUSED') return true;
    if (fetched.turnNumber < current.turnNumber) return true;
    return false;
  }

  private handleChatMessage(dto: { senderId: string; text: string; at: string }): void {
    const myId = this.playerId();
    if (dto.senderId === myId) return;
    const known = this.lobbyPlayers.find(p => p.userId === dto.senderId);
    const msg: ChatMessage = {
      id: crypto.randomUUID(),
      senderId: dto.senderId,
      senderName: known?.username ?? 'Oponente',
      content: dto.text,
      timestamp: new Date(dto.at)
    };
    this.chatMessages.update(m => [...m, msg]);
  }

  private mapEvents(events: string[] | undefined): GameEventDTO[] {
    return (events ?? []).map(type => ({
      eventType: type,
      playerId: null,
      playerName: null,
      details: type,
      timestamp: new Date().toISOString(),
    }));
  }

  private async mapState(view: BackendStateView): Promise<GameState> {
    const userId = this.authService.getUserId();
    const order = this.orderOf(userId);
    this.playerIndex.set(order);
    const opponentOrder = 1 - order;
    const [myBoard, opponentBoard] = await Promise.all([
      this.mapBoard(view.boards[order], !this.spectatorMode),
      this.mapBoard(view.boards[opponentOrder], false),
    ]);
    const winner = view.winnerPlayerIndex === null || view.winnerPlayerIndex === undefined
      ? null
      : this.userIdOf(view.winnerPlayerIndex);
    const mulligans = [0, 0];
    mulligans[order] = this.myMulligans;
    return {
      gameId: view.gameId,
      status: STATUS_MAP[view.status] ?? view.status.toUpperCase(),
      currentPlayerIndex: view.currentPlayerIndex,
      turnNumber: view.turnNumber,
      phase: PHASE_MAP[view.phase] ?? view.phase.toUpperCase(),
      players: [this.userIdOf(0), this.userIdOf(1)],
      playerNames: Object.fromEntries(
        this.lobbyPlayers.map(p => [p.userId, p.username] as const)),
      myBoard,
      opponentBoard,
      winner,
      currentSetupPlayerIndex: view.status === 'Setup' ? view.currentPlayerIndex : -1,
      startingPlayerIndex: view.startingPlayerIndex ?? 0,
      pendingChoice: this.mapPendingChoice(view),
      mulliganCounts: mulligans,
      stadiumCard: view.stadiumCardId ? await this.fetchTrainer(view.stadiumCardId) : null,
      spectator: this.spectatorMode,
    };
  }

  private spectatorMode = false;
  private orderOf(userId: string | null): number {
    if (!userId) return 0;
    const found = this.lobbyPlayers.find(p => p.userId === userId);
    return found ? found.order : 0;
  }

  private userIdOf(order: number): string {
    return this.lobbyPlayers.find(p => p.order === order)?.userId ?? '';
  }

  private mapPendingChoice(view: BackendStateView): PendingChoice | null {
    const pending = view.pendingChoice;
    if (!pending) return null;
    return {
      choiceId: pending.choiceId,
      playerId: this.userIdOf(pending.playerIndex),
      abilityName: pending.abilityName ?? '',
      promptLabel: pending.prompt,
      options: pending.options.map(o => ({
        optionId: o.optionId,
        label: o.label,
        value: o.optionId,
        imageUrl: o.imageUrl ?? undefined,
      })),
    };
  }

  private async mapBoard(view: BackendBoardView, isOwner: boolean): Promise<BoardDto> {
    const [active, bench] = await Promise.all([
      view.active ? this.mapPokemon(view.active, true) : Promise.resolve(null),
      Promise.all(view.bench.map(p => this.mapPokemon(p, false))),
    ]);
    return {
      activePokemon: active,
      bench,
      hand: isOwner ? await this.mapHand(view.hand ?? []) : view.handCount,
      deck: view.deckCount,
      prizeCards: view.prizeCount,
      discardPile: view.discard.length,
    };
  }

  private async mapPokemon(view: BackendPokemon, active: boolean): Promise<PokemonInPlay> {
    // Un id fuera del catálogo no debe romper todo el mapeo del estado (FE-018):
    // se degrada a una carta placeholder con los datos del tablero.
    const card = await this.fetchPokemon(view.cardId)
      .catch(() => this.placeholderPokemon(view));
    const energies = await Promise.all(view.energies.map(id => this.fetchEnergy(id)));
    const tool = view.tool ? await this.fetchTrainer(view.tool).catch(() => null) : null;
    return {
      pokemonCard: card,
      damageCounters: view.damageCounters,
      attachedEnergies: energies.filter((e): e is EnergyCard => e !== null),
      attachedTool: tool,
      specialConditions: view.statuses.map(s => CONDITION_MAP[s] ?? s.toUpperCase()),
      active,
      justPlayed: false,
      position: null,
      turnsInPlay: null,
    };
  }

  private async mapHand(hand: BackendHandCard[]): Promise<Card[]> {
    return hand.map(c => ({
      id: c.id,
      name: c.name,
      imageUrl: c.imageUrl,
      supertype: c.supertype.toUpperCase(),
      subtypes: (c.subtypes ?? []).map(s => s.toUpperCase()),
      rules: [],
      setId: null,
      evolvesFrom: c.evolvesFrom ?? null,
      energyType: c.energyType?.toUpperCase() ?? null,
      specialEnergyType: c.specialEnergy?.toUpperCase() ?? null,
      trainerSubtype: c.trainerSubtype?.toUpperCase() ?? null,
    }));
  }

  private async fetchPokemon(cardId: string): Promise<PokemonCard> {
    const cached = this.cardCache.get(cardId);
    if (cached && cached.supertype === 'POKEMON') {
      return cached as PokemonCard;
    }
    const dto = await firstValueFrom(
      this.http.get<Record<string, unknown>>(`${this.apiUrl}/v1/cards/${cardId}`));
    const card = this.toPokemonCard(dto);
    this.cardCache.set(cardId, card);
    return card;
  }

  private async fetchEnergy(cardId: string): Promise<EnergyCard | null> {
    const cached = this.cardCache.get(cardId);
    if (cached) {
      return cached.supertype === 'ENERGY' ? (cached as EnergyCard) : null;
    }
    try {
      const dto = await firstValueFrom(
        this.http.get<Record<string, unknown>>(`${this.apiUrl}/v1/cards/${cardId}`));
      const supertype = String(dto['supertype'] ?? '').toUpperCase();
      if (supertype !== 'ENERGY') return null;
      const card: EnergyCard = {
        id: String(dto['id']),
        name: String(dto['name']),
        imageUrl: String(dto['imageUrl'] ?? ''),
        supertype: 'ENERGY',
        subtypes: [],
        rules: [],
        setId: null,
        energyType: String(dto['energyType'] ?? 'COLORLESS').toUpperCase(),
        specialEnergyType: dto['specialEnergy'] ? String(dto['specialEnergy']).toUpperCase() : undefined,
      };
      this.cardCache.set(cardId, card);
      return card;
    } catch {
      return null;
    }
  }

  private async fetchTrainer(cardId: string): Promise<TrainerCard | null> {
    const cached = this.cardCache.get(cardId);
    if (cached) {
      return cached.supertype === 'TRAINER' ? (cached as TrainerCard) : null;
    }
    try {
      const dto = await firstValueFrom(
        this.http.get<Record<string, unknown>>(`${this.apiUrl}/v1/cards/${cardId}`));
      const card: TrainerCard = {
        id: String(dto['id']),
        name: String(dto['name']),
        imageUrl: String(dto['imageUrl'] ?? ''),
        supertype: 'TRAINER',
        subtypes: [],
        rules: [],
        setId: null,
      };
      this.cardCache.set(cardId, card);
      return card;
    } catch {
      return null;
    }
  }

  /** Carta placeholder cuando el catálogo no tiene el id (FE-018). */
  private placeholderPokemon(view: BackendPokemon): PokemonCard {
    return {
      id: view.cardId,
      name: view.name || view.cardId,
      imageUrl: '',
      supertype: 'POKEMON',
      subtypes: [],
      rules: [],
      setId: null,
      hp: view.hp,
      evolvesFrom: null,
      type: 'COLORLESS',
      weaknesses: [],
      resistances: [],
      retreatCost: 0,
      attacks: [],
      abilities: [],
    };
  }

  private toPokemonCard(dto: Record<string, unknown>): PokemonCard {
    const str = (value: unknown, fallback = ''): string =>
      typeof value === 'string' ? value : fallback;
    const num = (value: unknown, fallback = 0): number =>
      typeof value === 'number' ? value : fallback;
    const asArray = (value: unknown): Record<string, unknown>[] =>
      Array.isArray(value) ? value.filter((v): v is Record<string, unknown> =>
        typeof v === 'object' && v !== null) : [];
    return {
      id: str(dto['id']),
      name: str(dto['name']),
      imageUrl: str(dto['imageUrl']),
      supertype: 'POKEMON',
      subtypes: [],
      rules: [],
      setId: null,
      hp: num(dto['hp'], 60),
      evolvesFrom: typeof dto['evolvesFrom'] === 'string' ? dto['evolvesFrom'] : null,
      type: str(dto['pokemonType'], 'COLORLESS').toUpperCase(),
      weaknesses: [],
      resistances: [],
      retreatCost: num(dto['retreatCost']),
      attacks: asArray(dto['attacks']).map(a => ({
        name: str(a['name']),
        description: str(a['description']),
        damage: str(a['damage']),
        cost: Array.isArray(a['cost']) ? a['cost'].map(c => String(c).toUpperCase()) : [],
      })),
      abilities: asArray(dto['abilities']).map(a => ({
        name: str(a['name']),
        description: str(a['text']),
        type: str(a['type']).toUpperCase(),
      })),
    };
  }

  private extractError(err: unknown): string {
    const response = err as { error?: { message?: string; error?: string; title?: string; detail?: string }; message?: string };
    if (response?.error?.message) {
      return response.error.message;
    }
    if (response?.error?.detail) {
      return response.error.detail;
    }
    if (typeof response?.error === 'string') {
      return response.error;
    }
    if (response?.message) {
      return response.message;
    }
    return 'Error desconocido';
  }
}
