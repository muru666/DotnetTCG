import { Routes } from '@angular/router';

export const GAME_ROUTES: Routes = [
  {
    path: 'spectator/:gameId',
    loadComponent: () => import('./pages/spectator-page/spectator-page').then(m => m.SpectatorPageComponent)
  },
  {
    path: ':gameId',
    loadComponent: () => import('./pages/game-page/game-page').then(m => m.GamePageComponent)
  }
];
