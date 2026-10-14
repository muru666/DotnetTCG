import { Routes } from '@angular/router';
import { authGuard } from './core/guards/auth.guard';
import { redirectIfAuthenticatedGuard } from './core/guards/redirect-if-authenticated.guard';

export const routes: Routes = [
  {
    path: '',
    redirectTo: '/inicio',
    pathMatch: 'full'
  },
  {
    path: 'inicio',
    canActivate: [redirectIfAuthenticatedGuard],
    loadComponent: () => import('./features/auth/pages/home-page/home-page').then(m => m.HomePageComponent),
    data: { title: 'Inicio' }
  },
  {
    path: 'crear-usuario',
    canActivate: [redirectIfAuthenticatedGuard],
    loadComponent: () => import('./features/auth/pages/register-page/register-page').then(m => m.RegisterPageComponent),
    data: { title: 'Crear Usuario' }
  },
  {
    path: 'iniciar-sesion',
    canActivate: [redirectIfAuthenticatedGuard],
    loadComponent: () => import('./features/auth/pages/login-page/login-page').then(m => m.LoginPageComponent),
    data: { title: 'Iniciar Sesion' }
  },
  {
    path: 'recuperar-contrasena',
    canActivate: [redirectIfAuthenticatedGuard],
    loadComponent: () => import('./features/auth/pages/recover-password-page/recover-password-page').then(m => m.RecoverPasswordPageComponent),
    data: { title: 'Recuperar Contraseña' }
  },
  {
    path: 'api/auth/reset-password',
    canActivate: [redirectIfAuthenticatedGuard],
    loadComponent: () => import('./features/auth/pages/reset-password-page/reset-password-page').then(m => m.ResetPasswordPageComponent),
    data: { title: 'Cambiar Contraseña' }
  },
  {
    path: 'main-menu',
    canActivate: [authGuard],
    loadComponent: () => import('./features/main-menu/pages/main-menu-page/main-menu-page').then(m => m.MainMenuPageComponent),
    data: { title: 'Main Menu' }
  },
  {
    path: 'lobby',
    canActivate: [authGuard],
    loadChildren: () => import('./features/lobby/lobby.routes').then(m => m.LOBBY_ROUTES),
    data: { title: 'Game Lobby' }
  },
  {
    path: 'deck-builder',
    canActivate: [authGuard],
    loadChildren: () => import('./features/deck-builder/deck-builder.routes').then(m => m.DECK_BUILDER_ROUTES),
    data: { title: 'Deck Builder' }
  },
  {
    path: 'game',
    canActivate: [authGuard],
    loadChildren: () => import('./features/game/game.routes').then(m => m.GAME_ROUTES),
    data: { title: 'Pokemon TCG Game' }
  },
  {
    path: 'pokedex',
    canActivate: [authGuard],
    loadChildren: () => import('./features/pokedex/pokedex.routes').then(m => m.POKEDEX_ROUTES),
    data: { title: 'Pokédex' }
  },
  {
    path: '**',
    redirectTo: '/inicio'
  }
];
