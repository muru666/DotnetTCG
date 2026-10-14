import { Component, inject, signal } from '@angular/core';
import { HttpErrorResponse } from '@angular/common/http';
import { FormControl, FormGroup, ReactiveFormsModule, Validators } from '@angular/forms';
import { Router, RouterLink } from '@angular/router';
import { catchError, of } from 'rxjs';
import { toApiError } from '../../../core/http/api-error';
import { SoundService } from '../../../core/sound/sound';
import { DecksApi } from '../data-access/decks.api';
import { DeckDto } from '../models/deck.models';

type ListState =
  | { readonly kind: 'loading' }
  | { readonly kind: 'ready' }
  | { readonly kind: 'error'; readonly message: string };

@Component({
  imports: [ReactiveFormsModule, RouterLink],
  template: `
    <section aria-labelledby="decks-title">
      <h1 id="decks-title">Mis mazos</h1>
      <form class="create-row" [formGroup]="createForm" (ngSubmit)="create()">
        <label class="visually-hidden" for="deck-name">Nombre del mazo</label>
        <input id="deck-name" type="text" formControlName="name" placeholder="Nombre del mazo" required />
        <button type="submit" [disabled]="createForm.invalid">Crear</button>
      </form>
      @if (state().kind === 'loading') {
        <p role="status">Cargando mazos…</p>
      } @else if (state().kind === 'error') {
        <p class="error" role="alert">{{ errorMessage() }}</p>
      } @else if (decks().length === 0) {
        <p class="empty">Todavía no tenés mazos. Creá el primero arriba.</p>
      } @else {
        <ul class="deck-list">
          @for (deck of decks(); track deck.id) {
            <li class="deck-row">
              <a [routerLink]="['/decks', deck.id]">{{ deck.name }}</a>
              <span class="deck-meta">{{ cardCount(deck) }}/60</span>
              @if (deck.isValid) {
                <span class="badge-ok">Válido</span>
              } @else {
                <span class="badge-pending">Borrador</span>
              }
              <button type="button" (click)="remove(deck.id)" aria-label="Borrar {{ deck.name }}">
                Borrar
              </button>
            </li>
          }
        </ul>
      }
    </section>
  `,
  styles: [
    `
      .create-row {
        display: flex;
        gap: 0.5rem;
        margin-bottom: 1rem;
      }
      .create-row input {
        flex: 1;
      }
      .create-row button,
      .deck-row button {
        background: var(--accent);
        color: var(--accent-text);
        border: none;
        border-radius: var(--radius-sm);
        padding: 0.5rem 0.9rem;
        font-weight: 600;
      }
      .create-row button:hover:not(:disabled),
      .deck-row button:hover {
        background: var(--accent-hover);
      }
      .deck-row button {
        background: var(--surface-3);
        color: var(--text-primary);
        border: 1px solid var(--surface-border);
      }
      .error {
        color: var(--accent-danger);
      }
      .empty {
        color: var(--text-secondary);
      }
      .deck-list {
        list-style: none;
        margin: 0;
        padding: 0;
        display: flex;
        flex-direction: column;
        gap: 0.5rem;
      }
      .deck-row {
        display: flex;
        align-items: center;
        gap: 0.75rem;
        background: var(--surface-1);
        border: 1px solid var(--surface-border);
        border-radius: var(--radius-md);
        padding: 0.75rem 1rem;
      }
      .deck-row a {
        font-weight: 600;
      }
      .deck-meta {
        margin-left: auto;
        color: var(--text-secondary);
      }
      .badge-ok {
        color: var(--accent-ok);
        font-size: 0.8rem;
      }
      .badge-pending {
        color: var(--accent-warn);
        font-size: 0.8rem;
      }
      .visually-hidden {
        position: absolute;
        width: 1px;
        height: 1px;
        overflow: hidden;
        clip: rect(0 0 0 0);
      }
    `,
  ],
})
export class DeckListPage {
  private readonly api = inject(DecksApi);
  private readonly router = inject(Router);
  private readonly sound = inject(SoundService);

  protected readonly decks = signal<readonly DeckDto[]>([]);
  protected readonly state = signal<ListState>({ kind: 'loading' });

  protected readonly createForm = new FormGroup({
    name: new FormControl('', {
      nonNullable: true,
      validators: [Validators.required, Validators.maxLength(100)],
    }),
  });

  constructor() {
    this.reload();
  }

  protected errorMessage(): string {
    const state = this.state();
    return state.kind === 'error' ? state.message : '';
  }

  protected cardCount(deck: DeckDto): number {
    return deck.cards.reduce((total, card) => total + card.quantity, 0);
  }

  protected create(): void {
    if (this.createForm.invalid) {
      return;
    }
    this.api
      .create(this.createForm.getRawValue().name.trim())
      .pipe(catchError((response: HttpErrorResponse) => this.fail(response)))
      .subscribe((deck) => {
        if (deck === null) {
          return;
        }
        this.createForm.reset();
        void this.router.navigate(['/decks', deck.id]);
      });
  }

  protected remove(id: string): void {
    this.api
      .delete(id)
      .pipe(catchError((response: HttpErrorResponse) => this.fail(response)))
      .subscribe((result) => {
        if (result === null) {
          return;
        }
        this.decks.update((decks) => decks.filter((deck) => deck.id !== id));
      });
  }

  private reload(): void {
    this.state.set({ kind: 'loading' });
    this.api
      .list()
      .pipe(catchError((response: HttpErrorResponse) => this.fail(response)))
      .subscribe((decks) => {
        if (decks === null) {
          return;
        }
        this.decks.set(decks);
        this.state.set({ kind: 'ready' });
      });
  }

  private fail(response: HttpErrorResponse) {
    const error = toApiError(response);
    this.state.set({ kind: 'error', message: error.message });
    this.sound.playError();
    return of(null);
  }
}
