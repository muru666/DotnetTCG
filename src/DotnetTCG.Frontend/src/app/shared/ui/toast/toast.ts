import { ChangeDetectionStrategy, Component, input, output } from '@angular/core';

type ToastTone = 'info' | 'success' | 'danger';

/**
 * Notificación flotante (presentacional). El contenedor/queue lo maneja la
 * pantalla que la use; aquí solo se renderiza un toast con su cierre.
 */
@Component({
  selector: 'ui-toast',
  host: { '[class]': "'ui-toast ui-toast--' + tone()" },
  template: `
    <span class="ui-toast-icon" aria-hidden="true">{{ icon() }}</span>
    <span class="ui-toast-body"><ng-content /></span>
    <button class="ui-toast-close" type="button" (click)="dismiss.emit()" aria-label="Cerrar">✕</button>
  `,
  styles: `
    :host {
      display: flex;
      align-items: center;
      gap: var(--space-2);
      min-width: 16rem;
      max-width: 22rem;
      padding: var(--space-3);
      border-radius: var(--radius-md);
      border: 1px solid;
      background: var(--surface-raised);
      box-shadow: var(--shadow-lg);
      font-size: var(--text-sm);
    }
    .ui-toast-icon { flex-shrink: 0; font-weight: 800; }
    .ui-toast-body { flex: 1; }
    .ui-toast-close {
      flex-shrink: 0;
      border: none;
      background: transparent;
      color: var(--text-faint);
      cursor: pointer;
      font-size: var(--text-sm);
      padding: var(--space-1);
    }
    .ui-toast-close:hover { color: var(--text-primary); }

    :host(.ui-toast--info) { border-color: rgba(59, 130, 246, 0.4); color: #bfdbfe; }
    :host(.ui-toast--success) { border-color: rgba(34, 197, 94, 0.4); color: #bbf7d0; }
    :host(.ui-toast--danger) { border-color: rgba(239, 68, 68, 0.45); color: #fecaca; }
  `,
  changeDetection: ChangeDetectionStrategy.OnPush,
})
export class ToastComponent {
  readonly tone = input<ToastTone>('info');
  readonly dismiss = output<void>();

  protected icon(): string {
    return this.tone() === 'success' ? '✓' : this.tone() === 'danger' ? '✕' : 'ℹ';
  }
}
