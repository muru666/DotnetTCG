import { ChangeDetectionStrategy, Component, inject, signal } from '@angular/core';
import { Router } from '@angular/router';
import { BackgroundImageService } from '../../../../core/services/background-image.service';
import { ButtonComponent, PanelComponent } from '../../../../shared/ui';

@Component({
  selector: 'app-home-page',
  standalone: true,
  imports: [ButtonComponent, PanelComponent],
  templateUrl: './home-page.html',
  changeDetection: ChangeDetectionStrategy.OnPush
})
export class HomePageComponent {
  private readonly backgroundImageService = inject(BackgroundImageService);
  private readonly router = inject(Router);

  protected readonly backgroundImage = signal(this.backgroundImageService.getAuthBackgroundImage());

  protected goLogin(): void {
    this.router.navigate(['/iniciar-sesion']);
  }

  protected goRegister(): void {
    this.router.navigate(['/crear-usuario']);
  }
}