import { ChangeDetectionStrategy, Component, input } from '@angular/core';

type BadgeTone = 'neutral' | 'action' | 'success' | 'danger' | 'info';

/** Etiqueta compacta: tipo de carta, estado, contador. */
@Component({
  selector: 'ui-badge',
  host: { '[class]': "'ui-badge ui-badge--' + tone()" },
  template: `<ng-content />`,
  styles: `
    :host {
      display: inline-flex;
      align-items: center;
      gap: var(--space-1);
      padding: 0.1rem var(--space-2);
      border-radius: var(--radius-full);
      border: 1px solid transparent;
      font-size: var(--text-xs);
      font-weight: 800;
      letter-spacing: 0.03em;
      text-transform: uppercase;
      line-height: 1.5;
      white-space: nowrap;
    }
    :host(.ui-badge--neutral) { background: rgba(148, 163, 184, 0.15); color: var(--text-muted); border-color: var(--surface-border); }
    :host(.ui-badge--action) { background: var(--action-soft); color: #fcd34d; border-color: rgba(245, 158, 11, 0.4); }
    :host(.ui-badge--success) { background: var(--success-soft); color: #86efac; border-color: rgba(34, 197, 94, 0.4); }
    :host(.ui-badge--danger) { background: var(--danger-soft); color: #fca5a5; border-color: rgba(239, 68, 68, 0.4); }
    :host(.ui-badge--info) { background: rgba(59, 130, 246, 0.15); color: #93c5fd; border-color: rgba(59, 130, 246, 0.4); }
  `,
  changeDetection: ChangeDetectionStrategy.OnPush,
})
export class BadgeComponent {
  readonly tone = input<BadgeTone>('neutral');
}
