import { ChangeDetectionStrategy, Component, input } from '@angular/core';
import { RouterLink } from '@angular/router';

/**
 * Estructura de pantalla con encabezado: botón volver, título y subtítulo.
 * Unifica los headers ad-hoc de pokedex, deck-builder, lobby y auth.
 */
@Component({
  selector: 'ui-page-shell',
  imports: [RouterLink],
  template: `
    <header class="ui-shell-header">
      @if (backLink()) {
        <a class="ui-shell-back" [routerLink]="backLink()" [attr.aria-label]="backLabel()">&#8592;</a>
      }
      <div class="ui-shell-titles">
        <h1 class="ui-shell-title">{{ title() }}</h1>
        @if (subtitle()) {
          <p class="ui-shell-subtitle">{{ subtitle() }}</p>
        }
      </div>
      <div class="ui-shell-actions"><ng-content select="[actions]" /></div>
    </header>
    <div class="ui-shell-body"><ng-content /></div>
  `,
  styles: `
    :host { display: block; }

    .ui-shell-header {
      display: flex;
      align-items: center;
      gap: var(--space-4);
      padding-bottom: var(--space-4);
      margin-bottom: var(--space-5);
      border-bottom: 1px solid var(--surface-border);
    }
    .ui-shell-back {
      display: grid;
      place-items: center;
      width: 2.4rem;
      height: 2.4rem;
      flex-shrink: 0;
      border-radius: var(--radius-full);
      border: 2px solid var(--action);
      color: var(--action);
      font-size: 1.2rem;
      text-decoration: none;
      transition: background 140ms ease, color 140ms ease;
    }
    .ui-shell-back:hover { background: var(--action); color: var(--text-on-action); }
    .ui-shell-back:focus-visible { outline: 2px solid var(--focus); outline-offset: 2px; }

    .ui-shell-titles { flex: 1; min-width: 0; }
    .ui-shell-title {
      margin: 0;
      font-family: var(--font-display);
      font-size: var(--text-2xl);
      letter-spacing: 0.04em;
      line-height: 1.1;
    }
    .ui-shell-subtitle { margin: var(--space-1) 0 0; color: var(--text-muted); font-size: var(--text-sm); }
    .ui-shell-actions { display: flex; align-items: center; gap: var(--space-2); }
  `,
  changeDetection: ChangeDetectionStrategy.OnPush,
})
export class PageShellComponent {
  readonly title = input.required<string>();
  readonly subtitle = input('');
  readonly backLink = input<string | null>(null);
  readonly backLabel = input('Volver');
}
