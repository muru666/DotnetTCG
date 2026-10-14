import { ChangeDetectionStrategy, Component, input, output } from '@angular/core';

import { PokemonInPlay } from '../../../../shared/models/dtos/game-state.model';

@Component({
  selector: 'app-card-chip',
  imports: [],
  template: `
    <div class="chip" [class.knocked]="hpRemaining() <= 0"
      (dblclick)="preview.emit(pokemon().pokemonCard.imageUrl); $event.stopPropagation()">
      <img [src]="pokemon().pokemonCard.imageUrl" [alt]="pokemon().pokemonCard.name" class="chip-img" />
      <div class="chip-footer">
        <span class="chip-name">{{ pokemon().pokemonCard.name }}</span>
        @if (pokemon().damageCounters > 0) {
          <span class="chip-dmg">✕{{ pokemon().damageCounters * 10 }}</span>
        }
        @if (pokemon().attachedTool) {
          <span class="chip-tool" [title]="pokemon().attachedTool!.name">🔧</span>
        }
      </div>
    </div>
  `,
  styles: [`
    .chip {
      width: 42px;
      border-radius: 4px;
      overflow: hidden;
      background: #111622;
      border: 1.5px solid rgba(255, 255, 255, 0.08);
      cursor: pointer;
      transition: transform 0.15s, border-color 0.15s;
      flex-shrink: 0;
    }
    .chip:hover {
      transform: translateY(-2px);
      border-color: rgba(245, 158, 11, 0.3);
    }
    .chip.knocked {
      opacity: 0.35;
      filter: grayscale(1);
    }
    .chip-img {
      width: 100%;
      height: auto;
      display: block;
      object-fit: contain;
    }
    .chip-footer {
      background: rgba(0,0,0,0.7);
      padding: 1px 3px;
      display: flex;
      align-items: center;
      justify-content: space-between;
      gap: 2px;
    }
    .chip-name {
      font-size: 0.45rem;
      font-weight: 700;
      color: #cbd5e1;
      overflow: hidden;
      text-overflow: ellipsis;
      white-space: nowrap;
    }
    .chip-dmg {
      font-size: 0.45rem;
      font-weight: 700;
      color: #f87171;
      white-space: nowrap;
    }
    .chip-tool {
      font-size: 0.45rem;
      cursor: help;
    }
  `],
  changeDetection: ChangeDetectionStrategy.OnPush
})
export class CardChipComponent {
  readonly pokemon = input.required<PokemonInPlay>();
  readonly preview = output<string>();

  protected maxHp(): number {
    return this.pokemon().pokemonCard.hp;
  }

  protected hpRemaining(): number {
    return Math.max(0, this.maxHp() - this.pokemon().damageCounters * 10);
  }
}
