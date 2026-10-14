import { ChangeDetectionStrategy, Component, input, output } from '@angular/core';
import { BoardDto } from '../../../../shared/models/dtos/game-state.model';
import { ActiveCardComponent } from '../active-card/active-card';
import { CardChipComponent } from '../card-chip/card-chip';

@Component({
  selector: 'app-spectator-player-zone',
  imports: [ActiveCardComponent, CardChipComponent],
  template: `
    <div class="spec-zone" [class.active-turn]="isActiveTurn()">
      <div class="spec-zone-header">
        <span class="spec-zone-name">{{ playerName() }}</span>
        @if (isActiveTurn()) {
          <span class="spec-zone-turn-badge">Su turno</span>
        }
      </div>

      <div class="spec-active-area">
        @if (board()?.activePokemon; as active) {
          <app-active-card [pokemon]="active" (preview)="preview.emit($event)" />
        } @else {
          <div class="spec-active-empty">Sin Pokémon Activo</div>
        }
      </div>

      <div class="spec-bench-area">
        @for (idx of [0, 1, 2, 3, 4]; track idx) {
          @if (board()?.bench?.[idx]; as poke) {
            <app-card-chip [pokemon]="poke" (preview)="preview.emit($event)" />
          } @else {
            <div class="spec-bench-empty"></div>
          }
        }
      </div>
    </div>
  `,
  styles: [`
    .spec-zone {
      display: flex;
      flex-direction: column;
      align-items: center;
      gap: 14px;
      height: 100%;
      justify-content: center;
      padding: 16px;
      border-radius: 14px;
      transition: background 0.3s, box-shadow 0.3s;
    }
    .spec-zone.active-turn {
      background: rgba(245, 158, 11, 0.05);
      box-shadow: inset 0 0 0 1px rgba(245, 158, 11, 0.2);
    }
    .spec-zone-header {
      display: flex;
      align-items: center;
      gap: 8px;
    }
    .spec-zone-name {
      font-family: 'Bungee', sans-serif;
      font-size: 0.85rem;
      color: #fff;
      text-shadow: 0 1px 3px rgba(0, 0, 0, 0.5);
      letter-spacing: 0.5px;
    }
    .spec-zone-turn-badge {
      font-family: 'Nunito', sans-serif;
      font-size: 0.6rem;
      font-weight: 800;
      text-transform: uppercase;
      letter-spacing: 0.5px;
      color: #fbbf24;
      background: rgba(245, 158, 11, 0.15);
      border: 1px solid rgba(245, 158, 11, 0.3);
      border-radius: 10px;
      padding: 2px 8px;
    }
    .spec-active-area {
      display: flex;
      justify-content: center;
    }
    .spec-active-empty {
      width: 140px;
      aspect-ratio: 0.7;
      border-radius: 10px;
      border: 2px dashed rgba(255, 255, 255, 0.1);
      display: flex;
      align-items: center;
      justify-content: center;
      color: rgba(255, 255, 255, 0.2);
      font-size: 12px;
      font-weight: 600;
      text-align: center;
      padding: 8px;
    }
    .spec-bench-area {
      display: flex;
      gap: 6px;
      flex-wrap: wrap;
      justify-content: center;
      max-width: 260px;
    }
    .spec-bench-empty {
      width: 42px;
      aspect-ratio: 0.7;
      border-radius: 4px;
      border: 1.5px dashed rgba(255, 255, 255, 0.08);
      background: rgba(255, 255, 255, 0.02);
      flex-shrink: 0;
    }
  `],
  changeDetection: ChangeDetectionStrategy.OnPush
})
export class SpectatorPlayerZoneComponent {
  readonly board = input<BoardDto | null>(null);
  readonly playerName = input('');
  readonly isActiveTurn = input(false);
  readonly preview = output<string>();
}
