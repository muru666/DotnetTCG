import { Component, input, output } from '@angular/core';
import { BoardViewDto, FieldPokemonDto } from '../models/game.models';
import { PokemonCardComponent } from './pokemon-card';

export interface BoardSlot {
  readonly zone: 'active' | 'bench';
  readonly index: number;
}

/**
 * Una mitad del campo: pilas laterales + activo + banca.
 * Presentacional: emite clicks, no llama a la API.
 */
@Component({
  selector: 'app-player-board',
  imports: [PokemonCardComponent],
  template: `
    <div class="board" [class.opponent]="isOpponent()" [class.turn]="isTurn()">
      <div class="side">
        <div class="pile" title="Mazo">
          <strong>{{ board().deckCount }}</strong><span>Mazo</span>
        </div>
        <div class="pile" title="Premios">
          <strong>{{ board().prizeCount }}</strong><span>Premios</span>
        </div>
        <div class="pile" title="Descarte ({{ board().discard.length }} cartas)">
          <strong>{{ board().discard.length }}</strong><span>Descarte</span>
        </div>
      </div>
      <div class="center">
        <app-pokemon-card
          [pokemon]="board().active"
          [imageUrl]="imageFor()(board().active)"
          [selected]="isSelected(null)"
          (clicked)="emitSlot('active', 0)"
          (preview)="preview.emit($event)"
        />
        <div class="bench">
          @for (poke of board().bench; track poke.cardId; let i = $index) {
            <app-pokemon-card
              [pokemon]="poke"
              [imageUrl]="imageFor()(poke)"
              [selected]="isSelected(i)"
              [dimmed]="dimBench()"
              (clicked)="emitSlot('bench', i)"
              (preview)="preview.emit($event)"
            />
          }
          @for (empty of emptySlots(); track $index) {
            <app-pokemon-card [pokemon]="null" />
          }
        </div>
      </div>
    </div>
  `,
  styles: [
    `
      .board {
        display: flex;
        gap: 0.75rem;
        align-items: center;
        background: var(--surface-1);
        border: 1px solid var(--surface-border);
        border-radius: var(--radius-lg);
        padding: 0.75rem;
      }
      .board.opponent {
        flex-direction: row-reverse;
      }
      .board.turn {
        border-color: var(--accent);
      }
      .side {
        display: flex;
        flex-direction: column;
        gap: 0.5rem;
      }
      .pile {
        display: flex;
        flex-direction: column;
        align-items: center;
        background: var(--surface-2);
        border: 1px solid var(--surface-border);
        border-radius: var(--radius-sm);
        padding: 0.4rem 0.6rem;
        min-width: 4.5rem;
      }
      .pile strong {
        font-size: 1.1rem;
      }
      .pile span {
        font-size: 0.7rem;
        color: var(--text-secondary);
      }
      .center {
        display: flex;
        gap: 0.75rem;
        align-items: flex-start;
        flex: 1;
        flex-wrap: wrap;
      }
      .board.opponent .center {
        flex-direction: row-reverse;
      }
      .bench {
        display: flex;
        gap: 0.4rem;
        flex-wrap: wrap;
      }
    `,
  ],
})
export class PlayerBoardComponent {
  readonly board = input.required<BoardViewDto>();
  readonly isOpponent = input(false);
  readonly isTurn = input(false);
  readonly dimBench = input(false);
  readonly selectedSlot = input<BoardSlot | null>(null);
  readonly imageFor = input<(pokemon: FieldPokemonDto | null) => string | null>(() => null);

  readonly slotClick = output<BoardSlot>();
  readonly preview = output<string>();

  protected emitSlot(zone: 'active' | 'bench', index: number): void {
    this.slotClick.emit({ zone, index });
  }

  protected emptySlots(): null[] {
    return Array.from({ length: Math.max(0, 5 - this.board().bench.length) });
  }

  protected isSelected(benchIndex: number | null): boolean {
    const selected = this.selectedSlot();
    if (selected === null) {
      return false;
    }
    if (benchIndex === null) {
      return selected.zone === 'active';
    }
    return selected.zone === 'bench' && selected.index === benchIndex;
  }
}
