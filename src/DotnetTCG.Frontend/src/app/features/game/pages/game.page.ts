import { Component, computed, DestroyRef, inject, resource, signal } from '@angular/core';
import { ActivatedRoute, RouterLink } from '@angular/router';
import { NgOptimizedImage } from '@angular/common';
import { GameHubService } from '../../../core/realtime/game-hub.service';
import { SoundService } from '../../../core/sound/sound';
import { GameSession, TargetRef } from '../data-access/game-session';
import { PlayerBoardComponent } from '../ui/player-board';
import { ActionPanelComponent } from '../ui/action-panel';
import { AbilityButtonsComponent } from '../ui/ability-buttons';
import { SetupOverlayComponent } from '../ui/setup-overlay';
import { ChoiceDialogComponent } from '../ui/choice-dialog';
import { SidePanelComponent } from '../ui/side-panel';
import { BoardSlot } from '../ui/player-board';

@Component({
  providers: [GameSession],
  imports: [
    RouterLink,
    NgOptimizedImage,
    PlayerBoardComponent,
    ActionPanelComponent,
    AbilityButtonsComponent,
    SetupOverlayComponent,
    ChoiceDialogComponent,
    SidePanelComponent,
  ],
  template: `
    @if (session.state(); as view) {
      <div class="game">
        <header class="game-head">
          <span>Turno {{ view.turnNumber }}</span>
          @if (view.suddenDeath) {
            <strong class="sudden">Muerte súbita: 1 premio</strong>
          }
          @if (hub.state() !== 'connected') {
            <span role="status">Reconectando…</span>
          }
        </header>

        @if (session.error(); as error) {
          <p class="error" role="alert">
            {{ error }} <button type="button" (click)="session.clearError()">×</button>
          </p>
        }

        @if (view.status === 'Setup') {
          <app-setup-overlay
            [hand]="myHand()"
            [myTurn]="setupTurn()"
            [hasActive]="myBoardActive() !== null"
            (place)="placeSetupCard($event)"
            (mulligan)="session.mulligan()"
            (confirm)="confirmSetup()"
          />
        }

        @if (rivalBoard(); as rival) {
          <app-player-board
            [board]="rival"
            [isOpponent]="true"
            [isTurn]="isRivalTurn()"
            [imageFor]="imageFor"
            (preview)="session.previewUrl.set($event)"
          />
        }

        @if (view.stadiumCardId !== null) {
          <p class="stadium">Estadio: {{ stadiumName() ?? view.stadiumCardId }}</p>
        }

        @if (session.myBoard(); as mine) {
          <app-player-board
            [board]="mine"
            [isOpponent]="false"
            [isTurn]="session.myTurn()"
            [imageFor]="imageFor"
            (slotClick)="onFieldClick($event)"
            (preview)="session.previewUrl.set($event)"
          />
          @for (poke of benchWithRefs(); track poke.ref.index) {
            <app-ability-buttons
              [pokemon]="poke.pokemon"
              [userRef]="poke.ref"
              [enabled]="session.myTurn()"
              (use)="session.useAbility($event.abilityName, $event.user)"
            />
          }
          @if (mine.active !== null) {
            <app-ability-buttons
              [pokemon]="mine.active"
              [userRef]="{ zone: 'active', index: 0 }"
              [enabled]="session.myTurn()"
              (use)="session.useAbility($event.abilityName, $event.user)"
            />
          }
        }

        @if (myHand(); as hand) {
          <div class="hand-row" role="list" aria-label="Tu mano">
            @for (card of hand; track card.id) {
              <button
                type="button"
                role="listitem"
                class="hand-card"
                [class.selected]="session.selection()?.handCardId === card.id"
                (click)="onHandClick(card.id)"
                (dblclick)="session.previewUrl.set(card.imageUrl)"
                [title]="card.name"
              >
                <img [ngSrc]="card.imageUrl" [alt]="card.name" width="72" height="100" />
              </button>
            } @empty {
              <p class="empty">Mano vacía.</p>
            }
          </div>
        }

        @if (session.mode().kind !== 'none') {
          <div class="mode-bar" role="status">
            {{ modeHint() }}
            <button type="button" (click)="session.cancelMode()">Cancelar</button>
          </div>
        }

        <app-action-panel
          [attacks]="activeAttacks()"
          [phase]="view.phase"
          [turnNumber]="view.turnNumber"
          [myTurn]="session.myTurn()"
          (attack)="session.attack($event)"
          (retreat)="session.startRetreat()"
          (passTurn)="session.passTurn()"
          (concede)="askConcede()"
        />
        @if (confirmingConcede()) {
          <p class="confirm-concede">
            ¿Conceder la partida?
            <button type="button" (click)="doConcede()">Sí, conceder</button>
            <button type="button" (click)="confirmingConcede.set(false)">Seguir</button>
          </p>
        }

        <app-side-panel
          [log]="session.log()"
          [chat]="session.chat()"
          [labelOf]="labelOf"
          (sendMessage)="session.sendChat($event)"
        />

        @if (view.pendingChoice !== null) {
          <app-choice-dialog
            [choice]="view.pendingChoice"
            (resolve)="session.resolveChoice($event.choiceId, $event.selectedOptions)"
          />
        }

        @if (session.paused()) {
          <div class="overlay" role="alertdialog" aria-label="Partida pausada">
            <div class="dialog">
              <h2>Partida pausada</h2>
              <p>Esperando reconexión…</p>
              <button type="button" (click)="session.reconnect()">Reconectar</button>
            </div>
          </div>
        }

        @if (session.finished()) {
          <div class="overlay" role="alertdialog" aria-label="Partida terminada">
            <div class="dialog">
              <h2>{{ session.didIWin() ? '¡Ganaste!' : 'Perdiste' }}</h2>
              <a routerLink="/lobby">Volver al lobby</a>
            </div>
          </div>
        }
      </div>
    } @else {
      <p role="status">Cargando partida…</p>
    }
    @if (session.previewUrl(); as url) {
      <div class="overlay" (click)="session.previewUrl.set(null)">
        <img [ngSrc]="url" alt="Vista previa" width="300" height="419" class="preview" />
      </div>
    }
  `,
  styles: [
    `
      .game {
        display: flex;
        flex-direction: column;
        gap: 0.75rem;
      }
      .game-head {
        display: flex;
        gap: 1rem;
        align-items: baseline;
        color: var(--text-secondary);
      }
      .sudden {
        color: var(--accent-warn);
      }
      .error {
        color: var(--accent-danger);
      }
      .error button {
        background: none;
        border: none;
        color: inherit;
        font-size: 1.1rem;
      }
      .stadium {
        color: var(--text-secondary);
        margin: 0;
      }
      .hand-row {
        display: flex;
        gap: 0.4rem;
        flex-wrap: wrap;
        background: var(--surface-1);
        border: 1px solid var(--surface-border);
        border-radius: var(--radius-md);
        padding: 0.6rem;
      }
      .hand-card {
        background: none;
        border: 2px solid transparent;
        border-radius: var(--radius-sm);
        padding: 0;
      }
      .hand-card:hover {
        border-color: var(--accent);
      }
      .hand-card.selected {
        border-color: var(--accent);
        transform: translateY(-4px);
      }
      .hand-card img {
        display: block;
        border-radius: 4px;
      }
      .empty {
        color: var(--text-muted);
      }
      .mode-bar {
        background: var(--surface-2);
        border: 1px solid var(--accent);
        border-radius: var(--radius-sm);
        padding: 0.5rem 0.75rem;
        display: flex;
        gap: 1rem;
        align-items: center;
      }
      .confirm-concede {
        color: var(--accent-danger);
      }
      .overlay {
        position: fixed;
        inset: 0;
        background: rgba(0, 0, 0, 0.7);
        display: flex;
        align-items: center;
        justify-content: center;
        z-index: 70;
        padding: 1rem;
      }
      .dialog {
        background: var(--surface-1);
        border: 1px solid var(--surface-border);
        border-radius: var(--radius-lg);
        padding: 1.5rem;
        text-align: center;
      }
      .preview {
        border-radius: var(--radius-md);
      }
    `,
  ],
})
export class GamePage {
  protected readonly session = inject(GameSession);
  protected readonly hub = inject(GameHubService);
  private readonly sound = inject(SoundService);

  protected readonly confirmingConcede = signal(false);

  protected readonly myHand = computed(() => this.session.myBoard()?.hand ?? []);
  protected readonly myBoardActive = computed(() => this.session.myBoard()?.active ?? null);

  protected modeHint(): string {
    switch (this.session.mode().kind) {
      case 'attach':
        return 'Elegí un Pokémon propio para unir la energía.';
      case 'evolve':
        return 'Elegí el Pokémon a evolucionar.';
      case 'tool':
        return 'Elegí el Pokémon para la herramienta.';
      case 'retreat':
        return 'Elegí el Pokémon de banca para el cambio.';
      case 'none':
        return '';
    }
  }
  protected readonly activeAttacks = computed(
    () => this.activeCard.value()?.attacks ?? [],
  );

  private readonly activeCard = resource({
    params: () => this.session.myBoard()?.active?.cardId ?? null,
    loader: ({ params }) =>
      params === null ? Promise.resolve(null) : this.session.card(params),
  });

  private readonly stadiumCard = resource({
    params: () => this.session.state()?.stadiumCardId ?? null,
    loader: ({ params }) =>
      params === null ? Promise.resolve(null) : this.session.card(params),
  });

  protected readonly stadiumName = computed(() => this.stadiumCard.value()?.name ?? null);

  protected readonly setupTurn = computed(() => {
    const view = this.session.state();
    return view !== null
      && view.status === 'Setup'
      && view.currentPlayerIndex === view.viewerPlayerIndex;
  });

  protected readonly isRivalTurn = computed(() => {
    const view = this.session.state();
    return view !== null
      && view.viewerPlayerIndex !== null
      && view.currentPlayerIndex !== view.viewerPlayerIndex;
  });

  protected readonly rivalBoard = computed(() => this.session.rivalBoard());

  protected readonly benchWithRefs = computed(() => {
    const bench = this.session.myBoard()?.bench ?? [];
    return bench.map((pokemon, index) => ({
      pokemon,
      ref: { zone: 'bench', index } as TargetRef,
    }));
  });

  protected readonly selectedSlot = computed(() => null);

  protected readonly imageFor = (pokemon: { cardId: string } | null): string | null => {
    if (pokemon === null) {
      return null;
    }
    const inHand = this.session
      .myBoard()
      ?.hand?.find((card) => card.id === pokemon.cardId)?.imageUrl;
    return inHand ?? this.session.imageUrlOf(pokemon.cardId);
  };

  protected readonly labelOf = (senderId: string): string => this.session.myLabel(senderId);

  constructor() {
    const gameId = inject(ActivatedRoute).snapshot.paramMap.get('id');
    if (gameId !== null) {
      void this.session.connect(gameId).catch(() => {
        this.sound.playError();
      });
    }
    inject(DestroyRef).onDestroy(() => this.session.disconnect());
  }

  protected async onHandClick(cardId: string): Promise<void> {
    this.sound.playClick();
    const current = this.session.selection();
    if (current?.handCardId === cardId) {
      this.session.selectHandCard(null);
      return;
    }
    this.session.selectHandCard(cardId);
    await this.session.playHandCard(cardId);
    if (this.session.mode().kind === 'none') {
      this.session.selectHandCard(null);
    }
  }

  protected async onFieldClick(slot: BoardSlot): Promise<void> {
    const mine = this.session.myBoard();
    if (mine === null) {
      return;
    }
    const target = slot.zone === 'active' ? mine.active : mine.bench[slot.index] ?? null;
    if (target === null) {
      return;
    }
    await this.session.fieldClick(slot.zone, slot.index);
  }

  protected async placeSetupCard(cardId: string): Promise<void> {
    await this.session.act({ type: 'PlayCard', cardId });
  }

  protected async confirmSetup(): Promise<void> {
    await this.session.act({ type: 'PassTurn' });
  }

  protected askConcede(): void {
    this.confirmingConcede.set(true);
  }

  protected async doConcede(): Promise<void> {
    this.confirmingConcede.set(false);
    await this.session.concede();
  }
}
