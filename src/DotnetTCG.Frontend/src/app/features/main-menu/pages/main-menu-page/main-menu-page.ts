import { ChangeDetectionStrategy, Component, inject, signal } from '@angular/core';
import { Router, RouterLink } from '@angular/router';
import { AuthService } from '../../../../core/services/auth.service';
import { BackgroundImageService } from '../../../../core/services/background-image.service';

@Component({
  selector: 'app-main-menu-page',
  standalone: true,
  imports: [RouterLink],
  templateUrl: './main-menu-page.html',
  styleUrl: './main-menu-page.css',
  changeDetection: ChangeDetectionStrategy.OnPush
})
export class MainMenuPageComponent {
  private readonly authService = inject(AuthService);
  private readonly router = inject(Router);
  private readonly backgroundImageService = inject(BackgroundImageService);

  protected readonly username = this.authService.getUsername();
  protected readonly backgroundImage = signal(this.backgroundImageService.getAuthBackgroundImage());

  protected onLogout(): void {
    this.authService.logout();
    this.router.navigate(['/inicio']);
  }
}
