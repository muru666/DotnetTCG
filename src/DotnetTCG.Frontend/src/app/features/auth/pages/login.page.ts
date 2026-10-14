import { Component, inject, signal } from '@angular/core';
import { HttpErrorResponse } from '@angular/common/http';
import { FormControl, FormGroup, ReactiveFormsModule, Validators } from '@angular/forms';
import { Router, RouterLink } from '@angular/router';
import { catchError, of } from 'rxjs';
import { toApiError } from '../../../core/http/api-error';
import { SessionStore } from '../../../core/auth/session';
import { SoundService } from '../../../core/sound/sound';
import { AuthApi } from '../data-access/auth.api';

type LoginState =
  | { readonly kind: 'idle' }
  | { readonly kind: 'loading' }
  | { readonly kind: 'error'; readonly message: string };

@Component({
  imports: [ReactiveFormsModule, RouterLink],
  template: `
    <section class="auth-card" aria-labelledby="login-title">
      <h1 id="login-title">Ingresar</h1>
      <form [formGroup]="form" (ngSubmit)="submit()">
        <label>
          Usuario
          <input type="text" formControlName="username" autocomplete="username" required />
        </label>
        @if (form.controls.username.touched && form.controls.username.invalid) {
          <p class="field-error" role="alert">Usuario requerido (3-50 caracteres).</p>
        }
        <label>
          Contraseña
          <input
            type="password"
            formControlName="password"
            autocomplete="current-password"
            required
          />
        </label>
        @if (form.controls.password.touched && form.controls.password.invalid) {
          <p class="field-error" role="alert">Contraseña requerida.</p>
        }
        @if (state(); as s) {
          @if (s.kind === 'error') {
            <p class="form-error" role="alert">{{ s.message }}</p>
          }
        }
        <button type="submit" [disabled]="form.invalid || state().kind === 'loading'">
          @if (state().kind === 'loading') {
            Ingresando…
          } @else {
            Ingresar
          }
        </button>
      </form>
      <p class="auth-links">
        <a routerLink="/recovery">Olvidé mi contraseña</a>
        <a routerLink="/register">Crear cuenta</a>
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
        display: flex;
        justify-content: space-between;
        margin: 1rem 0 0;
      }
    `,
  ],
})
export class LoginPage {
  private readonly api = inject(AuthApi);
  private readonly session = inject(SessionStore);
  private readonly router = inject(Router);
  private readonly sound = inject(SoundService);

  protected readonly form = new FormGroup({
    username: new FormControl('', {
      nonNullable: true,
      validators: [Validators.required, Validators.minLength(3), Validators.maxLength(50)],
    }),
    password: new FormControl('', {
      nonNullable: true,
      validators: [Validators.required],
    }),
  });

  protected readonly state = signal<LoginState>({ kind: 'idle' });

  protected submit(): void {
    if (this.form.invalid || this.state().kind === 'loading') {
      return;
    }
    this.state.set({ kind: 'loading' });
    const body = this.form.getRawValue();
    this.api
      .login(body)
      .pipe(
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
