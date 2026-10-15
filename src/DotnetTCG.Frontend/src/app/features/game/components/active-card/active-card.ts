import { ChangeDetectionStrategy, Component, computed, input, output } from '@angular/core';
import { NgOptimizedImage } from '@angular/common';

import { PokemonInPlay } from '../../../../shared/models/dtos/game-state.model';

@Component({
  selector: 'app-active-card',
  imports: [NgOptimizedImage],
  template: `
    <div class="active-card" [class.opponent-active]="isOpponent()" [class.knocked-out]="hpRemaining() <= 0"
      (dblclick)="preview.emit(pokemon().pokemonCard.imageUrl); $event.stopPropagation()">
      <div class="ac-image-frame" [class.rotated-sideways]="isSidewaysCondition()">
        <img [ngSrc]="pokemon().pokemonCard.imageUrl" [alt]="pokemon().pokemonCard.name" class="ac-image"
          [class.rotated-ccw]="hasCondition('ASLEEP')"
          [class.rotated-cw]="hasCondition('PARALYZED')"
          [class.rotated-180]="hasCondition('CONFUSED')" priority width="250" height="350" />
      </div>

      <div class="ac-info">
        <div class="ac-info-row">
          <span class="ac-name">{{ pokemon().pokemonCard.name }}</span>
          <span class="ac-hp">♥ {{ hpRemaining() }}/{{ maxHp() }}</span>
        </div>
        <div class="ac-info-row">
          @if (pokemon().attachedEnergies.length > 0) {
            <span class="ac-energies">
              @for (e of displayEnergies(); track $index) {
                <span class="ac-energy" [title]="e.energyType">{{ energyIcon(e.energyType) }}</span>
              }
            </span>
          }
          @if (pokemon().damageCounters > 0) {
            <span class="ac-dmg">✕{{ pokemon().damageCounters * 10 }}</span>
          }
          @if (pokemon().attachedTool) {
            <span class="ac-tool" [title]="pokemon().attachedTool!.name">🔧</span>
          }
          @if (pokemon().specialConditions.length) {
            <span class="ac-conditions">
              @for (sc of pokemon().specialConditions; track $index) {
                <span class="ac-condition"
                  [class.cond-burned]="sc === 'BURNED'"
                  [class.cond-poisoned]="sc === 'POISONED'"
                  [class.cond-asleep]="sc === 'ASLEEP'"
                  [class.cond-confused]="sc === 'CONFUSED'"
                  [class.cond-paralyzed]="sc === 'PARALYZED'"
                  [title]="statusLabel(sc)">{{ statusIcon(sc) }}</span>
              }
            </span>
          }
        </div>
      </div>
    </div>
  `,
  styles: [`
    .active-card {
      width: 140px;
      border-radius: 10px;
      overflow: hidden;
      background: #0f1525;
      border: 2px solid rgba(245, 158, 11, 0.4);
      transition: border-color 0.2s;
      box-shadow: 0 4px 16px rgba(0,0,0,0.3);
    }
    .active-card.opponent-active {
      border-color: rgba(255, 255, 255, 0.1);
    }
    .active-card.knocked-out {
      opacity: 0.35;
      filter: grayscale(1);
    }
    .ac-image-frame {
      width: 100%;
      position: relative;
      overflow: hidden;
      display: block;
      aspect-ratio: 2.5 / 3.5;
    }
    .ac-image-frame.rotated-sideways {
      aspect-ratio: 7 / 5;
    }
    .ac-image {
      width: 100%;
      height: auto;
      display: block;
      object-fit: contain;
      transition: transform 0.3s ease;
    }
    .ac-image-frame.rotated-sideways .ac-image {
      position: absolute;
      top: 50%;
      left: 50%;
      width: 100%;
      height: auto;
    }
    .ac-image-frame.rotated-sideways .ac-image.rotated-ccw { transform: translate(-50%, -50%) rotate(-90deg) scale(0.714); }
    .ac-image-frame.rotated-sideways .ac-image.rotated-cw  { transform: translate(-50%, -50%) rotate(90deg) scale(0.714); }
    .ac-image.rotated-180 { transform: rotate(180deg); }
    .ac-info {
      background: rgba(0, 0, 0, 0.75);
      padding: 6px 8px;
      display: flex;
      flex-direction: column;
      gap: 3px;
      backdrop-filter: blur(4px);
    }
    .ac-info-row {
      display: flex;
      align-items: center;
      justify-content: space-between;
      gap: 6px;
    }
    .ac-name {
      font-family: 'Nunito', sans-serif;
      font-size: 0.7rem;
      font-weight: 800;
      color: #f1f5f9;
      overflow: hidden;
      text-overflow: ellipsis;
      white-space: nowrap;
    }
    .ac-hp {
      font-family: 'Bungee', sans-serif;
      font-size: 0.7rem;
      color: #ef4444;
      white-space: nowrap;
    }
    .ac-energies {
      display: flex;
      gap: 2px;
    }
    .ac-energy {
      font-size: 0.65rem;
    }
    .ac-dmg {
      font-family: 'Nunito', sans-serif;
      font-size: 0.65rem;
      font-weight: 700;
      color: #f87171;
    }
    .ac-tool {
      font-size: 0.7rem;
      cursor: help;
    }
    .ac-conditions {
      display: flex;
      gap: 3px;
    }
    .ac-condition {
      font-size: 0.6rem;
      padding: 1px 4px;
      border-radius: 3px;
      font-weight: 700;
    }
    .cond-burned    { background: rgba(249, 115, 22, 0.3); color: #fdba74; border: 1px solid rgba(249, 115, 22, 0.4); }
    .cond-poisoned  { background: rgba(168, 85, 247, 0.3); color: #d8b4fe; border: 1px solid rgba(168, 85, 247, 0.4); }
    .cond-asleep    { background: rgba(59, 130, 246, 0.3); color: #93c5fd; border: 1px solid rgba(59, 130, 246, 0.4); }
    .cond-confused  { background: rgba(234, 179, 8, 0.3);  color: #fde047; border: 1px solid rgba(234, 179, 8, 0.4); }
    .cond-paralyzed { background: rgba(245, 158, 11, 0.3); color: #fcd34d; border: 1px solid rgba(245, 158, 11, 0.4); }
    @media (max-width: 1100px) {
      .active-card { width: 112px; }
      .ac-name { font-size: 0.62rem; }
      .ac-hp { font-size: 0.62rem; }
    }
    @media (max-width: 760px) {
      .active-card { width: 92px; }
      .ac-info { padding: 4px 5px; }
    }
  `],
  changeDetection: ChangeDetectionStrategy.OnPush
})
export class ActiveCardComponent {
  readonly pokemon = input.required<PokemonInPlay>();
  readonly isOpponent = input(false);
  readonly preview = output<string>();

  readonly displayEnergies = computed(() => {
    return this.pokemon().attachedEnergies.flatMap(e =>
      e.specialEnergyType === 'DOUBLE_COLORLESS' ? [e, e] : [e]
    );
  });

  protected maxHp(): number {
    return this.pokemon().pokemonCard.hp;
  }

  protected hpRemaining(): number {
    return Math.max(0, this.maxHp() - this.pokemon().damageCounters * 10);
  }

  protected energyIcon(type: string): string {
    const icons: Record<string, string> = {
      'GRASS': '🌿', 'FIRE': '🔥', 'WATER': '💧', 'LIGHTNING': '⚡',
      'PSYCHIC': '🔮', 'FIGHTING': '💪', 'DARKNESS': '🌑', 'METAL': '🔩',
      'FAIRY': '✨', 'DRAGON': '🐉', 'COLORLESS': '⚪'
    };
    return icons[type?.toUpperCase()] ?? '⚪';
  }

  protected readonly isSidewaysCondition = computed(() => {
    const conditions = this.pokemon().specialConditions;
    return conditions.includes('ASLEEP') || conditions.includes('PARALYZED');
  });

  protected hasCondition(condition: string): boolean {
    return this.pokemon().specialConditions.includes(condition);
  }

  private readonly statusIcons: Record<string, string> = {
    'BURNED': '🔥',
    'POISONED': '☠️',
    'ASLEEP': '💤',
    'CONFUSED': '❓',
    'PARALYZED': '⚡'
  };

  private readonly statusLabels: Record<string, string> = {
    'BURNED': 'Quemado',
    'POISONED': 'Envenenado',
    'ASLEEP': 'Dormido',
    'CONFUSED': 'Confundido',
    'PARALYZED': 'Paralizado'
  };

  protected statusIcon(condition: string): string {
    return this.statusIcons[condition] ?? condition;
  }

  protected statusLabel(condition: string): string {
    return this.statusLabels[condition] ?? condition;
  }
}
