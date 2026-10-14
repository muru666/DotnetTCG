import { Component, inject } from '@angular/core';
import { Router, RouterLink, RouterOutlet } from '@angular/router';
import { SessionStore } from '../auth/session';
import { SoundService } from '../sound/sound';

@Component({
  selector: 'app-shell',
  imports: [RouterLink, RouterOutlet],
  template: `
    <header class="shell-header">
      <a class="shell-brand" routerLink="/decks">DotnetTCG</a>
      <nav class="shell-nav" aria-label="Principal">
        @if (isAuthenticated()) {
          <a routerLink="/decks">Mazos</a>
          <a routerLink="/lobby">Lobby</a>
        } @else {
          <a routerLink="/login">Ingresar</a>
          <a routerLink="/register">Registrarse</a>
        }
      </nav>
      <div class="shell-user">
        @if (session(); as s) {
          <span class="shell-username">{{ s.username }}</span>
          <button type="button" (click)="logout()">Salir</button>
        }
        <button
          type="button"
          class="shell-mute"
          (click)="sound.toggleMute()"
          [attr.aria-label]="sound.muted() ? 'Activar sonido' : 'Silenciar'"
          [attr.aria-pressed]="sound.muted()"
        >
          {{ sound.muted() ? '🔇' : '🔊' }}
        </button>
      </div>
    </header>
    <main class="shell-main">
      <router-outlet />
    </main>
  `,
  styles: [
    `
      .shell-header {
        display: flex;
        align-items: center;
        gap: 1rem;
        padding: 0.75rem 1.25rem;
        background: var(--surface-1);
        border-bottom: 1px solid var(--surface-border);
      }
      .shell-brand {
        font-weight: 700;
        text-decoration: none;
        color: var(--text-primary);
      }
      .shell-nav {
        display: flex;
        gap: 1rem;
      }
      .shell-nav a {
        text-decoration: none;
        color: var(--text-secondary);
      }
      .shell-nav a:hover {
        color: var(--text-primary);
      }
      .shell-user {
        margin-left: auto;
        display: flex;
        align-items: center;
        gap: 0.75rem;
      }
      .shell-username {
        color: var(--text-secondary);
      }
      .shell-user button {
        background: var(--surface-2);
        color: var(--text-primary);
        border: 1px solid var(--surface-border);
        border-radius: var(--radius-sm);
        padding: 0.35rem 0.7rem;
      }
      .shell-user button:hover {
        background: var(--surface-3);
      }
      .shell-mute {
        font-size: 1rem;
        line-height: 1;
      }
      .shell-main {
        padding: 1.25rem;
        max-width: 1100px;
        margin: 0 auto;
      }
    `,
  ],
})
export class AppShell {
  private readonly store = inject(SessionStore);
  protected readonly session = this.store.session;
  protected readonly isAuthenticated = this.store.isAuthenticated;
  protected readonly sound = inject(SoundService);
  private readonly sessionStore = inject(SessionStore);
  private readonly router = inject(Router);

  protected logout(): void {
    this.sessionStore.clear();
    void this.router.navigate(['/login']);
  }
}
