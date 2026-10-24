import { ChangeDetectionStrategy, Component, input } from '@angular/core';

/**
 * Campo de formulario: label + control proyectado + mensaje de error.
 * El control proyectado debe recibir [id]="fieldId()" y
 * [attr.aria-describedby]="error() ? fieldId() + '-error' : null" para
 * vincular label y error de forma accesible. El estilo del input vive en
 * una regla global (styles.css) porque el input proyectado pertenece al
 * scope del padre, no al de este componente.
 */
@Component({
  selector: 'ui-form-field',
  template: `
    <label class="ui-field-label" [attr.for]="fieldId()">{{ label() }}</label>
    <div class="ui-field-control"><ng-content /></div>
    @if (error()) {
      <p class="ui-field-error" [id]="fieldId() + '-error'">{{ error() }}</p>
    }
  `,
  styles: `
    :host { display: block; margin-bottom: var(--space-4); }

    .ui-field-label {
      display: block;
      margin-bottom: var(--space-2);
      font-weight: 700;
      font-size: var(--text-sm);
      color: var(--text-primary);
    }

    .ui-field-error {
      margin: var(--space-1) 0 0;
      color: #fca5a5;
      font-size: var(--text-xs);
    }
  `,
  changeDetection: ChangeDetectionStrategy.OnPush,
})
export class FormFieldComponent {
  readonly label = input.required<string>();
  readonly fieldId = input.required<string>();
  readonly error = input<string | null>(null);
}
