import { TestBed } from '@angular/core/testing';
import { ActivatedRoute, Router } from '@angular/router';
import { Subject, of } from 'rxjs';
import { vi } from 'vitest';
import { GameLobbyPageComponent } from './game-lobby-page';
import { GameService } from '../../../../shared/services/api/game.service';
import { GameSignalService } from '../../../../shared/services/websocket/game-signal.service';
import { DeckBuilderService } from '../../../../shared/services/api/deck-builder.service';
import { AuthService } from '../../../../core/services/auth.service';
import { BackgroundImageService } from '../../../../core/services/background-image.service';

const GAME_ID = 'g1';

function setup() {
  const gameStartedSubject = new Subject<{ gameId: string }>();
  const hubErrorSubject = new Subject<{ code: string; message: string }>();
  const lobbyState$ = new Subject<unknown>();

  const signalService = {
    connect: vi.fn(() => Promise.resolve()),
    joinLobby: vi.fn(() => Promise.resolve()),
    leaveLobby: vi.fn(() => Promise.resolve()),
    gameStarted$: gameStartedSubject.asObservable(),
    hubError$: hubErrorSubject.asObservable(),
  };
  const gameService = {
    getGameInfo: vi.fn(() => of({ gameId: GAME_ID, status: 'IN_LOBBY' })),
    getLobbyState: vi.fn(() => of({
      gameId: GAME_ID,
      creatorId: 'u1',
      isPublic: true,
      acceptsSpectators: true,
      players: [
        { playerId: 'u1', order: 0, deckId: null, ready: false, username: 'me', hasDeck: false },
      ],
    })),
    lobbyState$,
  };
  const deckService = { getUserDecks: vi.fn(() => of([])) };
  const router = { navigate: vi.fn(() => Promise.resolve(true)) };
  const route = {
    snapshot: { paramMap: { get: (key: string) => (key === 'gameId' ? GAME_ID : null) } },
  };

  TestBed.configureTestingModule({
    providers: [
      { provide: ActivatedRoute, useValue: route },
      { provide: Router, useValue: router },
      { provide: GameSignalService, useValue: signalService },
      { provide: GameService, useValue: gameService },
      { provide: DeckBuilderService, useValue: deckService },
      { provide: AuthService, useValue: { getUserId: () => 'u1', getToken: () => 't' } },
      { provide: BackgroundImageService, useValue: { getAuthBackgroundImage: () => '' } },
    ],
  });

  // Instanciar en contexto de inyección sin renderizar el template.
  const component = TestBed.runInInjectionContext(() => new GameLobbyPageComponent());
  component.ngOnInit();
  // connectHub suscribe en el .then() de connect(): dejar correr los microtareas.
  const flush = () => new Promise<void>(resolve => setTimeout(resolve, 0));

  return { component, signalService, gameStartedSubject, router, flush };
}

describe('GameLobbyPageComponent ngDestroy', () => {
  it('al navegar al tablero NO invoca leaveLobby (regresión FE-001)', async () => {
    const { component, signalService, gameStartedSubject, router, flush } = setup();
    await flush();

    gameStartedSubject.next({ gameId: GAME_ID });
    expect(router.navigate).toHaveBeenCalledWith(['/game', GAME_ID]);

    component.ngOnDestroy();

    expect(signalService.leaveLobby).not.toHaveBeenCalled();
  });

  it('al salir del lobby sin haber navegado sí invoca leaveLobby', async () => {
    const { component, signalService, flush } = setup();
    await flush();

    component.ngOnDestroy();

    expect(signalService.leaveLobby).toHaveBeenCalledWith(GAME_ID);
  });
});
