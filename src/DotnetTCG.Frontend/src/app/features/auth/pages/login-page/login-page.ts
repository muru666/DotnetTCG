import { ChangeDetectionStrategy, Component, inject, signal } from '@angular/core';
import { FormBuilder, ReactiveFormsModule, Validators } from '@angular/forms';
import { Router, RouterLink } from '@angular/router';
import { BackgroundImageService } from '../../../../core/services/background-image.service';
import { AuthService } from '../../../../core/services/auth.service';

@Component({
  selector: 'app-login-page',
  standalone: true,
  imports: [ReactiveFormsModule, RouterLink],
  templateUrl: './login-page.html',
  changeDetection: ChangeDetectionStrategy.OnPush
})
export class LoginPageComponent {
  private readonly formBuilder = inject(FormBuilder);
  private readonly authService = inject(AuthService);
  private readonly router = inject(Router);
  private readonly backgroundImageService = inject(BackgroundImageService);

  protected readonly backgroundImage = signal(this.backgroundImageService.getAuthBackgroundImage());
  protected readonly isSubmitting = signal(false);
  protected readonly requestError = signal<string | null>(null);
  protected readonly submitAttempted = signal(false);

  protected readonly form = this.formBuilder.group({
    usernameOrEmail: ['', [Validators.required]],
    password: ['', [Validators.required]]
  });

  protected submit(): void {
    this.submitAttempted.set(true);
    this.requestError.set(null);

    if (this.form.invalid) {
      this.form.markAllAsTouched();
      return;
    }

    const { usernameOrEmail, password } = this.form.getRawValue();
    this.isSubmitting.set(true);

    this.authService.login(usernameOrEmail!, password!).subscribe({
      next: () => {
        this.isSubmitting.set(false);
        this.router.navigate(['/main-menu']);
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
}