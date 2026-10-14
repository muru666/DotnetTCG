import { Component, inject, signal } from '@angular/core';
import { HttpErrorResponse } from '@angular/common/http';
import { FormControl, FormGroup, ReactiveFormsModule, Validators } from '@angular/forms';
import { Router, RouterLink } from '@angular/router';
import { catchError, of, switchMap } from 'rxjs';
import { toApiError } from '../../../core/http/api-error';
import { SessionStore } from '../../../core/auth/session';
import { SoundService } from '../../../core/sound/sound';
import { AuthApi } from '../data-access/auth.api';

type RegisterState =
  | { readonly kind: 'idle' }
  | { readonly kind: 'loading' }
  | { readonly kind: 'error'; readonly message: string };

@Component({
  imports: [ReactiveFormsModule, RouterLink],
  template: `
    <section class="auth-card" aria-labelledby="register-title">
      <h1 id="register-title">Crear cuenta</h1>
      <form [formGroup]="form" (ngSubmit)="submit()">
        <label>
          Usuario
          <input type="text" formControlName="username" autocomplete="username" required />
        </label>
        @if (form.controls.username.touched && form.controls.username.invalid) {
          <p class="field-error" role="alert">Usuario requerido (3-50 caracteres).</p>
        }
        <label>
          Email
          <input type="email" formControlName="email" autocomplete="email" required />
        </label>
        @if (form.controls.email.touched && form.controls.email.invalid) {
          <p class="field-error" role="alert">Email válido requerido.</p>
        }
        <label>
          Contraseña
          <input
            type="password"
            formControlName="password"
            autocomplete="new-password"
            required
          />
        </label>
        @if (form.controls.password.touched && form.controls.password.invalid) {
          <p class="field-error" role="alert">Mínimo 8 caracteres.</p>
        }
        @if (state(); as s) {
          @if (s.kind === 'error') {
            <p class="form-error" role="alert">{{ s.message }}</p>
          }
        }
        <button type="submit" [disabled]="form.invalid || state().kind === 'loading'">
          @if (state().kind === 'loading') {
            Creando…
          } @else {
            Crear cuenta
          }
        </button>
      </form>
      <p class="auth-links">
        <a routerLink="/login">Ya tengo cuenta</a>
      </p>
    </section>
  `,
  styles: [
    `
      .auth-card {
        max-width: 26rem;
        margin: 3rem auto;
        padding: 1.5rem;
        background: var(--surface-1);
        border: 1px solid var(--surface-border);
        border-radius: var(--radius-md);
        box-shadow: var(--shadow-md);
      }
      h1 {
        margin: 0 0 1rem;
      }
      form {
        display: flex;
        flex-direction: column;
        gap: 0.75rem;
      }
      label {
        display: flex;
        flex-direction: column;
        gap: 0.35rem;
        color: var(--text-secondary);
      }
      button[type='submit'] {
        margin-top: 0.5rem;
        background: var(--accent);
        color: var(--accent-text);
        border: none;
        border-radius: var(--radius-sm);
        padding: 0.6rem;
        font-weight: 600;
      }
      button[type='submit']:hover:not(:disabled) {
        background: var(--accent-hover);
      }
      .field-error,
      .form-error {
        margin: 0;
        color: var(--accent-danger);
      }
      .auth-links {
        margin: 1rem 0 0;
      }
    `,
  ],
})
export class RegisterPage {
  private readonly api = inject(AuthApi);
  private readonly session = inject(SessionStore);
  private readonly router = inject(Router);
  private readonly sound = inject(SoundService);

  protected readonly form = new FormGroup({
    username: new FormControl('', {
      nonNullable: true,
      validators: [Validators.required, Validators.minLength(3), Validators.maxLength(50)],
    }),
    email: new FormControl('', {
      nonNullable: true,
      validators: [Validators.required, Validators.email, Validators.maxLength(200)],
    }),
    password: new FormControl('', {
      nonNullable: true,
      validators: [Validators.required, Validators.minLength(8)],
    }),
  });

  protected readonly state = signal<RegisterState>({ kind: 'idle' });

  protected submit(): void {
    if (this.form.invalid || this.state().kind === 'loading') {
      return;
    }
    this.state.set({ kind: 'loading' });
    const body = this.form.getRawValue();
    // El registro no devuelve token: encadenar login con las mismas credenciales.
    this.api
      .register(body)
      .pipe(
        switchMap(() => this.api.login({ username: body.username, password: body.password })),
        catchError((response: HttpErrorResponse) => {
          const error = toApiError(response);
          this.state.set({ kind: 'error', message: error.message });
          this.sound.playError();
          return of(null);
        }),
      )
      .subscribe((tokens) => {
        if (tokens === null) {
          return;
        }
        this.session.setSession(tokens.accessToken);
        void this.router.navigate(['/decks']);
      });
  }
}
