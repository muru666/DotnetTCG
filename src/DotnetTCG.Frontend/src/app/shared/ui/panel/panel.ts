import { ChangeDetectionStrategy, Component, input } from '@angular/core';

/**
 * Superficie de tarjeta/panel: superficie + borde + radio + sombra.
 * Reemplaza el patrón repetido .auth-card / .db-* / modales ad-hoc.
 */
@Component({
  selector: 'ui-panel',
  host: {
    '[class.ui-panel--pad-sm]': 'padding() === "sm"',
    '[class.ui-panel--pad-md]': 'padding() === "md"',
    '[class.ui-panel--pad-lg]': 'padding() === "lg"',
  },
  template: `<ng-content />`,
  styles: `
    :host {
      display: block;
      background: var(--surface-raised);
      border: 1px solid var(--surface-border);
      border-radius: var(--radius-lg);
      box-shadow: var(--shadow-lg);
    }
    :host(.ui-panel--pad-sm) { padding: var(--space-3); }
    :host(.ui-panel--pad-md) { padding: var(--space-5); }
    :host(.ui-panel--pad-lg) { padding: var(--space-8); }
  `,
  changeDetection: ChangeDetectionStrategy.OnPush,
})
export class PanelComponent {
  readonly padding = input<'sm' | 'md' | 'lg'>('md');
}
