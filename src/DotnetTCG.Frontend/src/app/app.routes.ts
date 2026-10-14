import { Routes } from '@angular/router';
import { NotFoundPage } from './core/layout/not-found.page';

// Las rutas de features (auth, decks, lobby, game) se agregan en F4.1–F4.4
// como lazy routes junto a sus páginas.
export const routes: Routes = [
  { path: '', pathMatch: 'full', redirectTo: 'login' },
  { path: '**', component: NotFoundPage },
];
