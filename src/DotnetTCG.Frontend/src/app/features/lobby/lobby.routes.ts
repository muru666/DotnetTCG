import { Routes } from '@angular/router';
import { authGuard } from '../../core/auth/guards';

export const lobbyRoutes: Routes = [
  {
    path: '',
    canActivate: [authGuard],
    loadComponent: () =>
      import('./pages/lobby.page').then((m) => m.LobbyPage),
  },
];
