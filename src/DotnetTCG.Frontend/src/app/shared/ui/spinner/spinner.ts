import { ChangeDetectionStrategy, Component, input } from '@angular/core';

type SpinnerSize = 'xs' | 'sm' | 'md';

/** Indicador de carga. Tamaño por token; accesible con role="status". */
@Component({
  selector: 'ui-spinner',
  host: {
    role: 'status',
    '[attr.aria-label]': 'label()',
    '[class.ui-spinner--xs]': 'size() === "xs"',
    '[class.ui-spinner--sm]': 'size() === "sm"',
    '[class.ui-spinner--md]': 'size() === "md"',
  },
  template: `<span class="ui-spinner-track" aria-hidden="true"></span>`,
  styles: `
    :host { display: inline-flex; align-items: center; justify-content: center; }
    :host(.ui-spinner--xs) { --spin-size: 0.85rem; --spin-border: 2px; }
    :host(.ui-spinner--sm) { --spin-size: 1.1rem; --spin-border: 2px; }
    :host(.ui-spinner--md) { --spin-size: 1.75rem; --spin-border: 3px; }

    .ui-spinner-track {
      width: var(--spin-size);
      height: var(--spin-size);
      border-radius: var(--radius-full);
      border: var(--spin-border) solid rgba(148, 163, 184, 0.3);
      border-top-color: var(--action);
      animation: ui-spin 0.7s linear infinite;
    }
    @keyframes ui-spin { to { transform: rotate(360deg); } }

    @media (prefers-reduced-motion: reduce) {
      .ui-spinner-track { animation-duration: 1.4s; }
    }
  `,
  changeDetection: ChangeDetectionStrategy.OnPush,
})
export class SpinnerComponent {
  readonly size = input<SpinnerSize>('md');
  readonly label = input('Cargando');
}
