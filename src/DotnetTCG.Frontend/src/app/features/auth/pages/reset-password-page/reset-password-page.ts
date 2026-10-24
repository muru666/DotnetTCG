import { ChangeDetectionStrategy, Component, OnInit, inject, signal } from '@angular/core';
import { AbstractControl, FormBuilder, ReactiveFormsModule, ValidationErrors, Validators } from '@angular/forms';
import { ActivatedRoute, Router, RouterLink } from '@angular/router';
import { BackgroundImageService } from '../../../../core/services/background-image.service';
import { AuthService } from '../../../../core/services/auth.service';
import { ButtonComponent, PanelComponent, FormFieldComponent, CalloutComponent } from '../../../../shared/ui';

function passwordMatchValidator(group: AbstractControl): ValidationErrors | null {
  const password = group.get('password')?.value;
  const confirmPassword = group.get('confirmPassword')?.value;

  if (!password || !confirmPassword) {
    return null;
  }

  return password === confirmPassword ? null : { passwordMismatch: true };
}

@Component({
  selector: 'app-reset-password-page',
  standalone: true,
  imports: [ReactiveFormsModule, RouterLink, ButtonComponent, PanelComponent, FormFieldComponent, CalloutComponent],
  templateUrl: './reset-password-page.html',
  changeDetection: ChangeDetectionStrategy.OnPush
})
export class ResetPasswordPageComponent implements OnInit {
  private readonly formBuilder = inject(FormBuilder);
  private readonly authService = inject(AuthService);
  private readonly route = inject(ActivatedRoute);
  private readonly router = inject(Router);
  private readonly backgroundImageService = inject(BackgroundImageService);

  protected readonly backgroundImage = signal(this.backgroundImageService.getAuthBackgroundImage());
  protected readonly tokenInput = signal('');
  protected readonly isSubmitting = signal(false);
  protected readonly isTokenValid = signal(false);
  protected readonly requestError = signal<string | null>(null);
  protected readonly successMessage = signal<string | null>(null);
  protected readonly submitAttempted = signal(false);

  private recoveryToken = '';

  protected readonly form = this.formBuilder.group(
    {
      password: ['', [Validators.required, Validators.minLength(8)]],
      confirmPassword: ['', [Validators.required]]
    },
    { validators: passwordMatchValidator }
  );

  ngOnInit(): void {
    // El email trae el codigo crudo: se acepta por ?token= o pegado a mano.
    const token = this.route.snapshot.queryParamMap.get('token');
    if (token) {
      this.tokenInput.set(token);
      this.validateToken(token);
    }
  }

  protected validateTokenInput(): void {
    const token = this.tokenInput().trim();
    if (!token) return;
    this.validateToken(token);
  }

  private validateToken(token: string): void {
    this.requestError.set(null);
    this.recoveryToken = token;
    this.authService.validateRecoveryToken(token).subscribe({
      next: () => {
        this.isTokenValid.set(true);
      },
      error: (error: Error) => {
        this.requestError.set(error.message);
        this.isTokenValid.set(false);
      }
    });
  }

  protected submit(): void {
    this.submitAttempted.set(true);
    this.requestError.set(null);

    if (!this.isTokenValid() || this.form.invalid) {
      this.form.markAllAsTouched();
      return;
    }

    const { password } = this.form.getRawValue();
    this.isSubmitting.set(true);

    this.authService.resetPassword(this.recoveryToken, password!).subscribe({
      next: () => {
        this.isSubmitting.set(false);
        this.successMessage.set('Contraseña cambiada correctamente.');
        setTimeout(() => {
          this.router.navigate(['/inicio']);
        }, 400);
      },
      error: (error: Error) => {
        this.isSubmitting.set(false);
        this.requestError.set(error.message);
      }
    });
  }

  protected showControlError(controlName: string): boolean {
    const control = this.form.get(controlName);
    return Boolean(control && control.invalid && (control.touched || this.submitAttempted()));
  }

  protected passwordErrorMessage(): string {
    const password = this.form.get('password');
    if (password?.hasError('required')) {
      return 'La contraseña es obligatoria.';
    }

    return 'Debe tener al menos 8 caracteres.';
  }

  protected confirmPasswordErrorMessage(): string {
    const confirmPassword = this.form.get('confirmPassword');
    if (confirmPassword?.hasError('required')) {
      return 'Confirma la contraseña.';
    }

    if (this.form.hasError('passwordMismatch')) {
      return 'La confirmacion debe coincidir con la contraseña.';
    }

    return 'Dato invalido.';
  }
}