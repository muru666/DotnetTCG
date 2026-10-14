import { Component, input, output } from '@angular/core';
import { AttackDto } from '../../../shared/models/card.models';
import { PHASE_LABELS } from '../models/game.models';

@Component({
  selector: 'app-action-panel',
  template: `
    <div class="panel" aria-label="Acciones">
      <div class="turn-line">
        <strong>Turno {{ turnNumber() }}</strong>
        <span>{{ phaseLabel() }}</span>
        @if (!myTurn()) {
          <span class="wait">Turno rival…</span>
        }
      </div>
      @if (attacks().length > 0) {
        <h3>Ataques</h3>
        <ul>
          @for (atk of attacks(); track atk.name) {
            <li>
              <button type="button" [disabled]="!myTurn()" (click)="attack.emit(atk.name)">
                <span>{{ atk.name }}</span>
                <span class="dmg">{{ atk.damage }}</span>
              </button>
            </li>
          }
        </ul>
      }
      <div class="row">
        <button type="button" [disabled]="!myTurn()" (click)="retreat.emit()">Retirarse</button>
        <button type="button" [disabled]="!myTurn()" (click)="passTurn.emit()">Pasar turno</button>
        <button type="button" class="danger" [disabled]="!myTurn()" (click)="concede.emit()">
          Conceder
        </button>
      </div>
    </div>
  `,
  styles: [
    `
      .panel {
        background: var(--surface-1);
        border: 1px solid var(--surface-border);
        border-radius: var(--radius-md);
        padding: 0.75rem;
        display: flex;
        flex-direction: column;
        gap: 0.6rem;
      }
      .turn-line {
        display: flex;
        gap: 0.6rem;
        align-items: baseline;
        color: var(--text-secondary);
      }
      .wait {
        margin-left: auto;
        font-style: italic;
      }
      h3 {
        margin: 0;
        font-size: 0.85rem;
        color: var(--text-secondary);
      }
      ul {
        list-style: none;
        margin: 0;
        padding: 0;
        display: flex;
        flex-direction: column;
        gap: 0.35rem;
      }
      li button {
        width: 100%;
        display: flex;
        justify-content: space-between;
        background: var(--surface-2);
        color: var(--text-primary);
        border: 1px solid var(--surface-border);
        border-radius: var(--radius-sm);
        padding: 0.5rem 0.65rem;
      }
      li button:hover:not(:disabled) {
        border-color: var(--accent-danger);
      }
      .dmg {
        color: var(--accent-danger);
        font-weight: 700;
      }
      .row {
        display: flex;
        gap: 0.4rem;
        flex-wrap: wrap;
      }
      .row button {
        flex: 1;
        background: var(--surface-2);
        color: var(--text-primary);
        border: 1px solid var(--surface-border);
        border-radius: var(--radius-sm);
        padding: 0.5rem;
      }
      .row button:hover:not(:disabled) {
        border-color: var(--accent);
      }
      .row button.danger:hover:not(:disabled) {
        border-color: var(--accent-danger);
        color: var(--accent-danger);
      }
    `,
  ],
})
export class ActionPanelComponent {
  readonly attacks = input<readonly AttackDto[]>([]);
  readonly phase = input('');
  readonly turnNumber = input(0);
  readonly myTurn = input(false);

  readonly attack = output<string>();
  readonly retreat = output<void>();
  readonly passTurn = output<void>();
  readonly concede = output<void>();

  protected phaseLabel(): string {
    return PHASE_LABELS[this.phase()] ?? this.phase();
  }
}
