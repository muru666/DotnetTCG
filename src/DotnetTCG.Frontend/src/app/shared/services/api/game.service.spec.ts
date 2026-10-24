import { TestBed } from '@angular/core/testing';
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
  executeAction = () => Promise.resolve();
  joinAsSpectator = () => Promise.resolve();
  reconnect = () => Promise.resolve();
}

describe('GameService', () => {
  let service: GameService;
  let httpMock: HttpTestingController;

  beforeEach(() => {
    sessionStorage.clear();
    TestBed.configureTestingModule({
      providers: [
        provideHttpClient(),
        provideHttpClientTesting(),
        { provide: GameSignalService, useValue: new FakeSignalService() },
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
});
