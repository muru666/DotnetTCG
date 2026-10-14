import { Component, computed, input, output } from '@angular/core';
import { NgOptimizedImage } from '@angular/common';
import { HandCardDto } from '../models/game.models';

@Component({
  selector: 'app-setup-overlay',
  imports: [NgOptimizedImage],
  template: `
    <div class="overlay" role="dialog" aria-modal="true" aria-label="Preparación">
      <div class="card">
        <h2>Preparación</h2>
        @if (!myTurn()) {
          <p role="status">Esperando al oponente…</p>
        } @else {
          <p class="hint">Colocá tu Activo y hasta 5 en Banca. Sin Básico en mano: pedí Mulligan.</p>
        }
        @if (mulliganNotice()) {
          <p class="notice" role="status">
            Sin Básicos en mano: pedí Mulligan (tu rival roba una carta extra).
          </p>
        }
        <h3>Tu mano ({{ hand().length }})</h3>
        <ul class="hand">
          @for (card of hand(); track card.id) {
            <li>
              <button
                type="button"
                class="hand-card"
                [disabled]="!myTurn() || !isBasic(card)"
                (click)="place.emit(card.id)"
                [title]="card.name"
              >
                <img [ngSrc]="card.imageUrl" [alt]="card.name" width="72" height="100" />
                <span>{{ card.name }}</span>
              </button>
            </li>
          } @empty {
            <li class="empty">Sin cartas.</li>
          }
        </ul>
        <div class="actions">
          <button type="button" (click)="mulligan.emit()" [disabled]="!myTurn() || hasBasic()">
            Mulligan
          </button>
          <button
            type="button"
            class="primary"
            (click)="confirm.emit()"
            [disabled]="!myTurn() || !hasActive()"
          >
            Confirmar
          </button>
        </div>
      </div>
    </div>
  `,
  styles: [
    `
      .overlay {
        position: fixed;
        inset: 0;
        background: rgba(0, 0, 0, 0.7);
        display: flex;
        align-items: center;
        justify-content: center;
        z-index: 50;
        padding: 1rem;
      }
      .card {
        background: var(--surface-1);
        border: 1px solid var(--surface-border);
        border-radius: var(--radius-lg);
        padding: 1.5rem;
        max-width: 40rem;
        width: 100%;
        max-height: 90dvh;
        overflow-y: auto;
      }
      h2 {
        margin: 0 0 0.5rem;
      }
      .hint,
      .notice {
        color: var(--text-secondary);
      }
      .notice {
        color: var(--accent-warn);
      }
      .hand {
        list-style: none;
        margin: 0.75rem 0;
        padding: 0;
        display: flex;
        gap: 0.5rem;
        flex-wrap: wrap;
      }
      .hand-card {
        background: var(--surface-2);
        border: 1px solid var(--surface-border);
        border-radius: var(--radius-sm);
        padding: 0.35rem;
        display: flex;
        flex-direction: column;
        gap: 0.25rem;
        align-items: center;
        width: 88px;
      }
      .hand-card:hover:not(:disabled) {
        border-color: var(--accent);
      }
      .hand-card span {
        font-size: 0.7rem;
        text-align: center;
      }
      .empty {
        color: var(--text-muted);
      }
      .actions {
        display: flex;
        gap: 0.75rem;
        margin-top: 1rem;
      }
      .actions button {
        background: var(--surface-3);
        color: var(--text-primary);
        border: 1px solid var(--surface-border);
        border-radius: var(--radius-sm);
        padding: 0.6rem 1rem;
        font-weight: 600;
      }
      .actions button.primary {
        background: var(--accent);
        border-color: var(--accent);
        color: var(--accent-text);
      }
      .actions button.primary:hover:not(:disabled) {
        background: var(--accent-hover);
      }
    `,
  ],
})
export class SetupOverlayComponent {
  readonly hand = input<readonly HandCardDto[]>([]);
  readonly myTurn = input(false);
  readonly hasActive = input(false);

  readonly place = output<string>();
  readonly mulligan = output<void>();
  readonly confirm = output<void>();

  protected readonly hasBasic = computed(() =>
    this.hand().some((card) => isBasicPokemon(card)),
  );

  protected readonly mulliganNotice = computed(
    () => this.myTurn() && !this.hasBasic(),
  );

  protected isBasic(card: HandCardDto): boolean {
    return isBasicPokemon(card);
  }
}

function isBasicPokemon(card: HandCardDto): boolean {
  return card.supertype === 'Pokemon' && card.subtypes.includes('Basic');
}
