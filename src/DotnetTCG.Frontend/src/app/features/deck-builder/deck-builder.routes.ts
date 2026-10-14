import { Routes } from '@angular/router';
import { unsavedChangesGuard } from '../../core/guards/unsaved-changes.guard';

export const DECK_BUILDER_ROUTES: Routes = [
  {
    path: '',
    canDeactivate: [unsavedChangesGuard],
    loadComponent: () => import('./pages/deck-builder-page/deck-builder-page').then(m => m.DeckBuilderPageComponent)
  },
  {
    path: ':id',
    canDeactivate: [unsavedChangesGuard],
    loadComponent: () => import('./pages/deck-builder-page/deck-builder-page').then(m => m.DeckBuilderPageComponent)
  }
];
