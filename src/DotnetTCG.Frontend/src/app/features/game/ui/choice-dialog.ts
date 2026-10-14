import { Component, input, output, signal } from '@angular/core';
import { PendingChoiceDto } from '../models/game.models';

@Component({
  selector: 'app-choice-dialog',
  template: `
    @if (choice(); as pc) {
      <div class="overlay" role="dialog" aria-modal="true" aria-label="Elegí una opción">
        <div class="dialog">
          <p class="prompt">{{ pc.prompt }}</p>
          <ul class="options">
            @for (option of pc.options; track option.optionId) {
              <li>
                <button
                  type="button"
                  [class.selected]="selected().includes(option.optionId)"
                  (click)="toggle(option.optionId)"
                >
                  {{ option.label }}
                </button>
              </li>
            }
          </ul>
          <button
            type="button"
            class="primary"
            [disabled]="selected().length === 0"
            (click)="confirm(pc.choiceId)"
          >
            Confirmar
          </button>
        </div>
      </div>
    }
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
        z-index: 60;
        padding: 1rem;
      }
      .dialog {
        background: var(--surface-1);
        border: 1px solid var(--surface-border);
        border-radius: var(--radius-lg);
        padding: 1.5rem;
        max-width: 26rem;
        width: 100%;
      }
      .prompt {
        font-weight: 600;
        margin: 0 0 1rem;
      }
      .options {
        list-style: none;
        margin: 0 0 1rem;
        padding: 0;
        display: flex;
        flex-direction: column;
        gap: 0.4rem;
        max-height: 40dvh;
        overflow-y: auto;
      }
      .options button {
        width: 100%;
        text-align: left;
        background: var(--surface-2);
        color: var(--text-primary);
        border: 1px solid var(--surface-border);
        border-radius: var(--radius-sm);
        padding: 0.55rem 0.75rem;
      }
      .options button:hover {
        border-color: var(--accent);
      }
      .options button.selected {
        border-color: var(--accent);
        background: var(--surface-3);
      }
      .primary {
        width: 100%;
        background: var(--accent);
        color: var(--accent-text);
        border: none;
        border-radius: var(--radius-sm);
        padding: 0.6rem;
        font-weight: 600;
      }
      .primary:hover:not(:disabled) {
        background: var(--accent-hover);
      }
    `,
  ],
})
export class ChoiceDialogComponent {
  readonly choice = input<PendingChoiceDto | null>(null);
  readonly resolve = output<{ choiceId: string; selectedOptions: string[] }>();

  protected readonly selected = signal<readonly string[]>([]);

  protected toggle(optionId: string): void {
    this.selected.update((current) =>
      current.includes(optionId)
        ? current.filter((id) => id !== optionId)
        : [...current, optionId],
    );
  }

  protected confirm(choiceId: string): void {
    const selected = this.selected();
    if (selected.length === 0) {
      return;
    }
    this.selected.set([]);
    this.resolve.emit({ choiceId, selectedOptions: [...selected] });
  }
}
