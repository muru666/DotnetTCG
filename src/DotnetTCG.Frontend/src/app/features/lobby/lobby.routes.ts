import { Routes } from '@angular/router';

export const LOBBY_ROUTES: Routes = [
  {
    path: '',
    loadComponent: () => import('./pages/lobby-page/lobby-page').then(m => m.GameSearcherComponent)
  },
  {
    path: ':gameId',
    loadComponent: () => import('./pages/game-lobby/game-lobby-page').then(m => m.GameLobbyPageComponent)
  }
];
