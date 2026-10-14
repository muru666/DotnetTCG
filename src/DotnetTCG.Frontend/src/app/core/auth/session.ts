import { Injectable, computed, signal } from '@angular/core';

export interface Session {
  readonly token: string;
  readonly userId: string;
  readonly username: string;
}

const STORAGE_KEY = 'dotnettcp.session';

/**
 * Sesión en sessionStorage (muere con la pestaña; el servidor es quien autoriza).
 * El userId/username se leen del payload del JWT solo para mostrarlos.
 */
@Injectable({ providedIn: 'root' })
export class SessionStore {
  private readonly sessionState = signal<Session | null>(readStoredSession());

  readonly session = this.sessionState.asReadonly();
  readonly token = computed(() => this.sessionState()?.token ?? null);
  readonly isAuthenticated = computed(() => this.sessionState() !== null);

  setSession(token: string): void {
    const payload = decodePayload(token);
    const session: Session = {
      token,
      userId: typeof payload?.['sub'] === 'string' ? payload['sub'] : '',
      username: typeof payload?.['unique_name'] === 'string' ? payload['unique_name'] : '',
    };
    sessionStorage.setItem(STORAGE_KEY, token);
    this.sessionState.set(session);
  }

  clear(): void {
    sessionStorage.removeItem(STORAGE_KEY);
    this.sessionState.set(null);
  }
}

function readStoredSession(): Session | null {
  const token = sessionStorage.getItem(STORAGE_KEY);
  if (token === null || token.length === 0) {
    return null;
  }
  const payload = decodePayload(token);
  return {
    token,
    userId: typeof payload?.['sub'] === 'string' ? payload['sub'] : '',
    username: typeof payload?.['unique_name'] === 'string' ? payload['unique_name'] : '',
  };
}

function decodePayload(token: string): Record<string, unknown> | null {
  try {
    const [, payload] = token.split('.');
    if (payload === undefined) {
      return null;
    }
    return JSON.parse(atob(payload.replace(/-/g, '+').replace(/_/g, '/'))) as Record<string, unknown>;
  } catch {
    return null;
  }
}
