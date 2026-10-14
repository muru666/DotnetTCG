import { Component, DestroyRef, inject, signal } from '@angular/core';
import { HttpErrorResponse } from '@angular/common/http';
import { FormControl, FormGroup, ReactiveFormsModule, Validators } from '@angular/forms';
import { Router } from '@angular/router';
import { catchError, firstValueFrom, of } from 'rxjs';
import { toApiError } from '../../../core/http/api-error';
import { SessionStore } from '../../../core/auth/session';
import { GameHubService } from '../../../core/realtime/game-hub.service';
import { SoundService } from '../../../core/sound/sound';
import { DecksApi } from '../../decks/data-access/decks.api';
import { DeckDto } from '../../decks/models/deck.models';
import { GamesApi } from '../data-access/games.api';
import { GameStartedEvent, HubError, LobbyDto } from '../models/lobby.models';

type LobbyScreen =
  | { readonly kind: 'loading' }
  | { readonly kind: 'ready' }
  | { readonly kind: 'error'; readonly message: string };

@Component({
  imports: [ReactiveFormsModule],
  template: `
    <section aria-labelledby="lobby-title">
      <h1 id="lobby-title">Lobby</h1>
      @if (hub.state() === 'reconnecting' || hub.state() === 'connecting') {
        <p role="status">Conectando al lobby…</p>
      }
      @if (screen().kind === 'loading') {
        <p role="status">Cargando partidas…</p>
      } @else if (screen().kind === 'error') {
        <p class="error" role="alert">{{ screenError() }}</p>
      } @else {
        <form class="create-row" [formGroup]="createForm" (ngSubmit)="create()">
          <label class="check">
            <input type="checkbox" formControlName="isPublic" /> Pública
          </label>
          <label class="visually-hidden" for="game-password">Contraseña (opcional)</label>
          <input
            id="game-password"
            type="password"
            formControlName="password"
            placeholder="Contraseña (opcional)"
            autocomplete="off"
          />
          <button type="submit">Crear partida</button>
        </form>
        @if (lobbies().length === 0) {
          <p class="empty">No hay partidas abiertas. Creá una para empezar.</p>
        } @else {
          <ul class="lobby-list">
            @for (game of lobbies(); track game.id) {
              <li class="lobby-row">
                <button
                  type="button"
                  class="lobby-select"
                  (click)="select(game.id)"
                  [attr.aria-expanded]="selectedId() === game.id"
                >
                  Partida {{ game.id.slice(0, 8) }}… · {{ game.players.length }}/2
                  @if (!game.isPublic) {
                    <span aria-label="Privada">🔒</span>
                  }
                </button>
                @if (selectedId() === game.id) {
                  <div class="lobby-detail">
                    <h2>Jugadores</h2>
                    <ul>
                      @for (player of selected()?.players ?? []; track player.userId) {
                        <li>
                          {{ player.order === 0 ? 'Anfitrión' : 'Rival' }}
                          @if (player.deckId !== null) {
                            <span class="deck-ok">con mazo</span>
                          }
                          @if (player.isReady) {
                            <span class="ready">listo</span>
                          }
                        </li>
                      }
                    </ul>
                    @if (!isMember()) {
                      @if (selected()?.isPublic === false) {
                        <label>
                          Contraseña
                          <input
                            type="password"
                            [formControl]="joinPassword"
                            autocomplete="off"
                          />
                        </label>
                      }
                      <button type="button" (click)="join()">Unirse</button>
                    } @else {
                      <label>
                        Mi mazo
                        <select (change)="selectDeck(deckSelect.value)" #deckSelect>
                          <option value="">Sin mazo</option>
                          @for (deck of myDecks(); track deck.id) {
                            <option [value]="deck.id" [selected]="myPlayer()?.deckId === deck.id">
                              {{ deck.name }}{{ deck.isValid ? '' : ' (borrador)' }}
                            </option>
                          }
                        </select>
                      </label>
                      <button type="button" (click)="toggleReady()">
                        {{ myPlayer()?.isReady === true ? 'No listo' : 'Listo' }}
                      </button>
                      <button type="button" class="primary" (click)="start()">Empezar</button>
                      <button type="button" (click)="leave()">Salir</button>
                    }
                  </div>
                }
              </li>
            }
          </ul>
        }
      }
      @if (hubError() !== null) {
        <p class="error" role="alert">{{ hubError() }}</p>
      }
    </section>
  `,
  styles: [
    `
      .create-row {
        display: flex;
        gap: 0.5rem;
        align-items: center;
        margin-bottom: 1rem;
      }
      .check {
        display: flex;
        gap: 0.35rem;
        align-items: center;
        color: var(--text-secondary);
      }
      .create-row button,
      .lobby-detail button {
        background: var(--accent);
        color: var(--accent-text);
        border: none;
        border-radius: var(--radius-sm);
        padding: 0.5rem 0.9rem;
        font-weight: 600;
      }
      .create-row button:hover,
      .lobby-detail button:hover {
        background: var(--accent-hover);
      }
      .error {
        color: var(--accent-danger);
      }
      .empty {
        color: var(--text-secondary);
      }
      .lobby-list {
        list-style: none;
        margin: 0;
        padding: 0;
        display: flex;
        flex-direction: column;
        gap: 0.5rem;
      }
      .lobby-row {
        background: var(--surface-1);
        border: 1px solid var(--surface-border);
        border-radius: var(--radius-md);
        padding: 0.5rem;
      }
      .lobby-select {
        background: none;
        border: none;
        color: var(--text-primary);
        font-weight: 600;
        width: 100%;
        text-align: left;
        padding: 0.5rem;
      }
      .lobby-detail {
        border-top: 1px solid var(--surface-border);
        margin-top: 0.5rem;
        padding: 0.75rem 0.5rem 0.25rem;
        display: flex;
        flex-direction: column;
        gap: 0.6rem;
      }
      .lobby-detail h2 {
        font-size: 0.95rem;
        margin: 0;
        color: var(--text-secondary);
      }
      .lobby-detail ul {
        list-style: none;
        margin: 0;
        padding: 0;
      }
      .deck-ok {
        color: var(--accent-ok);
        font-size: 0.85rem;
      }
      .ready {
        color: var(--accent);
        font-size: 0.85rem;
      }
      .lobby-detail label {
        display: flex;
        flex-direction: column;
        gap: 0.35rem;
        color: var(--text-secondary);
      }
      .visually-hidden {
        position: absolute;
        width: 1px;
        height: 1px;
        overflow: hidden;
        clip: rect(0 0 0 0);
      }
    `,
  ],
})
export class LobbyPage {
  protected readonly hub = inject(GameHubService);
  private readonly api = inject(GamesApi);
  private readonly decksApi = inject(DecksApi);
  private readonly session = inject(SessionStore);
  private readonly router = inject(Router);
  private readonly sound = inject(SoundService);
  private readonly offFns: Array<() => void> = [];

  protected readonly lobbies = signal<readonly LobbyDto[]>([]);
  protected readonly screen = signal<LobbyScreen>({ kind: 'loading' });
  protected readonly selectedId = signal<string | null>(null);
  protected readonly selected = signal<LobbyDto | null>(null);
  protected readonly myDecks = signal<readonly DeckDto[]>([]);
  protected readonly hubError = signal<string | null>(null);

  protected readonly createForm = new FormGroup({
    isPublic: new FormControl(true, { nonNullable: true }),
    password: new FormControl('', { nonNullable: true }),
  });

  protected readonly joinPassword = new FormControl('', { nonNullable: true });

  constructor() {
    const destroyRef = inject(DestroyRef);
    destroyRef.onDestroy(() => {
      for (const off of this.offFns.splice(0)) {
        off();
      }
    });
    void this.boot();
  }

  protected screenError(): string {
    const screen = this.screen();
    return screen.kind === 'error' ? screen.message : '';
  }

  protected isMember(): boolean {
    const me = this.session.session()?.userId;
    return me !== undefined && (this.selected()?.players.some((p) => p.userId === me) ?? false);
  }

  protected myPlayer() {
    const me = this.session.session()?.userId;
    return this.selected()?.players.find((p) => p.userId === me) ?? null;
  }

  protected async create(): Promise<void> {
    const { isPublic, password } = this.createForm.getRawValue();
    try {
      const lobby = await firstValueFrom(
        this.api.create({
          isPublic,
          acceptsSpectators: true,
          password: password.length > 0 ? password : undefined,
        }),
      );
      await this.refresh();
      this.selectedId.set(lobby.id);
      this.selected.set(lobby);
      await this.hub.invoke('JoinLobby', lobby.id, null);
    } catch (error) {
      this.fail(error);
    }
  }

  protected select(id: string): void {
    this.selectedId.set(id);
    this.hubError.set(null);
    const found = this.lobbies().find((game) => game.id === id) ?? null;
    this.selected.set(found);
  }

  protected async join(): Promise<void> {
    const game = this.selected();
    if (game === null) {
      return;
    }
    try {
      await this.hub.invoke('JoinLobby', game.id, this.joinPassword.value.length > 0 ? this.joinPassword.value : null);
      this.sound.playClick();
    } catch (error) {
      this.fail(error);
    }
  }

  protected async selectDeck(deckId: string): Promise<void> {
    const game = this.selected();
    if (game === null || deckId.length === 0) {
      return;
    }
    try {
      await this.hub.invoke('SelectDeck', game.id, deckId);
    } catch (error) {
      this.fail(error);
    }
  }

  protected async toggleReady(): Promise<void> {
    const game = this.selected();
    const me = this.myPlayer();
    if (game === null || me === null) {
      return;
    }
    try {
      await this.hub.invoke('SetReady', game.id, !me.isReady);
    } catch (error) {
      this.fail(error);
    }
  }

  protected async start(): Promise<void> {
    const game = this.selected();
    if (game === null) {
      return;
    }
    try {
      await this.hub.invoke('StartGame', game.id);
    } catch (error) {
      this.fail(error);
    }
  }

  protected async leave(): Promise<void> {
    const game = this.selected();
    if (game === null) {
      return;
    }
    try {
      await this.hub.invoke('LeaveLobby', game.id);
      this.selectedId.set(null);
      this.selected.set(null);
      await this.refresh();
    } catch (error) {
      this.fail(error);
    }
  }

  private async boot(): Promise<void> {
    try {
      await this.hub.ensureConnected();
    } catch {
      this.hubError.set('Sin conexión en vivo. Reintentando…');
    }
    this.offFns.push(
      this.hub.on<LobbyDto>('lobby', (lobby) => this.onLobby(lobby)),
      this.hub.on<GameStartedEvent>('game-started', (event) => {
        void this.router.navigate(['/games', event.gameId]);
      }),
      this.hub.on<HubError>('error', (error) => {
        this.hubError.set(error.message);
        this.sound.playError();
      }),
    );
    this.decksApi.list().subscribe((decks) => this.myDecks.set(decks));
    await this.refresh();
  }

  private onLobby(lobby: LobbyDto): void {
    this.lobbies.update((games) => {
      const known = games.some((game) => game.id === lobby.id);
      const next = known
        ? games.map((game) => (game.id === lobby.id ? lobby : game))
        : [...games, lobby];
      return next.filter((game) => game.players.length > 0);
    });
    if (this.selectedId() === lobby.id) {
      this.selected.set(lobby.players.length > 0 ? lobby : null);
      if (lobby.players.length === 0) {
        this.selectedId.set(null);
      }
    }
  }

  private async refresh(): Promise<void> {
    this.screen.set({ kind: 'loading' });
    try {
      const lobbies = await firstValueFrom(this.api.listOpen());
      this.lobbies.set(lobbies);
      this.screen.set({ kind: 'ready' });
    } catch (error) {
      const message = error instanceof Error ? error.message : 'Ocurrió un error inesperado.';
      this.screen.set({ kind: 'error', message });
      this.sound.playError();
    }
  }

  private fail(error: unknown): void {
    const message = error instanceof Error ? error.message : 'Ocurrió un error inesperado.';
    this.hubError.set(message);
    this.sound.playError();
  }
}
