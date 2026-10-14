import { Routes } from '@angular/router';
import { authGuard } from '../../core/auth/guards';

export const decksRoutes: Routes = [
  {
    path: '',
    canActivate: [authGuard],
    loadComponent: () =>
      import('./pages/deck-list.page').then((m) => m.DeckListPage),
  },
  {
    path: ':id',
    canActivate: [authGuard],
    loadComponent: () =>
      import('./pages/deck-builder.page').then((m) => m.DeckBuilderPage),
  },
];
