import { Component, inject, signal } from '@angular/core';
import { HttpErrorResponse } from '@angular/common/http';
import { FormControl, FormGroup, ReactiveFormsModule, Validators } from '@angular/forms';
import { ActivatedRoute, RouterLink } from '@angular/router';
import { catchError, of } from 'rxjs';
import { toApiError } from '../../../core/http/api-error';
import { SoundService } from '../../../core/sound/sound';
import { AuthApi } from '../data-access/auth.api';

type RecoveryState =
  | { readonly kind: 'request' }
  | { readonly kind: 'sending' }
  | { readonly kind: 'sent' }
  | { readonly kind: 'resetting' }
  | { readonly kind: 'done' }
  | { readonly kind: 'error'; readonly message: string; readonly step: 'request' | 'reset' };

@Component({
  imports: [ReactiveFormsModule, RouterLink],
  template: `
    <section class="auth-card" aria-labelledby="recovery-title">
      <h1 id="recovery-title">Recuperar contraseña</h1>
      @if (state().kind === 'done') {
        <p role="status">Contraseña actualizada. <a routerLink="/login">Ingresar</a></p>
      } @else if (state().kind === 'sent' || state().kind === 'resetting' || isResetError()) {
        <form [formGroup]="resetForm" (ngSubmit)="reset()">
          <label>
            Token
            <input type="text" formControlName="token" autocomplete="off" required />
          </label>
          <label>
            Nueva contraseña
            <input
              type="password"
              formControlName="newPassword"
              autocomplete="new-password"
              required
            />
          </label>
          @if (resetForm.controls.newPassword.touched && resetForm.controls.newPassword.invalid) {
            <p class="field-error" role="alert">Mínimo 8 caracteres.</p>
          }
          @if (isResetError()) {
            <p class="form-error" role="alert">{{ resetErrorMessage() }}</p>
          }
          <button type="submit" [disabled]="resetForm.invalid || state().kind === 'resetting'">
            @if (state().kind === 'resetting') {
              Actualizando…
            } @else {
              Actualizar contraseña
            }
          </button>
        </form>
      } @else {
        <form [formGroup]="requestForm" (ngSubmit)="request()">
          <label>
            Email
            <input type="email" formControlName="email" autocomplete="email" required />
          </label>
          @if (requestForm.controls.email.touched && requestForm.controls.email.invalid) {
            <p class="field-error" role="alert">Email válido requerido.</p>
          }
          @if (isRequestError()) {
            <p class="form-error" role="alert">{{ resetErrorMessage() }}</p>
          }
          <button type="submit" [disabled]="requestForm.invalid || state().kind === 'sending'">
            @if (state().kind === 'sending') {
              Enviando…
            } @else {
              Enviar token
            }
          </button>
        </form>
      }
      <p class="auth-links">
        <a routerLink="/login">Volver</a>
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
export class RecoveryPage {
  private readonly api = inject(AuthApi);
  private readonly sound = inject(SoundService);

  protected readonly requestForm = new FormGroup({
    email: new FormControl('', {
      nonNullable: true,
      validators: [Validators.required, Validators.email],
    }),
  });

  protected readonly resetForm = new FormGroup({
    token: new FormControl(inject(ActivatedRoute).snapshot.queryParamMap.get('token') ?? '', {
      nonNullable: true,
      validators: [Validators.required],
    }),
    newPassword: new FormControl('', {
      nonNullable: true,
      validators: [Validators.required, Validators.minLength(8)],
    }),
  });

  protected readonly state = signal<RecoveryState>({ kind: 'request' });

  protected isRequestError(): boolean {
    const state = this.state();
    return state.kind === 'error' && state.step === 'request';
  }

  protected isResetError(): boolean {
    const state = this.state();
    return state.kind === 'error' && state.step === 'reset';
  }

  protected resetErrorMessage(): string {
    const state = this.state();
    return state.kind === 'error' ? state.message : '';
  }

  protected request(): void {
    if (this.requestForm.invalid || this.state().kind === 'sending') {
      return;
    }
    this.state.set({ kind: 'sending' });
    this.api
      .requestRecovery(this.requestForm.getRawValue().email)
      .pipe(
        catchError((response: HttpErrorResponse) => {
          const error = toApiError(response);
          this.state.set({ kind: 'error', message: error.message, step: 'request' });
          this.sound.playError();
          return of(null);
        }),
      )
      .subscribe((result) => {
        if (result === null) {
          return;
        }
        this.state.set({ kind: 'sent' });
      });
  }

  protected reset(): void {
    if (this.resetForm.invalid || this.state().kind === 'resetting') {
      return;
    }
    this.state.set({ kind: 'resetting' });
    const { token, newPassword } = this.resetForm.getRawValue();
    this.api
      .resetPassword(token, newPassword)
      .pipe(
        catchError((response: HttpErrorResponse) => {
          const error = toApiError(response);
          this.state.set({ kind: 'error', message: error.message, step: 'reset' });
          this.sound.playError();
          return of(null);
        }),
      )
      .subscribe((result) => {
        if (result === null) {
          return;
        }
        this.state.set({ kind: 'done' });
      });
  }
}
