import { ChangeDetectionStrategy, Component, inject, signal } from '@angular/core';
import { AbstractControl, FormBuilder, ReactiveFormsModule, ValidationErrors, Validators } from '@angular/forms';
import { Router, RouterLink } from '@angular/router';
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
  selector: 'app-register-page',
  standalone: true,
  imports: [ReactiveFormsModule, RouterLink, ButtonComponent, PanelComponent, FormFieldComponent, CalloutComponent],
  templateUrl: './register-page.html',
  changeDetection: ChangeDetectionStrategy.OnPush
})
export class RegisterPageComponent {
  private readonly formBuilder = inject(FormBuilder);
  private readonly authService = inject(AuthService);
  private readonly router = inject(Router);
  private readonly backgroundImageService = inject(BackgroundImageService);

  protected readonly backgroundImage = signal(this.backgroundImageService.getAuthBackgroundImage());
  protected readonly isSubmitting = signal(false);
  protected readonly requestError = signal<string | null>(null);
  protected readonly submitAttempted = signal(false);

  protected readonly form = this.formBuilder.group(
    {
      username: ['', [Validators.required, Validators.pattern(/^[A-Za-z0-9_]{3,}$/)]],
      email: ['', [Validators.required, Validators.email]],
      password: ['', [Validators.required, Validators.minLength(8)]],
      confirmPassword: ['', [Validators.required]]
    },
    { validators: passwordMatchValidator }
  );

  protected submit(): void {
    this.submitAttempted.set(true);
    this.requestError.set(null);

    if (this.form.invalid) {
      this.form.markAllAsTouched();
      return;
    }

    const { username, email, password } = this.form.getRawValue();
    this.isSubmitting.set(true);

    this.authService.register({ username: username!, email: email!, password: password! }).subscribe({
      next: () => {
        this.isSubmitting.set(false);
        this.router.navigate(['/iniciar-sesion']);
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

  protected usernameErrorMessage(): string {
    const username = this.form.get('username');
    if (username?.hasError('required')) {
      return 'El nombre de usuario es obligatorio.';
    }

    return 'Debe tener minimo 3 caracteres y solo usar letras, numeros o guion bajo (_).';
  }

  protected emailErrorMessage(): string {
    const email = this.form.get('email');
    if (email?.hasError('required')) {
      return 'El email es obligatorio.';
    }

    return 'Ingresa un email valido.';
  }

  protected passwordErrorMessage(): string {
    const password = this.form.get('password');
    if (password?.hasError('required')) {
      return 'La contraseña es obligatoria.';
    }

    return 'La contraseña debe tener al menos 8 caracteres.';
  }

  protected confirmPasswordErrorMessage(): string {
    const confirmPassword = this.form.get('confirmPassword');
    if (confirmPassword?.hasError('required')) {
      return 'Confirma la contraseña.';
    }

    if (this.form.hasError('passwordMismatch')) {
      return 'La confirmacion debe ser igual a la contraseña ingresada.';
    }

    return 'Dato invalido.';
  }
}