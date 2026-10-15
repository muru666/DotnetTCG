import { ChangeDetectionStrategy, Component, input, output, computed, signal } from '@angular/core';

import { CdkDropListGroup, CdkDropList, CdkDragDrop } from '@angular/cdk/drag-drop';
import { GameState, Card, PokemonInPlay } from '../../../../shared/models/dtos/game-state.model';
import { CardComponent } from '../card/card';
import { CardSlotComponent } from '../card-slot/card-slot';

@Component({
  selector: 'app-setup-phase',
  imports: [CardComponent, CardSlotComponent, CdkDropListGroup, CdkDropList],
  template: `
    <div class="setup-overlay">
      @if (!isMyTurn()) {
        <div class="setup-waiting-overlay">
          <div class="setup-waiting-pulse"></div>
          <h2 class="setup-waiting-title">Esperando al oponente...</h2>
          <p class="setup-waiting-text">El otro jugador está colocando sus Pokémon</p>
        </div>
      }
      @if (showMulligan()) {
        <div class="setup-mulligan-overlay" (click)="dismissMulligan()">
          <div class="setup-mulligan-card">
            <span class="setup-mulligan-icon">♻️</span>
            <h2 class="setup-mulligan-title">¡MULLIGAN!</h2>
            <p class="setup-mulligan-text">
              No tenías Pokémon Básicos en tu mano inicial.<br/>
              Se te repartieron <strong>{{ mulliganCount() }} mano(s)</strong> nueva(s).
            </p>
            <p class="setup-mulligan-text">
              Tu oponente robó <strong>{{ mulliganCount() }} carta(s)</strong> adicional(es).
            </p>
            <button class="setup-mulligan-btn" (click)="dismissMulligan()">
              ENTENDIDO
            </button>
          </div>
        </div>
      }
      <div class="setup-card">
        <h2 class="setup-title">FASE DE PREPARACIÓN</h2>
        <p class="setup-subtitle">Coloca tu Pokémon Activo y hasta 5 en Banca</p>

        <div class="setup-content" cdkDropListGroup>
          <div class="setup-hand-label">Tu Mano ({{ handCards().length }} cartas)</div>
          <div class="setup-hand" cdkDropList
            [cdkDropListData]="'hand-setup'"
            (cdkDropListDropped)="onHandDrop($event)"
          >
            @for (card of handCards(); track $index) {
              <app-card
                [card]="card"
                [draggable]="isMyTurn() && isBasicCard(card)"
                [class.card-disabled]="!isMyTurn() || !isBasicCard(card)"
                [class.clicked-card]="selectedCardId() === card.id"
                (clicked)="onCardClick(card)"
              />
            }
          </div>

          <div class="setup-field">
            <div class="setup-active-zone">
              <div class="setup-zone-label">ACTIVO</div>
              @if (activePlaced()) {
                <div class="setup-active-placed">✓ Pokémon Activo colocado</div>
              } @else {
                <app-card-slot
                  label="ARRASTRA AQUÍ"
                  [isEmpty]="true"
                  [highlight]="isMyTurn()"
                  slotId="active-setup"
                  (cardDropped)="isMyTurn() ? onSlotDrop($event) : null"
                />
              }
            </div>

            <div class="setup-bench-zone">
              <div class="setup-zone-label">BANCA ({{ benchCount() }}/5)</div>
              <div class="setup-bench-slots">
                @for (idx of [0,1,2,3,4]; track idx) {
                  @if (benchCards()[idx]; as placed) {
                    <div class="setup-bench-placed">
                      <img [src]="placed.pokemonCard.imageUrl" [alt]="placed.pokemonCard.name" class="setup-bench-img" />
                      <span class="setup-bench-name">{{ placed.pokemonCard.name }}</span>
                    </div>
                  } @else {
                    <app-card-slot
                      label="BANCA"
                      [isEmpty]="true"
                      [slotId]="'bench-setup-'+idx"
                      (cardDropped)="isMyTurn() ? onSlotDrop($event) : null"
                    />
                  }
                }
              </div>
            </div>
          </div>

          <button
            class="setup-confirm-btn"
            [disabled]="!activePlaced() || !isMyTurn()"
            (click)="onConfirm()"
          >
            @if (!isMyTurn()) {
              Esperando al oponente...
            } @else if (!activePlaced()) {
              Primero coloca un Pokémon Activo
            } @else {
              CONFIRMAR PREPARACIÓN
            }
          </button>
        </div>
      </div>
    </div>
  `,
  styles: [`
    .setup-overlay {
      position: fixed;
      inset: 0;
      background: rgba(0, 0, 0, 0.85);
      display: flex;
      align-items: center;
      justify-content: center;
      z-index: 100;
      backdrop-filter: blur(4px);
    }
    .setup-card {
      position: relative;
      background: linear-gradient(180deg, #1a1a2e 0%, #16213e 100%);
      border: 2px solid rgba(245, 158, 11, 0.3);
      border-radius: 16px;
      padding: 28px 32px;
      max-width: 700px;
      width: 90%;
      max-height: 90vh;
      overflow-y: auto;
      box-shadow: 0 16px 48px rgba(0,0,0,0.5);
    }
    .setup-title {
      font-family: 'Bungee', sans-serif;
      font-size: 1.3rem;
      color: #f59e0b;
      text-align: center;
      margin: 0 0 4px;
      letter-spacing: 1px;
    }
    .setup-subtitle {
      font-family: 'Nunito', sans-serif;
      font-size: 0.85rem;
      color: rgba(255,255,255,0.5);
      text-align: center;
      margin: 0 0 20px;
    }
    .setup-content {
      display: flex;
      flex-direction: column;
      gap: 16px;
    }
    .setup-hand-label {
      font-family: 'Nunito', sans-serif;
      font-size: 0.75rem;
      font-weight: 700;
      color: rgba(255,255,255,0.4);
      text-transform: uppercase;
      letter-spacing: 1px;
    }
    .setup-hand {
      display: flex;
      gap: 8px;
      flex-wrap: wrap;
      justify-content: center;
      padding: 12px;
      background: rgba(0,0,0,0.3);
      border-radius: 12px;
      min-height: 120px;
      border: 1px solid rgba(255,255,255,0.05);
    }
    .clicked-card {
      transform: translateY(-8px);
    }
    .setup-field {
      display: flex;
      gap: 24px;
      align-items: flex-start;
      justify-content: center;
    }
    .setup-active-zone {
      display: flex;
      flex-direction: column;
      align-items: center;
      gap: 8px;
    }
    .setup-zone-label {
      font-family: 'Nunito', sans-serif;
      font-size: 0.7rem;
      font-weight: 800;
      color: rgba(255,255,255,0.3);
      text-transform: uppercase;
      letter-spacing: 1.5px;
    }
    .setup-active-placed {
      width: 100px;
      height: 140px;
      border-radius: 8px;
      background: rgba(34, 197, 94, 0.15);
      border: 2px solid rgba(34, 197, 94, 0.4);
      display: flex;
      align-items: center;
      justify-content: center;
      font-family: 'Nunito', sans-serif;
      font-size: 0.75rem;
      font-weight: 700;
      color: #22c55e;
      text-align: center;
      padding: 8px;
    }
    .setup-bench-zone {
      display: flex;
      flex-direction: column;
      align-items: center;
      gap: 8px;
    }
    .setup-bench-slots {
      display: flex;
      gap: 8px;
    }
    .setup-bench-placed {
      width: 56px;
      height: 76px;
      border-radius: 5px;
      overflow: hidden;
      background: rgba(34, 197, 94, 0.1);
      border: 1.5px solid rgba(34, 197, 94, 0.3);
      position: relative;
    }
    .setup-bench-img {
      width: 100%;
      height: 100%;
      object-fit: cover;
    }
    .setup-bench-name {
      position: absolute;
      bottom: 0;
      left: 0;
      right: 0;
      background: rgba(0,0,0,0.7);
      color: #fff;
      font-size: 6px;
      text-align: center;
      padding: 1px 2px;
      white-space: nowrap;
      overflow: hidden;
      text-overflow: ellipsis;
      font-weight: 600;
    }
    .setup-confirm-btn {
      width: 100%;
      padding: 14px;
      border: none;
      border-radius: 10px;
      font-family: 'Bungee', sans-serif;
      font-size: 0.9rem;
      letter-spacing: 1px;
      cursor: pointer;
      transition: all 0.2s;
      margin-top: 8px;
    }
    .setup-confirm-btn:not(:disabled) {
      background: linear-gradient(130deg, #22c55e, #16a34a);
      color: #fff;
      box-shadow: 0 4px 12px rgba(34, 197, 94, 0.3);
    }
    .setup-confirm-btn:not(:disabled):hover {
      transform: translateY(-2px);
      box-shadow: 0 6px 20px rgba(34, 197, 94, 0.4);
    }
    .setup-confirm-btn:disabled {
      background: rgba(255,255,255,0.08);
      color: rgba(255,255,255,0.2);
      cursor: default;
    }
    .card-disabled {
      opacity: 0.35;
      filter: grayscale(0.6);
      pointer-events: none;
    }
    .setup-mulligan-overlay {
      position: absolute;
      inset: 0;
      z-index: 10;
      display: flex;
      align-items: center;
      justify-content: center;
      background: rgba(0, 0, 0, 0.7);
      backdrop-filter: blur(4px);
      border-radius: 16px;
    }
    .setup-mulligan-card {
      background: linear-gradient(180deg, #1e1b4b 0%, #312e81 100%);
      border: 2px solid rgba(250, 204, 21, 0.4);
      border-radius: 16px;
      padding: 32px;
      text-align: center;
      max-width: 360px;
      box-shadow: 0 16px 48px rgba(0,0,0,0.5);
    }
    .setup-mulligan-icon {
      font-size: 3rem;
      display: block;
      margin-bottom: 12px;
    }
    .setup-mulligan-title {
      font-family: 'Bungee', sans-serif;
      font-size: 1.5rem;
      color: #facc15;
      margin: 0 0 12px;
      letter-spacing: 2px;
    }
    .setup-mulligan-text {
      font-family: 'Nunito', sans-serif;
      font-size: 0.85rem;
      color: rgba(255,255,255,0.8);
      margin: 0 0 8px;
      line-height: 1.5;
    }
    .setup-mulligan-btn {
      margin-top: 16px;
      padding: 10px 28px;
      border: none;
      border-radius: 8px;
      background: linear-gradient(130deg, #facc15, #eab308);
      color: #1e1b4b;
      font-family: 'Bungee', sans-serif;
      font-size: 0.8rem;
      letter-spacing: 1px;
      cursor: pointer;
      transition: transform 0.2s;
    }
    .setup-mulligan-btn:hover {
      transform: translateY(-2px);
    }
    .setup-waiting-overlay {
      position: absolute;
      inset: 0;
      z-index: 15;
      display: flex;
      flex-direction: column;
      align-items: center;
      justify-content: center;
      background: rgba(0, 0, 0, 0.6);
      backdrop-filter: blur(3px);
      border-radius: 16px;
      pointer-events: none;
    }
    .setup-waiting-pulse {
      width: 48px;
      height: 48px;
      border-radius: 50%;
      background: rgba(245, 158, 11, 0.2);
      border: 3px solid rgba(245, 158, 11, 0.5);
      margin-bottom: 16px;
      animation: waitingPulse 1.5s ease-in-out infinite;
    }
    @keyframes waitingPulse {
      0%, 100% { transform: scale(1); opacity: 0.6; }
      50% { transform: scale(1.15); opacity: 1; }
    }
    .setup-waiting-title {
      font-family: 'Bungee', sans-serif;
      font-size: 1rem;
      color: #f59e0b;
      margin: 0 0 8px;
      letter-spacing: 1px;
    }
    .setup-waiting-text {
      font-family: 'Nunito', sans-serif;
      font-size: 0.8rem;
      color: rgba(255, 255, 255, 0.5);
      margin: 0;
    }
  `],
  changeDetection: ChangeDetectionStrategy.OnPush
})
export class SetupPhaseComponent {
  readonly gameState = input.required<GameState>();
  readonly playerIndex = input(0);

  readonly cardDropped = output<{ cardId: string; targetSlot: string }>();
  readonly confirm = output<void>();

  protected readonly selectedCardId = signal<string | null>(null);
  private readonly mulliganDismissedAt = signal(-1);

  protected readonly handCards = computed(() => {
    const hand = this.gameState()?.myBoard?.hand;
    return Array.isArray(hand) ? hand : [];
  });

  protected readonly benchCards = computed(() => {
    return this.gameState()?.myBoard?.bench ?? [];
  });

  protected readonly benchCount = computed(() => this.benchCards().length);

  protected readonly activePlaced = computed(() => {
    return !!this.gameState()?.myBoard?.activePokemon;
  });

  protected readonly mulliganCount = computed(() => {
    const counts = this.gameState()?.mulliganCounts;
    return counts ? counts[this.playerIndex()] ?? 0 : 0;
  });

  protected readonly showMulligan = computed(() => {
    const count = this.mulliganCount();
    return count > 0 && this.mulliganDismissedAt() < count;
  });

  protected readonly isMyTurn = computed(() => {
    return this.gameState()?.currentSetupPlayerIndex === this.playerIndex();
  });

  protected isBasicCard(card: Card): boolean {
    return card.supertype === 'POKEMON' && card.subtypes?.includes('BASIC') === true;
  }

  protected dismissMulligan(): void {
    this.mulliganDismissedAt.set(this.mulliganCount());
  }

  protected onCardClick(card: Card): void {
    if (!this.isMyTurn() || !this.isBasicCard(card)) return;
    this.selectedCardId.set(card.id);
    if (!this.activePlaced()) {
      this.cardDropped.emit({ cardId: card.id, targetSlot: 'active-setup' });
    } else if (this.benchCount() < 5) {
      this.cardDropped.emit({ cardId: card.id, targetSlot: 'bench-setup-' + this.benchCount() });
    }
  }

  protected onSlotDrop(event: { slotId: string; cardId: string }): void {
    this.selectedCardId.set(event.cardId);
    this.cardDropped.emit({ cardId: event.cardId, targetSlot: event.slotId });
  }

  protected onHandDrop(event: CdkDragDrop<string>): void {
    // Intencionalmente vacio: las cartas no vuelven a la mano por arrastre.
    void event;
  }

  protected onConfirm(): void {
    if (!this.isMyTurn()) return;
    this.confirm.emit();
  }
}
