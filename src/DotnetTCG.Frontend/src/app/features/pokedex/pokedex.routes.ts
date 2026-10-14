import { Routes } from '@angular/router';

export const POKEDEX_ROUTES: Routes = [
  {
    path: '',
    loadComponent: () => import('./pages/pokedex-page/pokedex-page').then(m => m.PokedexPage)
  }
];
