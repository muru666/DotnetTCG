import { ChangeDetectionStrategy, Component, OnDestroy, inject, signal } from '@angular/core';
import { FormBuilder, ReactiveFormsModule, Validators } from '@angular/forms';
import { RouterLink } from '@angular/router';
import { interval, Subscription, take } from 'rxjs';
import { BackgroundImageService } from '../../../../core/services/background-image.service';
import { AuthService } from '../../../../core/services/auth.service';
import { ButtonComponent, PanelComponent, FormFieldComponent, CalloutComponent } from '../../../../shared/ui';

@Component({
  selector: 'app-recover-password-page',
  standalone: true,
  imports: [ReactiveFormsModule, RouterLink, ButtonComponent, PanelComponent, FormFieldComponent, CalloutComponent],
  templateUrl: './recover-password-page.html',
  changeDetection: ChangeDetectionStrategy.OnPush
})
export class RecoverPasswordPageComponent implements OnDestroy {
  private readonly formBuilder = inject(FormBuilder);
  private readonly authService = inject(AuthService);
  private readonly backgroundImageService = inject(BackgroundImageService);

  protected readonly backgroundImage = signal(this.backgroundImageService.getAuthBackgroundImage());
  protected readonly isSubmitting = signal(false);
  protected readonly requestError = signal<string | null>(null);
  protected readonly successMessage = signal<string | null>(null);
  protected readonly submitAttempted = signal(false);
  protected readonly resendSeconds = signal(0);

  protected readonly form = this.formBuilder.group({
    email: ['', [Validators.required, Validators.email]]
  });

  private cooldownSubscription?: Subscription;

  ngOnDestroy(): void {
    this.cooldownSubscription?.unsubscribe();
  }

  protected submit(): void {
    this.submitAttempted.set(true);
    this.requestError.set(null);

    if (this.form.invalid || this.resendSeconds() > 0) {
      this.form.markAllAsTouched();
      return;
    }

    const { email } = this.form.getRawValue();
    this.isSubmitting.set(true);

    this.authService.requestPasswordRecovery(email!).subscribe({
      next: () => {
        this.isSubmitting.set(false);
        this.successMessage.set('Te enviamos un codigo a tu correo. Ingresalo en la pantalla de cambio de contraseña.');
        this.startCooldown();
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

  private startCooldown(): void {
    this.resendSeconds.set(60);
    this.cooldownSubscription?.unsubscribe();

    this.cooldownSubscription = interval(1000)
      .pipe(take(60))
      .subscribe((tick: number) => {
        this.resendSeconds.set(59 - tick);
      });
  }
}