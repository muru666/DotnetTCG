import {
  ChangeDetectionStrategy, Component, OnDestroy, OnInit,
  inject, signal, computed
} from '@angular/core';
import { Router, ActivatedRoute } from '@angular/router';
import { FormsModule } from '@angular/forms';
import { HttpClient } from '@angular/common/http';
import { Subscription } from 'rxjs';

import { AuthService } from '../../../../core/services/auth.service';
import { BackgroundImageService } from '../../../../core/services/background-image.service';
import { GameService } from '../../../../shared/services/api/game.service';
import { GameSignalService } from '../../../../shared/services/websocket/game-signal.service';
import { DeckBuilderService, Deck } from '../../../../shared/services/api/deck-builder.service';
import { LobbyState } from '../../../../shared/models/dtos/lobby-state.model';
import { environment } from '../../../../../environments/environment';

@Component({
  selector: 'app-game-lobby-page',
  standalone: true,
  imports: [FormsModule],
  templateUrl: './game-lobby-page.html',
  styleUrl: './game-lobby-page.css',
  changeDetection: ChangeDetectionStrategy.OnPush
})
export class GameLobbyPageComponent implements OnInit, OnDestroy {
  private readonly route = inject(ActivatedRoute);
  private readonly router = inject(Router);
  private readonly authService = inject(AuthService);
  private readonly bgService = inject(BackgroundImageService);
  private readonly gameService = inject(GameService);
  private readonly signalService = inject(GameSignalService);
  private readonly deckService = inject(DeckBuilderService);
  private readonly http = inject(HttpClient);

  protected readonly backgroundImage = signal(this.bgService.getAuthBackgroundImage());

  protected gameId = signal<string | null>(null);
  protected lobbyState = signal<LobbyState | null>(null);
  protected decks = signal<Deck[]>([]);
  protected selectedDeckId = signal<string>('');
  protected error = signal<string | null>(null);
  protected isLoading = signal(false);

  protected isPublicSetting = signal(true);
  protected acceptsSpectatorsSetting = signal(true);
  protected passwordInput = signal('');
  protected passwordSaved = signal(false);
  protected isSpectator = signal(false);

  private readonly currentUserId = this.authService.getUserId();
  private hubSubs?: Subscription;
  private joinPassword: string | null = null;

  protected readonly myPlayer = computed(() => {
    const state = this.lobbyState();
    if (!state || !this.currentUserId) return null;
    return state.players.find(p => p.playerId === this.currentUserId) ?? null;
  });

  protected readonly amCreator = computed(() => {
    const state = this.lobbyState();
    return !!state && state.creatorId === this.currentUserId;
  });

  protected readonly allReady = computed(() => {
    const state = this.lobbyState();
    return !!state && state.players.length >= 2 && state.players.every(p => p.ready);
  });

  protected readonly canReady = computed(() => {
    return !!this.myPlayer()?.hasDeck;
  });

  // ── Lifecycle ────────────────────────────────────────────────────────────────

  ngOnInit(): void {
    const gameId = this.route.snapshot.paramMap.get('gameId');
    if (!gameId) { this.router.navigate(['/lobby']); return; }
    this.gameId.set(gameId);

    const navState = history.state as { password?: string; spectator?: boolean } | null;
    this.joinPassword = navState?.password ?? null;
    this.isSpectator.set(navState?.spectator === true);

    this.loadInitialState(gameId);
  }

  ngOnDestroy(): void {
    this.hubSubs?.unsubscribe();
    if (!this.isSpectator()) {
      const id = this.gameId();
      if (id) void this.signalService.leaveLobby(id).catch(() => {});
    }
  }

  // ── Init helpers ─────────────────────────────────────────────────────────────

  private loadDecks(): void {
    this.deckService.getUserDecks().subscribe({
      next: decks => {
        this.decks.set(decks);
        const valid = decks.filter(d => d.isValid);
        if (valid.length > 0) this.selectedDeckId.set(valid[0].id);
      }
    });
  }

  private navigateToGame(gameId: string): void {
    if (this.isSpectator()) {
      this.router.navigate(['/game', 'spectator', gameId]);
    } else {
      this.router.navigate(['/game', gameId]);
    }
  }

  private loadInitialState(gameId: string): void {
    this.isLoading.set(true);
    this.gameService.getGameInfo(gameId).subscribe({
      next: gameInfo => {
        const status = gameInfo.status;
        if (status !== 'IN_LOBBY') {
          this.navigateToGame(gameId);
          return;
        }
        this.gameService.getLobbyState(gameId).subscribe({
          next: state => {
            const alreadyIn = state.players.some(p => p.playerId === this.currentUserId);
            if (!this.isSpectator() && !alreadyIn && state.players.length >= 2) {
              this.router.navigate(['/lobby'], { state: { error: 'La partida está llena' } });
              return;
            }

            this.lobbyState.set(state);
            this.isPublicSetting.set(state.isPublic !== false);
            this.acceptsSpectatorsSetting.set(state.acceptsSpectators !== false);
            this.isLoading.set(false);

            if (!this.isSpectator()) {
              this.loadDecks();
            }
            this.connectHub(gameId);
          },
          error: () => {
            this.isLoading.set(false);
            this.router.navigate(['/lobby'], { state: { error: 'No se pudo cargar el lobby' } });
          }
        });
      },
      error: () => {
        this.isLoading.set(false);
        this.router.navigate(['/lobby'], { state: { error: 'Partida no encontrada o no disponible' } });
      }
    });
  }

  private connectHub(gameId: string): void {
    this.signalService.connect()
      .then(() => {
        this.hubSubs = new Subscription();
        const lobbySub = this.gameService.lobbyState$.subscribe(state => {
          this.handleLobbyMessage(state);
        });
        const startedSub = this.signalService.gameStarted$.subscribe(event => {
          if (event.gameId === gameId) {
            this.navigateToGame(gameId);
          }
        });
        const errorSub = this.signalService.hubError$.subscribe(err => {
          this.error.set(err.message);
          if (err.code === 'Game.NotFound') {
            this.router.navigate(['/lobby'], { state: { error: err.message } });
          }
        });
        this.hubSubs.add(lobbySub);
        this.hubSubs.add(startedSub);
        this.hubSubs.add(errorSub);

        if (this.isSpectator()) {
          void this.signalService.joinAsSpectator(gameId).catch(err => {
            this.error.set('No se pudo conectar como espectador');
            console.error('Spectator join error', err);
          });
        } else {
          const joinPayload = this.joinPassword ? { password: this.joinPassword } : undefined;
          void this.signalService.joinLobby(gameId, joinPayload?.password ?? null).catch(err => {
            this.error.set('No se pudo unir a la partida');
            console.error('Join error', err);
          });
        }
      })
      .catch(() => {
        this.error.set('No se pudo conectar al lobby');
      });
  }

  // ── Lobby message handler ─────────────────────────────────────────────────

  private handleLobbyMessage(state: LobbyState): void {
    if (state.gameId !== this.gameId()) return;
    this.lobbyState.set(state);
    this.isPublicSetting.set(state.isPublic !== false);
    this.acceptsSpectatorsSetting.set(state.acceptsSpectators !== false);

    // If we were removed from the game, go back (spectators are never in players list)
    if (!this.isSpectator()) {
      const stillIn = state.players.some(p => p.playerId === this.currentUserId);
      if (!stillIn) {
        this.router.navigate(['/lobby'], {
          state: { error: 'Saliste de la partida' }
        });
      }
    }
  }

  // ── User actions ─────────────────────────────────────────────────────────────

  protected togglePublic(): void {
    const newValue = !this.isPublicSetting();
    this.isPublicSetting.set(newValue);
    const gameId = this.gameId();
    if (!gameId) return;
    if (newValue) {
      this.updateSettings(gameId, { isPublic: true });
      this.passwordInput.set('');
      this.passwordSaved.set(false);
    } else {
      this.updateSettings(gameId, { isPublic: false });
    }
  }

  protected toggleAcceptsSpectators(): void {
    const newValue = !this.acceptsSpectatorsSetting();
    this.acceptsSpectatorsSetting.set(newValue);
    const gameId = this.gameId();
    if (!gameId) return;
    this.updateSettings(gameId, { acceptsSpectators: newValue });
  }

  protected savePassword(): void {
    const gameId = this.gameId();
    const pwd = this.passwordInput().trim();
    if (!gameId || !pwd) return;
    this.updateSettings(gameId, { isPublic: false, password: pwd });
    this.passwordSaved.set(true);
  }

  private updateSettings(gameId: string, patch: { isPublic?: boolean; acceptsSpectators?: boolean; password?: string | null }): void {
    const current = this.lobbyState();
    this.http.put(`${environment.apiUrl}/v1/games/${gameId}/settings`, {
      isPublic: patch.isPublic ?? current?.isPublic ?? true,
      acceptsSpectators: patch.acceptsSpectators ?? current?.acceptsSpectators ?? true,
      password: patch.password ?? null,
    }).subscribe({
      next: () => this.refreshLobby(gameId),
      error: () => this.error.set('No se pudo actualizar la partida'),
    });
  }

  private refreshLobby(gameId: string): void {
    this.gameService.getLobbyState(gameId).subscribe({
      next: state => this.handleLobbyMessage(state),
      error: () => this.error.set('No se pudo actualizar el lobby'),
    });
  }

  protected confirmDeck(): void {
    const deckId = this.selectedDeckId();
    const gameId = this.gameId();
    if (!deckId || !gameId) return;
    this.signalService.selectDeck(gameId, deckId).catch(() => {
      this.error.set('No se pudo seleccionar el mazo');
    });
  }

  protected toggleReady(): void {
    const gameId = this.gameId();
    if (!gameId) return;
    const ready = !(this.myPlayer()?.ready ?? false);
    this.signalService.setReady(gameId, ready).catch(() => {
      this.error.set('No se pudo cambiar el estado');
    });
  }

  protected removePlayer(playerId: string): void {
    const gameId = this.gameId();
    if (!gameId) return;
    this.http.post(`${environment.apiUrl}/v1/games/${gameId}/remove-player`, { targetUserId: playerId }).subscribe({
      next: () => this.refreshLobby(gameId),
      error: () => this.error.set('No se pudo expulsar al jugador'),
    });
  }

  protected goToDeckBuilder(): void {
    this.router.navigate(['/deck-builder']);
  }

  protected leaveAndGoBack(): void {
    const gameId = this.gameId();
    if (gameId) {
      void this.signalService.leaveLobby(gameId)
        .catch(() => {})
        .finally(() => this.router.navigate(['/lobby']));
    } else {
      this.router.navigate(['/lobby']);
    }
  }

  protected get validDecks(): Deck[] {
    return this.decks().filter(d => d.isValid);
  }

  protected getInitial(username: string): string {
    return username ? username.charAt(0).toUpperCase() : '?';
  }
}
