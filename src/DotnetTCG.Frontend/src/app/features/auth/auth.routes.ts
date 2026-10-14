import { Routes } from '@angular/router';
import { guestGuard } from '../../core/auth/guards';

export const authRoutes: Routes = [
  {
    path: 'login',
    canActivate: [guestGuard],
    loadComponent: () => import('./pages/login.page').then((m) => m.LoginPage),
  },
  {
    path: 'register',
    canActivate: [guestGuard],
    loadComponent: () =>
      import('./pages/register.page').then((m) => m.RegisterPage),
  },
  {
    path: 'recovery',
    canActivate: [guestGuard],
    loadComponent: () =>
      import('./pages/recovery.page').then((m) => m.RecoveryPage),
  },
];
