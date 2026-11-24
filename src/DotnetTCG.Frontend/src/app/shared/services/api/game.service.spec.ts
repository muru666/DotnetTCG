import { TestBed } from '@angular/core/testing';
import { effect } from '@angular/core';
import { HttpTestingController, provideHttpClientTesting } from '@angular/common/http/testing';
import { provideHttpClient } from '@angular/common/http';
import { Subject } from 'rxjs';
import { GameService } from './game.service';
import { GameSignalService } from '../websocket/game-signal.service';
import { environment } from '../../../../environments/environment';

const API = environment.apiUrl;

class FakeSignalService {
  actionResult$ = new Subject<unknown>();
  gameState$ = new Subject<unknown>();
  chat$ = new Subject<unknown>();
  hubError$ = new Subject<unknown>();
  lobby$ = new Subject<unknown>();
  connectionStatus$ = new Subject<boolean>();
  connect = () => Promise.resolve();
  disconnect = () => Promise.resolve();
  executeAction = () => Promise.resolve();
  joinAsSpectator = () => Promise.resolve();
  reconnect = () => Promise.resolve();
}

/** Vista mínima de GET /v1/games/{id}/state para los tests de mapeo. */
function minimalStateView(cardId = 'missing'): Record<string, unknown> {
  return {
    gameId: 'g1',
    status: 'InProgress',
    phase: 'Main',
    turnNumber: 3,
    currentPlayerIndex: 0,
    viewerPlayerIndex: 0,
    boards: [
      {
        active: {
          cardId, name: 'Missing', hp: 60, hpRemaining: 60, damageCounters: 0,
          energies: [], tool: null, statuses: [], totalEnergy: 0,
        },
        bench: [], deckCount: 5, prizeCount: 6, discard: [], hand: null, handCount: 0,
      },
      {
        active: null, bench: [], deckCount: 5, prizeCount: 6,
        discard: [], hand: null, handCount: 0,
      },
    ],
    stadiumCardId: null,
    pendingChoice: null,
    winnerPlayerIndex: null,
    suddenDeath: false,
    startingPlayerIndex: 0,
  };
}

describe('GameService', () => {
  let service: GameService;
  let httpMock: HttpTestingController;
  let fakeSignal: FakeSignalService;

  beforeEach(() => {
    sessionStorage.clear();
    fakeSignal = new FakeSignalService();
    TestBed.configureTestingModule({
      providers: [
        provideHttpClient(),
        provideHttpClientTesting(),
        { provide: GameSignalService, useValue: fakeSignal },
      ],
    });
    httpMock = TestBed.inject(HttpTestingController);
  });

  afterEach(() => httpMock.verify());

  it('createGame hace POST y guarda el id', () => {
    service = TestBed.inject(GameService);
    let created = '';
    service.createGame().subscribe(id => (created = id));

    const req = httpMock.expectOne(`${API}/v1/games`);
    expect(req.request.method).toBe('POST');
    expect(req.request.body).toEqual({ isPublic: true, acceptsSpectators: true });
    req.flush({ id: 'game-9' });

    expect(created).toBe('game-9');
    expect(service.gameId()).toBe('game-9');
  });

  it('getGameInfo con 404 cae a IN_LOBBY', () => {
    service = TestBed.inject(GameService);
    let info: { gameId: string; status: string } | null = null;
    service.getGameInfo('g1').subscribe(v => (info = v));

    httpMock.expectOne(`${API}/v1/games/g1/state`)
      .flush({ detail: 'Game not found' }, { status: 404, statusText: 'Not Found' });

    expect(info).toEqual({ gameId: 'g1', status: 'IN_LOBBY' });
  });

  it('getGameInfo con estado mapea el status', () => {
    service = TestBed.inject(GameService);
    let info: { gameId: string; status: string } | null = null;
    service.getGameInfo('g1').subscribe(v => (info = v));

    httpMock.expectOne(`${API}/v1/games/g1/state`)
      .flush({ gameId: 'g1', status: 'InProgress' });

    expect(info).toEqual({ gameId: 'g1', status: 'IN_PROGRESS' });
  });

  it('getLobbyState mapea los jugadores a playerId', async () => {
    service = TestBed.inject(GameService);
    let players: string[] = [];
    service.getLobbyState('g1').subscribe(state => {
      players = state.players.map(p => p.playerId);
    });

    const lobbyReq = httpMock.expectOne(`${API}/v1/games/g1/lobby`);
    expect(lobbyReq.request.method).toBe('GET');
    lobbyReq.flush({
      id: 'g1',
      creatorId: 'u1',
      isPublic: true,
      acceptsSpectators: true,
      players: [
        { userId: 'u1', order: 0, deckId: null, isReady: false, username: 'ash' },
        { userId: 'u2', order: 1, deckId: null, isReady: true, username: 'misty' },
      ],
    });

    // mapLobby es async: esperar el microtask del Promise.all.
    await new Promise(resolve => setTimeout(resolve, 0));
    expect(players).toEqual(['u1', 'u2']);
  });

  it('listGames expone hasPassword del backend (FE-004)', () => {
    service = TestBed.inject(GameService);
    let items: { gameId: string; hasPassword: boolean }[] = [];
    service.listGames().subscribe(v => (items = v));

    httpMock.expectOne(`${API}/v1/games`).flush([
      {
        id: 'g1',
        creatorId: 'u1',
        isPublic: true,
        acceptsSpectators: true,
        hasPassword: true,
        players: [{ userId: 'u1', order: 0, deckId: null, isReady: false, username: 'ash' }],
      },
      {
        id: 'g2',
        creatorId: 'u2',
        isPublic: false,
        acceptsSpectators: true,
        hasPassword: false,
        players: [{ userId: 'u2', order: 0, deckId: null, isReady: false, username: 'misty' }],
      },
    ]);

    expect(items[0].hasPassword).toBe(true);
    expect(items[1].hasPassword).toBe(false);
  });

  it('disconnect limpia chat, eventos y error (FE-012)', () => {
    service = TestBed.inject(GameService);
    service.chatMessages.set([
      { senderId: 'u1', content: 'hola', timestamp: new Date() },
    ]);
    service.gameEvents.set([{ eventType: 'Attack' } as never]);
    service.error.set('boom');

    service.disconnect();

    expect(service.chatMessages()).toEqual([]);
    expect(service.gameEvents()).toEqual([]);
    expect(service.error()).toBeNull();
  });

  it('una acción propia setea gameEvents una sola vez (FE-013)', async () => {
    service = TestBed.inject(GameService);
    service.gameId.set('g1');
    service.gameState.set({ gameId: 'g1' } as never);

    // Cuenta los cambios del signal: cada set (con referencia nueva) lo re-ejecuta.
    let sets = 0;
    TestBed.runInInjectionContext(() => {
      effect(() => {
        service.gameEvents();
        sets++;
      });
    });
    await new Promise(resolve => setTimeout(resolve, 0));
    const baseline = sets;

    // Acción en vuelo: queda esperando el action-result (carrera interna).
    service.executeAction({ type: 'PassTurn' }).subscribe();

    // Llega el action-result del hub: el listener setea los events y refresca.
    fakeSignal.actionResult$.next({ success: true, events: ['Attack'] });
    httpMock.expectOne(`${API}/v1/games/g1/state`).flush(minimalStateView());
    // mapState corre en un microtask después del flush del estado.
    await new Promise(resolve => setTimeout(resolve, 0));
    httpMock.expectOne(`${API}/v1/cards/missing`)
      .flush({}, { status: 404, statusText: 'Not Found' });
    await new Promise(resolve => setTimeout(resolve, 0));

    // Antes del fix había dos set (listener + tap de la carrera) = toasts dobles.
    expect(sets - baseline).toBe(1);
    expect(service.gameEvents()).toHaveLength(1);
  });

  it('getGameState degrada con una carta fuera del catálogo (FE-018)', async () => {
    service = TestBed.inject(GameService);
    let state: { myBoard?: { activePokemon?: { pokemonCard?: { id: string } } } } | null = null;
    let failed = false;

    service.getGameState('g1').subscribe({
      next: s => (state = s as typeof state),
      error: () => (failed = true),
    });

    httpMock.expectOne(`${API}/v1/games/g1/state`).flush(minimalStateView('no-en-catalogo'));
    await new Promise(resolve => setTimeout(resolve, 0));
    httpMock.expectOne(`${API}/v1/cards/no-en-catalogo`)
      .flush({ message: 'not found' }, { status: 404, statusText: 'Not Found' });
    await new Promise(resolve => setTimeout(resolve, 0));

    expect(failed).toBe(false);
    expect(state).not.toBeNull();
    expect(state!.myBoard!.activePokemon!.pokemonCard!.id).toBe('no-en-catalogo');
  });

  it('cachea el 404 del nombre de mazo ajeno (FE-017)', async () => {
    service = TestBed.inject(GameService);
    const lobby = (deckId: string | null) => ({
      id: 'g1',
      creatorId: 'u2',
      isPublic: true,
      acceptsSpectators: true,
      players: [
        { userId: 'u2', order: 0, deckId, isReady: false, username: 'otro' },
      ],
    });

    service.getLobbyState('g1').subscribe();
    httpMock.expectOne(`${API}/v1/games/g1/lobby`).flush(lobby('d1'));
    // resolveDeckName corre en un microtask después del flush del lobby.
    await new Promise(resolve => setTimeout(resolve, 0));
    httpMock.expectOne(`${API}/v1/decks/d1`)
      .flush({}, { status: 404, statusText: 'Not Found' });
    await new Promise(resolve => setTimeout(resolve, 0));

    // Segundo broadcast del lobby: el fallo ya está cacheado, sin nuevo request.
    service.getLobbyState('g1').subscribe();
    httpMock.expectOne(`${API}/v1/games/g1/lobby`).flush(lobby('d1'));
    await new Promise(resolve => setTimeout(resolve, 0));
    httpMock.expectNone((req: { url: string }) => req.url === `${API}/v1/decks/d1`);
  });
});
