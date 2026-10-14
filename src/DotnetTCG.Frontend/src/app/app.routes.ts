import { Routes } from '@angular/router';
import { NotFoundPage } from './core/layout/not-found.page';

// Cada feature expone sus rutas lazy (F4.1–F4.4 agregan las suyas aquí).
export const routes: Routes = [
  { path: '', pathMatch: 'full', redirectTo: 'login' },
  {
    path: '',
    loadChildren: () =>
      import('./features/auth/auth.routes').then((m) => m.authRoutes),
  },
  {
    path: 'decks',
    loadChildren: () =>
      import('./features/decks/decks.routes').then((m) => m.decksRoutes),
  },
  {
    path: 'lobby',
    loadChildren: () =>
      import('./features/lobby/lobby.routes').then((m) => m.lobbyRoutes),
  },
  {
    path: 'games/:id',
    loadChildren: () =>
      import('./features/game/game.routes').then((m) => m.gameRoutes),
  },
  { path: '**', component: NotFoundPage },
];
