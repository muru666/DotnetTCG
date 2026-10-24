import { ChangeDetectionStrategy, Component, input } from '@angular/core';
import { SpinnerComponent } from '../spinner/spinner';

type ButtonVariant = 'primary' | 'ghost' | 'danger';
type ButtonSize = 'sm' | 'md';

/**
 * Botón base del design system. Reemplaza los botones ad-hoc
 * (.auth-btn, .db-btn, .cs-page-btn, .action-btn…) de cada pantalla.
 * Renderiza un <button> nativo: soporta submit, foco y teclado.
 */
@Component({
  selector: 'ui-button',
  imports: [SpinnerComponent],
  template: `
    <button
      [type]="type()"
      [class]="'ui-btn ui-btn--' + variant() + ' ui-btn--' + size()"
      [disabled]="disabled() || loading()"
      [attr.aria-busy]="loading()"
    >
      @if (loading()) {
        <ui-spinner size="xs" />
      }
      <span class="ui-btn-label"><ng-content /></span>
    </button>
  `,
  styles: `
    :host { display: inline-flex; }

    .ui-btn {
      display: inline-flex;
      align-items: center;
      justify-content: center;
      gap: var(--space-2);
      border: 1px solid transparent;
      border-radius: var(--radius-md);
      font-family: var(--font-body);
      font-weight: 800;
      cursor: pointer;
      transition: transform 140ms ease, filter 140ms ease, background 140ms ease;
      white-space: nowrap;
    }
    .ui-btn--sm { padding: var(--space-2) var(--space-3); font-size: var(--text-sm); }
    .ui-btn--md { padding: var(--space-3) var(--space-4); font-size: var(--text-base); }

    .ui-btn--primary {
      background: linear-gradient(130deg, var(--action), var(--action-strong));
      color: var(--text-on-action);
    }
    .ui-btn--primary:hover:not(:disabled) { transform: translateY(-1px); filter: brightness(1.06); }

    .ui-btn--ghost {
      background: transparent;
      border-color: var(--surface-border);
      color: var(--text-primary);
    }
    .ui-btn--ghost:hover:not(:disabled) { background: rgba(148, 163, 184, 0.12); }

    .ui-btn--danger { background: var(--danger); color: #fff; }
    .ui-btn--danger:hover:not(:disabled) { filter: brightness(1.08); }

    .ui-btn:disabled { opacity: 0.55; cursor: not-allowed; transform: none; filter: none; }
    .ui-btn:focus-visible { outline: 2px solid var(--focus); outline-offset: 2px; }
  `,
  changeDetection: ChangeDetectionStrategy.OnPush,
})
export class ButtonComponent {
  readonly variant = input<ButtonVariant>('primary');
  readonly size = input<ButtonSize>('md');
  readonly type = input<'button' | 'submit'>('button');
  readonly loading = input(false);
  readonly disabled = input(false);
}
