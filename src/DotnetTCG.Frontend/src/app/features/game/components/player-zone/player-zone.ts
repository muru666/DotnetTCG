import { ChangeDetectionStrategy, Component, computed, input, output, signal } from '@angular/core';
import { CdkDropList, CdkDragDrop } from '@angular/cdk/drag-drop';
import { BoardDto, PokemonInPlay } from '../../../../shared/models/dtos/game-state.model';
import { CardChipComponent } from '../card-chip/card-chip';
import { ActiveCardComponent } from '../active-card/active-card';

// Habilidades con efecto activable en el motor (ver AbilityRegistry en el backend).
// Todo lo demas (Fur Coat, Sweet Veil, Forest's Curse, Destiny Burst, Spiky Shield)
// es pasivo o disparado: no tiene boton porque el motor lo rechaza.
const ACTIVATABLE_ABILITIES = new Set([
  'Mystical Fire', 'Water Shuriken', 'Upside-Down Evolution',
  'Stance Change', 'Fairy Transfer', 'Drive Off',
]);

@Component({
  selector: 'app-player-zone',
  imports: [CardChipComponent, ActiveCardComponent, CdkDropList],
  template: `
    <div class="player-zone" [class.opponent-zone]="isOpponent()" [class.active-turn]="isActiveTurn()">
      <div class="zone-body">
        <div class="zone-side left-side">
          <div class="pile deck-pile">
            <div class="pile-top"></div>
            <div class="pile-mid"></div>
            <div class="pile-bot">
              <div class="pile-count">{{ board()?.deck ?? 0 }}</div>
            </div>
            <div class="pile-label">MAZO</div>
          </div>
        </div>

        <div class="zone-center">
          @if (board(); as b) {
            <!-- Active Pokemon (ARRIBA/AL FRENTE) -->
            <div class="active-area">
              @if (b.activePokemon; as active) {
                <div
                  cdkDropList
                  [cdkDropListData]="'active-'+playerName()"
                  (cdkDropListDropped)="onDrop($event)"
                  (click)="onActiveClick()"
                  [class.evolvable]="evolvableSlots().includes(-1)"
                >
                  <app-active-card
                    [pokemon]="active"
                    [isOpponent]="isOpponent()"
                    (preview)="preview.emit($event)"
                  />
                </div>
              } @else {
                @if (isOpponent()) {
                  <div class="active-empty">Sin Pokémon Activo</div>
                } @else {
                  <div
                    class="active-empty drop-target"
                    [class.has-selected]="selectedCardIndex() !== null"
                    cdkDropList
                    [cdkDropListData]="'active-'+playerName()"
                    (cdkDropListDropped)="onDrop($event)"
                    (click)="onActiveClick()"
                  >
                    <span class="drop-hint-active">Suelta aquí tu Activo</span>
                  </div>
                }
              }
            </div>

            <!-- Bench (ABAJO/DETRÁS) -->
            <div class="bench-area" [class.inverted]="isOpponent()">
              <div class="bench-rect">
                <div class="bench-cards">
                  @for (idx of [0,1,2,3,4]; track idx) {
                    @if (b.bench[idx]; as poke) {
                      <div
                        cdkDropList
                        [cdkDropListData]="'bench-'+playerName()+'-'+idx"
                        (cdkDropListDropped)="onDrop($event)"
                        (click)="onBenchChipClick(idx)"
                        [class.evolvable]="evolvableSlots().includes(idx)"
                        [class.retreat-selectable]="retreatMode()"
                      >
                        @if (retreatMode() && !isOpponent()) {
                          <div class="retreat-overlay" (click)="$event.stopPropagation(); onBenchChipRetreat(idx)"></div>
                        }
                        <app-card-chip [pokemon]="poke" (preview)="preview.emit($event)" />
                      </div>
                      @if (!isOpponent() && hasActivatableAbilities(poke)) {
                        <div class="bench-abilities">
                          @for (ab of activatableAbilities(poke.pokemonCard.abilities); track $index) {
                            <button class="bench-ability-btn" [disabled]="!canUseAbility()"
                          (click)="canUseAbility() && abilityAction.emit({ abilityName: ab.name, pokemonId: poke.pokemonCard.id })">
                              {{ ab.name }}
                            </button>
                          }
                        </div>
                      }
                    } @else {
                      @if (isOpponent()) {
                        <div class="bench-slot-empty"></div>
                      } @else {
                        <div
                          class="bench-slot-empty drop-target"
                          [class.has-selected]="selectedCardIndex() !== null"
                          cdkDropList
                          [cdkDropListData]="'bench-'+playerName()+'-'+idx"
                          (cdkDropListDropped)="onDrop($event)"
                          (click)="onEmptyBenchClick(idx)"
                        >
                          <span class="drop-hint">+</span>
                        </div>
                      }
                    }
                  }
                </div>
              </div>
            </div>
          } @else {
            <div class="zone-empty">Esperando datos...</div>
          }
        </div>

        <div class="zone-side right-side">
          <div class="pile prize-pile">
            <div class="pile-top"></div>
            <div class="pile-mid"></div>
            <div class="pile-bot">
              <div class="pile-count">{{ board()?.prizeCards ?? 0 }}</div>
            </div>
            <div class="pile-label">PREMIO</div>
          </div>
          <div class="pile discard-pile">
            <div class="pile-top"></div>
            <div class="pile-mid"></div>
            <div class="pile-bot">
              <div class="pile-count">{{ board()?.discardPile ?? 0 }}</div>
            </div>
            <div class="pile-label">DESCARTE</div>
          </div>
        </div>

      </div>

      @if (!isOpponent() && board()?.activePokemon; as active) {
        <div class="zone-actions">
          <div class="actions-section">
            <div class="actions-title">ATACAR</div>
            @for (atk of active.pokemonCard.attacks; track $index) {
              <button class="action-btn attack-btn" [class.btn-disabled]="!canAttack()" [disabled]="!canAttack()"
                (click)="canAttack() && attackAction.emit(atk.name)">
                <span class="atk-name">{{ atk.name }}</span>
                <span class="atk-dmg">{{ atk.damage }}</span>
              </button>
            }
          </div>
          @if (hasActivatableAbilities(active)) {
            <div class="actions-section">
              <div class="actions-title">HABILIDAD</div>
              @for (ab of activatableAbilities(active.pokemonCard.abilities); track $index) {
                <button class="action-btn ability-btn" [class.btn-disabled]="!canUseAbility()" [disabled]="!canUseAbility()"
                (click)="canUseAbility() && abilityAction.emit({ abilityName: ab.name, pokemonId: active.pokemonCard.id })">
                  {{ ab.name }}
                </button>
              }
            </div>
          }
          @if (active.pokemonCard.retreatCost != null) {
            <button class="action-btn retreat-btn" [class.active-mode]="retreatMode()" [class.btn-disabled]="!canRetreat()" [disabled]="!canRetreat()"
              (click)="onRetreatClick()">
              ⟲ RETIRARSE ({{ active.pokemonCard.retreatCost }})
            </button>
          }
          @if (retreatMode()) {
            <div class="retreat-hint">Elegí Pokémon de banca</div>
            <button class="action-btn retreat-cancel-btn" (click)="onCancelRetreat()">✕ Cancelar</button>
          }
        </div>
      }
    </div>
  `,
  styles: [`
    .player-zone { width: 100%; height: 100%; display: flex; flex-direction: row; align-items: center; }
    .opponent-zone { flex-direction: row-reverse; }
    .zone-body { display: flex; align-items: center; gap: 10px; flex: 1; height: 100%; padding: 4px 8px; min-width: 0; overflow: hidden; }
    .zone-side { display: flex; flex-direction: column; align-items: center; gap: 6px; flex-shrink: 0; width: 60px; }
    .left-side { align-items: flex-start; }
    .right-side { align-items: flex-end; }
    .opponent-zone .left-side { align-items: flex-end; }
    .opponent-zone .right-side { align-items: flex-start; }
    .zone-center { flex: 1; display: flex; flex-direction: column; align-items: center; justify-content: center; gap: 6px; min-width: 0; overflow: hidden; }
    .opponent-zone .zone-center { flex-direction: column-reverse; }

    .active-area { display: flex; justify-content: center; flex-shrink: 0; z-index: 2; position: relative; }
    .active-empty { width: 140px; border-radius: 10px; border: 2px dashed rgba(255, 255, 255, 0.1); display: flex; align-items: center; justify-content: center; color: rgba(255, 255, 255, 0.2); font-size: 12px; font-weight: 600; aspect-ratio: 0.7; }
    .active-empty.drop-target { border-color: rgba(245, 158, 11, 0.3); background: rgba(245, 158, 11, 0.05); cursor: default; }
    .active-empty.drop-target:hover { border-color: rgba(245, 158, 11, 0.6); background: rgba(245, 158, 11, 0.1); }
    .active-empty.has-selected { border-color: rgba(245, 158, 11, 0.5); background: rgba(245, 158, 11, 0.15); cursor: pointer; }
    .drop-hint-active { font-family: 'Nunito', sans-serif; font-size: 11px; color: rgba(245, 158, 11, 0.35); text-align: center; padding: 8px; }

    .bench-area { flex-shrink: 0; z-index: 1; }
    .bench-rect { background: rgba(0, 0, 0, 0.25); border: 1.5px solid rgba(0, 0, 0, 0.2); border-radius: 8px; padding: 4px 8px; }
    .bench-cards { display: flex; gap: 3px; justify-content: center; max-width: 100%; overflow-x: auto; }
    .bench-slot-empty { width: 42px; aspect-ratio: 0.7; border-radius: 4px; border: 1.5px dashed rgba(255, 255, 255, 0.08); background: rgba(255, 255, 255, 0.02); flex-shrink: 0; cursor: default; }
    .bench-chip { cursor: pointer; }
    .evolvable { outline: 2px solid rgba(34, 197, 94, 0.6); outline-offset: 2px; border-radius: 8px; cursor: pointer; animation: pulse-evolve 1.5s ease-in-out infinite; }
    @keyframes pulse-evolve { 0%, 100% { outline-color: rgba(34, 197, 94, 0.6); } 50% { outline-color: rgba(34, 197, 94, 0.2); } }
    .bench-abilities { display: flex; gap: 2px; margin-top: 2px; justify-content: center; flex-wrap: wrap; }
    .bench-ability-btn { border: none; border-radius: 3px; padding: 1px 5px; font-family: 'Nunito', sans-serif; font-size: 0.45rem; font-weight: 700; cursor: pointer; background: rgba(99,102,241,0.15); color: #a5b4fc; border: 1px solid rgba(99,102,241,0.2); transition: all 0.15s; white-space: nowrap; }
    .bench-ability-btn:hover { background: rgba(99,102,241,0.3); }
    .bench-slot-empty.drop-target { border-color: rgba(245, 158, 11, 0.25); background: rgba(245, 158, 11, 0.05); }
    .bench-slot-empty.drop-target:hover { border-color: rgba(245, 158, 11, 0.5); background: rgba(245, 158, 11, 0.1); cursor: default; }
    .bench-slot-empty.has-selected { border-color: rgba(245, 158, 11, 0.5); background: rgba(245, 158, 11, 0.15); cursor: pointer; }
    .drop-hint { font-size: 16px; color: rgba(245, 158, 11, 0.25); font-weight: 300; display: flex; align-items: center; justify-content: center; height: 100%; }

    .zone-empty { color: rgba(255, 255, 255, 0.2); font-size: 13px; font-weight: 600; }

    .pile { display: flex; flex-direction: column; align-items: center; gap: 0; position: relative; padding-top: 6px; }
    .pile-top, .pile-mid, .pile-bot { width: 44px; aspect-ratio: 0.7; border-radius: 4px; position: absolute; }
    .pile-top { top: 0; left: -4px; background: linear-gradient(135deg, #152840, #0c1a30); border: 1px solid rgba(30, 74, 122, 0.2); transform: rotate(-2deg); }
    .pile-mid { top: 2px; left: -2px; background: linear-gradient(135deg, #1a3050, #102040); border: 1px solid rgba(30, 74, 122, 0.3); transform: rotate(1deg); }
    .pile-bot { position: relative; top: 4px; left: 0; background: linear-gradient(135deg, #1e3a5f, #142850); border: 1px solid rgba(30, 74, 122, 0.4); display: flex; align-items: center; justify-content: center; box-shadow: 0 2px 6px rgba(0,0,0,0.2); }
    .pile-count { font-family: 'Bungee', sans-serif; font-size: 16px; color: #f59e0b; text-shadow: 0 1px 2px rgba(0, 0, 0, 0.3); }
    .pile-label { font-size: 7px; font-weight: 800; color: rgba(255, 255, 255, 0.35); text-transform: uppercase; letter-spacing: 1px; margin-top: 4px; white-space: nowrap; position: relative; top: 2px; }
    .discard-pile .pile-bot { background: linear-gradient(135deg, #3a2020, #2a1515); border-color: rgba(122, 30, 30, 0.4); }
    .discard-pile .pile-top { background: linear-gradient(135deg, #301818, #201010); border-color: rgba(122, 30, 30, 0.25); }
    .discard-pile .pile-mid { background: linear-gradient(135deg, #3a2020, #2a1515); border-color: rgba(122, 30, 30, 0.3); }

    .zone-actions { display: flex; flex-direction: column; gap: 4px; flex-shrink: 0; width: 120px; max-height: 100%; overflow-y: auto; padding: 4px 0; }
    .actions-section { display: flex; flex-direction: column; gap: 2px; }
    .actions-title { font-family: 'Nunito', sans-serif; font-size: 0.5rem; font-weight: 700; color: rgba(255,255,255,0.25); text-transform: uppercase; letter-spacing: 1px; padding: 0 4px; }
    .action-btn { border: none; border-radius: 5px; padding: 5px 8px; font-family: 'Nunito', sans-serif; font-size: 0.6rem; font-weight: 700; cursor: pointer; text-align: center; transition: all 0.15s; }
    .attack-btn { background: rgba(239, 68, 68, 0.15); color: #fca5a5; border: 1px solid rgba(239, 68, 68, 0.2); display: flex; justify-content: space-between; align-items: center; }
    .attack-btn:hover { background: rgba(239, 68, 68, 0.25); }
    .atk-name { font-size: 0.6rem; white-space: nowrap; overflow: hidden; text-overflow: ellipsis; }
    .atk-dmg { font-family: 'Bungee', sans-serif; color: #ef4444; font-size: 0.7rem; flex-shrink: 0; margin-left: 4px; }
    .ability-btn { background: rgba(99, 102, 241, 0.15); color: #a5b4fc; border: 1px solid rgba(99, 102, 241, 0.2); }
    .ability-btn:hover { background: rgba(99, 102, 241, 0.25); }
    .retreat-btn { background: rgba(59, 130, 246, 0.15); color: #60a5fa; border: 1px solid rgba(59, 130, 246, 0.2); }
    .retreat-btn:hover { background: rgba(59, 130, 246, 0.25); }
    .retreat-btn.active-mode { background: rgba(245, 158, 11, 0.2); color: #fbbf24; border-color: rgba(245, 158, 11, 0.4); }
    .retreat-hint { font-family: 'Nunito', sans-serif; font-size: 0.5rem; font-weight: 700; color: rgba(245, 158, 11, 0.9); text-align: center; text-transform: uppercase; letter-spacing: 0.5px; padding: 2px 4px; }
    .retreat-cancel-btn { background: rgba(239, 68, 68, 0.1); color: #fca5a5; border: 1px solid rgba(239, 68, 68, 0.2); }
    .retreat-cancel-btn:hover { background: rgba(239, 68, 68, 0.2); }
    .retreat-selectable { outline: 2px solid rgba(245, 158, 11, 0.7); outline-offset: 2px; border-radius: 8px; cursor: pointer; animation: pulse-retreat 1.2s ease-in-out infinite; position: relative; }
    @keyframes pulse-retreat { 0%, 100% { outline-color: rgba(245, 158, 11, 0.7); } 50% { outline-color: rgba(245, 158, 11, 0.25); } }
    .retreat-overlay { position: absolute; inset: 0; z-index: 10; cursor: pointer; border-radius: 4px; }
    .action-btn.btn-disabled { opacity: 0.35; cursor: not-allowed; filter: grayscale(0.5); }
    @media (max-width: 1100px) {
      .zone-body { gap: 6px; padding: 2px 4px; }
      .zone-side { width: 44px; }
      .zone-actions { width: 96px; }
      .action-btn { font-size: 0.55rem; padding: 4px 5px; }
      .pile-label { display: none; }
    }
    @media (max-width: 760px) {
      .zone-side { width: 34px; }
      .zone-actions { width: 84px; }
    }
    .action-btn.btn-disabled:hover { background: rgba(255, 255, 255, 0.05); }
    .bench-ability-btn:disabled { opacity: 0.35; cursor: not-allowed; }
    .bench-ability-btn:disabled:hover { background: rgba(99, 102, 241, 0.15); }
  `],
  changeDetection: ChangeDetectionStrategy.OnPush
})
export class PlayerZoneComponent {
  readonly board = input<BoardDto | null>(null);
  readonly playerName = input('');
  readonly isOpponent = input(false);
  readonly isActiveTurn = input(false);
  readonly isSpectator = input(false);
  readonly phase = input<string>('');
  readonly selectedCardIndex = input<number | null>(null);
  readonly evolvableSlots = input<number[]>([]);
  readonly cardDrop = output<{ cardId: string; targetSlot: string }>();
  readonly placeCard = output<{ targetIndex: number }>();
  readonly activeClick = output<void>();
  readonly preview = output<string>();
  readonly attackAction = output<string>();
  readonly abilityAction = output<{ abilityName: string; pokemonId: string }>();
  readonly retreatAction = output<number>();

  protected readonly retreatMode = signal(false);

  protected readonly canAttack = computed(() => {
    if (this.isOpponent() || this.isSpectator()) return false;
    if (!this.isActiveTurn()) return false;
    if (this.phase() !== 'MAIN') return false;
    const active = this.board()?.activePokemon;
    if (!active) return false;
    const sc = active.specialConditions;
    return !sc.includes('ASLEEP') && !sc.includes('PARALYZED');
  });

  protected readonly canUseAbility = computed(() => {
    if (this.isOpponent() || this.isSpectator()) return false;
    if (!this.isActiveTurn()) return false;
    if (this.phase() !== 'MAIN') return false;
    const active = this.board()?.activePokemon;
    if (!active) return false;
    const sc = active.specialConditions;
    return !sc.includes('ASLEEP') && !sc.includes('PARALYZED');
  });

  protected readonly canRetreat = computed(() => {
    if (this.isOpponent() || this.isSpectator()) return false;
    if (!this.isActiveTurn()) return false;
    if (this.phase() !== 'MAIN') return false;
    const active = this.board()?.activePokemon;
    if (!active) return false;
    const bench = this.board()?.bench;
    if (!bench || bench.length === 0) return false;
    const sc = active.specialConditions;
    return !sc.includes('ASLEEP') && !sc.includes('PARALYZED');
  });

  protected onDrop(event: CdkDragDrop<string>): void {
    if (event.previousContainer === event.container) return;
    this.cardDrop.emit({
      cardId: event.item.data,
      targetSlot: event.container.data
    });
  }

  protected onActiveClick(): void {
    if (this.selectedCardIndex() !== null && !this.isOpponent()) {
      this.activeClick.emit();
    }
  }

  protected onEmptyBenchClick(index: number): void {
    if (this.selectedCardIndex() !== null && !this.isOpponent()) {
      this.placeCard.emit({ targetIndex: index });
    }
  }

  protected onBenchChipClick(index: number): void {
    if (this.selectedCardIndex() !== null && !this.isOpponent()) {
      this.placeCard.emit({ targetIndex: index });
    }
  }

  protected onRetreatClick(): void {
    if (this.isOpponent() || !this.canRetreat()) return;
    const bench = this.board()?.bench ?? [];
    if (bench.length === 1) {
      this.retreatAction.emit(0);
    } else {
      this.retreatMode.set(true);
    }
  }

  protected onBenchChipRetreat(index: number): void {
    this.retreatMode.set(false);
    this.retreatAction.emit(index);
  }

  protected onCancelRetreat(): void {
    this.retreatMode.set(false);
  }

  protected hasActivatableAbilities(poke: PokemonInPlay): boolean {
    return poke.pokemonCard.abilities.some(a => ACTIVATABLE_ABILITIES.has(a.name));
  }

  protected activatableAbilities(abilities: { name: string }[]): { name: string }[] {
    return abilities.filter(a => ACTIVATABLE_ABILITIES.has(a.name));
  }
}
