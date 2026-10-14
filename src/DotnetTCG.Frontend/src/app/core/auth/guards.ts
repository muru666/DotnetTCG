import { inject } from '@angular/core';
import { CanActivateFn, Router } from '@angular/router';
import { SessionStore } from './session';

/** Solo UX: el backend autoriza cada operación igual. */
export const authGuard: CanActivateFn = () => {
  if (inject(SessionStore).isAuthenticated()) {
    return true;
  }
  return inject(Router).createUrlTree(['/login']);
};

export const guestGuard: CanActivateFn = () => {
  if (inject(SessionStore).isAuthenticated()) {
    return inject(Router).createUrlTree(['/decks']);
  }
  return true;
};
