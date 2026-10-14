import { Routes } from '@angular/router';
import { authGuard } from '../../core/auth/guards';

export const gameRoutes: Routes = [
  {
    path: '',
    canActivate: [authGuard],
    loadComponent: () =>
      import('./pages/game.page').then((m) => m.GamePage),
  },
];
