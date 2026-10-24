import { ChangeDetectionStrategy, Component, input, output } from '@angular/core';

/**
 * Diálogo modal accesible: overlay, Escape cierra, foco al diálogo,
 * proyecta cuerpo y acciones. El padre controla `open`.
 */
@Component({
  selector: 'ui-modal',
  host: {
    '[attr.aria-hidden]': '!open()',
    '(keydown.escape)': 'open() && close.emit()',
  },
  template: `
    @if (open()) {
      <div class="ui-modal-overlay" (click)="close.emit()">
        <div
          class="ui-modal-dialog"
          role="dialog"
          aria-modal="true"
          [attr.aria-label]="ariaLabel()"
          (click)="$event.stopPropagation()"
          tabindex="-1"
        >
          @if (title()) {
            <h2 class="ui-modal-title">{{ title() }}</h2>
          }
          <div class="ui-modal-body"><ng-content select="[body]" /></div>
          <div class="ui-modal-actions"><ng-content select="[actions]" /></div>
        </div>
      </div>
    }
  `,
  styles: `
    :host { display: contents; }

    .ui-modal-overlay {
      position: fixed;
      inset: 0;
      z-index: 1000;
      background: rgba(2, 6, 23, 0.7);
      backdrop-filter: blur(3px);
      display: grid;
      place-items: center;
      padding: var(--space-4);
      animation: ui-modal-fade 160ms ease-out;
    }
    .ui-modal-dialog {
      width: min(32rem, 100%);
      background: var(--surface-raised);
      border: 1px solid var(--surface-border);
      border-radius: var(--radius-lg);
      box-shadow: var(--shadow-lg);
      padding: var(--space-6);
      animation: ui-modal-in 180ms ease-out;
    }
    .ui-modal-dialog:focus { outline: none; }
    .ui-modal-title {
      margin: 0 0 var(--space-4);
      font-family: var(--font-display);
      font-size: var(--text-lg);
      letter-spacing: 0.03em;
    }
    .ui-modal-body { color: var(--text-muted); }
    .ui-modal-actions {
      display: flex;
      justify-content: flex-end;
      gap: var(--space-2);
      margin-top: var(--space-6);
    }

    @keyframes ui-modal-fade { from { opacity: 0; } to { opacity: 1; } }
    @keyframes ui-modal-in { from { opacity: 0; transform: translateY(8px) scale(0.98); } to { opacity: 1; transform: none; } }
  `,
  changeDetection: ChangeDetectionStrategy.OnPush,
})
export class ModalComponent {
  readonly open = input(false);
  readonly title = input('');
  readonly ariaLabel = input('');
  readonly close = output<void>();
}
