import { ChangeDetectionStrategy, Component, input } from '@angular/core';

type CalloutVariant = 'error' | 'success' | 'info';

/**
 * Mensaje contextual (error, éxito, info). Reemplaza .auth-message y
 * banners de estado sueltos. No comunica solo por color: lleva un ícono
 * de texto para accesibilidad.
 */
@Component({
  selector: 'ui-callout',
  host: {
    role: 'alert',
    '[class]': "'ui-callout ui-callout--' + variant()",
    '[attr.aria-label]': 'label()',
  },
  template: `
    <span class="ui-callout-icon" aria-hidden="true">{{ icon() }}</span>
    <span class="ui-callout-body"><ng-content /></span>
  `,
  styles: `
    :host {
      display: flex;
      align-items: flex-start;
      gap: var(--space-2);
      padding: var(--space-3);
      border-radius: var(--radius-md);
      border: 1px solid;
      font-size: var(--text-sm);
      line-height: 1.45;
    }
    .ui-callout-icon { flex-shrink: 0; font-weight: 800; line-height: 1.4; }
    .ui-callout-body { flex: 1; }

    :host(.ui-callout--error) {
      background: var(--danger-soft);
      border-color: rgba(239, 68, 68, 0.45);
      color: #fecaca;
    }
    :host(.ui-callout--success) {
      background: var(--success-soft);
      border-color: rgba(34, 197, 94, 0.45);
      color: #bbf7d0;
    }
    :host(.ui-callout--info) {
      background: rgba(59, 130, 246, 0.12);
      border-color: rgba(59, 130, 246, 0.4);
      color: #bfdbfe;
    }
  `,
  changeDetection: ChangeDetectionStrategy.OnPush,
})
export class CalloutComponent {
  readonly variant = input<CalloutVariant>('info');
  readonly label = input('Mensaje');

  protected icon(): string {
    return this.variant() === 'error' ? '✕' : this.variant() === 'success' ? '✓' : 'ℹ';
  }
}
