import { Component, computed, DestroyRef, inject, signal } from '@angular/core';
import { HttpErrorResponse } from '@angular/common/http';
import { FormControl, FormGroup, ReactiveFormsModule, Validators } from '@angular/forms';
import { ActivatedRoute, RouterLink } from '@angular/router';
import { NgOptimizedImage } from '@angular/common';
import { takeUntilDestroyed } from '@angular/core/rxjs-interop';
import { catchError, debounceTime, distinctUntilChanged, merge, of, startWith, Subject, switchMap, tap } from 'rxjs';
import { toApiError } from '../../../core/http/api-error';
import { SoundService } from '../../../core/sound/sound';
import { CardsApi } from '../data-access/cards.api';
import { DecksApi } from '../data-access/decks.api';
import { CardDto, DeckDto, DeckValidation } from '../models/deck.models';

const PAGE_SIZE = 20;

const TYPE_FILTERS = [
  'all',
  'Grass',
  'Fire',
  'Water',
  'Lightning',
  'Psychic',
  'Fighting',
  'Darkness',
  'Metal',
  'Fairy',
  'Dragon',
  'Colorless',
] as const;

type BuilderState =
  | { readonly kind: 'loading' }
  | { readonly kind: 'ready' }
  | { readonly kind: 'error'; readonly message: string };

@Component({
  imports: [ReactiveFormsModule, RouterLink, NgOptimizedImage],
  template: `
    <a routerLink="/decks">← Mis mazos</a>
    @if (state().kind === 'loading') {
      <p role="status">Cargando mazo…</p>
    } @else if (state().kind === 'error') {
      <p class="error" role="alert">{{ loadError() }}</p>
    } @else if (deck(); as d) {
      <section aria-label="Constructor de mazo">
        <div class="builder-head">
          <input
            class="deck-name"
            type="text"
            [value]="d.name"
            (change)="rename(nameInput.value)"
            #nameInput
            aria-label="Nombre del mazo"
          />
          <span class="counter" [class.counter-full]="totalCards() >= 60">
            {{ totalCards() }}/60
          </span>
          @if (d.isValid) {
            <span class="badge-ok">Válido</span>
          } @else {
            <span class="badge-pending">Borrador</span>
          }
        </div>
        <div class="counter-bar" aria-hidden="true"><span [style.width.%]="deckPercent()"></span></div>
        @if (actionError() !== null) {
          <p class="error" role="alert">{{ actionError() }}</p>
        }
        <div class="builder-grid">
          <div class="search-pane">
            <h2>Buscar cartas</h2>
            <div class="search-row">
              <label class="visually-hidden" for="card-search">Nombre</label>
              <input
                id="card-search"
                type="search"
                [formControl]="searchControl"
                placeholder="Nombre…"
              />
              <label class="visually-hidden" for="type-filter">Tipo</label>
              <select id="type-filter" (change)="onTypeChange(typeSelect.value)" #typeSelect>
                @for (type of typeFilters; track type) {
                  <option [value]="type">{{ type === 'all' ? 'Todos' : type }}</option>
                }
              </select>
            </div>
            @if (searching()) {
              <p role="status">Buscando…</p>
            } @else if (results().length === 0) {
              <p class="empty">Sin resultados.</p>
            } @else {
              <ul class="results">
                @for (card of results(); track card.id) {
                  <li class="result-row">
                    <img
                      [ngSrc]="card.imageUrl"
                      [alt]="card.name"
                      width="48"
                      height="67"
                      class="result-thumb"
                      (click)="preview.set(card.imageUrl)"
                    />
                    <div class="result-info">
                      <strong>{{ card.name }}</strong>
                      <span>{{ card.supertype }}{{ card.hp !== null ? ' · ' + card.hp + ' HP' : '' }}</span>
                    </div>
                    <span class="slots">{{ slotsLabel(card) }}</span>
                    <button type="button" (click)="add(card)" [disabled]="!canAdd(card)">
                      Agregar
                    </button>
                  </li>
                }
              </ul>
            }
          </div>
          <div class="deck-pane">
            <h2>Mazo</h2>
            @if (d.cards.length === 0) {
              <p class="empty">Mazo vacío. Agregá cartas desde la búsqueda.</p>
            } @else {
              <ul class="deck-cards">
                @for (card of d.cards; track card.cardId) {
                  <li class="deck-card-row">
                    <span class="deck-card-name">{{ card.cardName }}</span>
                    <span class="qty">×{{ card.quantity }}</span>
                    <button type="button" (click)="changeQty(card.cardId, card.quantity - 1)" aria-label="Quitar una">
                      −
                    </button>
                    <button type="button" (click)="changeQty(card.cardId, card.quantity + 1)" aria-label="Agregar una">
                      +
                    </button>
                    <button type="button" (click)="removeCard(card.cardId)" aria-label="Quitar todas">
                      Quitar
                    </button>
                  </li>
                }
              </ul>
            }
            <button type="button" class="validate-btn" (click)="validate()">Validar mazo</button>
            @if (validation(); as v) {
              @if (v.valid) {
                <p class="valid-msg" role="status">Mazo válido para jugar.</p>
              } @else {
                <ul class="error-list" role="alert">
                  @for (error of v.errors; track error) {
                    <li>{{ error }}</li>
                  }
                </ul>
              }
            }
          </div>
        </div>
      </section>
    }
    @if (preview(); as url) {
      <div class="preview-overlay" (click)="preview.set(null)">
        <img [ngSrc]="url" alt="Vista previa" width="300" height="419" class="preview-image" />
      </div>
    }
  `,
  styles: [
    `
      .builder-head {
        display: flex;
        align-items: center;
        gap: 0.75rem;
        margin: 1rem 0 0.5rem;
      }
      .deck-name {
        font-size: 1.25rem;
        font-weight: 700;
        flex: 1;
      }
      .counter {
        font-weight: 700;
        color: var(--text-secondary);
      }
      .counter-full {
        color: var(--accent-ok);
      }
      .badge-ok {
        color: var(--accent-ok);
        font-size: 0.85rem;
      }
      .badge-pending {
        color: var(--accent-warn);
        font-size: 0.85rem;
      }
      .counter-bar {
        height: 6px;
        background: var(--surface-2);
        border-radius: 3px;
        overflow: hidden;
        margin-bottom: 1rem;
      }
      .counter-bar span {
        display: block;
        height: 100%;
        background: var(--accent);
      }
      .error {
        color: var(--accent-danger);
      }
      .empty {
        color: var(--text-secondary);
      }
      .builder-grid {
        display: grid;
        grid-template-columns: 1fr 1fr;
        gap: 1.5rem;
      }
      @media (max-width: 800px) {
        .builder-grid {
          grid-template-columns: 1fr;
        }
      }
      .search-pane h2,
      .deck-pane h2 {
        font-size: 1rem;
        color: var(--text-secondary);
      }
      .search-row {
        display: flex;
        gap: 0.5rem;
        margin-bottom: 0.75rem;
      }
      .search-row input {
        flex: 1;
      }
      .results,
      .deck-cards,
      .error-list {
        list-style: none;
        margin: 0;
        padding: 0;
        display: flex;
        flex-direction: column;
        gap: 0.4rem;
      }
      .result-row,
      .deck-card-row {
        display: flex;
        align-items: center;
        gap: 0.6rem;
        background: var(--surface-1);
        border: 1px solid var(--surface-border);
        border-radius: var(--radius-md);
        padding: 0.5rem 0.75rem;
      }
      .result-thumb {
        border-radius: 4px;
        cursor: zoom-in;
        flex-shrink: 0;
      }
      .result-info {
        display: flex;
        flex-direction: column;
        flex: 1;
        min-width: 0;
      }
      .result-info span {
        color: var(--text-secondary);
        font-size: 0.85rem;
      }
      .slots {
        color: var(--text-muted);
        font-size: 0.8rem;
        white-space: nowrap;
      }
      .result-row button,
      .deck-card-row button,
      .validate-btn {
        background: var(--surface-3);
        color: var(--text-primary);
        border: 1px solid var(--surface-border);
        border-radius: var(--radius-sm);
        padding: 0.35rem 0.7rem;
      }
      .result-row button:hover:not(:disabled),
      .deck-card-row button:hover:not(:disabled),
      .validate-btn:hover {
        background: var(--accent);
        border-color: var(--accent);
      }
      .deck-card-name {
        flex: 1;
      }
      .qty {
        color: var(--text-secondary);
        min-width: 2.5rem;
        text-align: right;
      }
      .validate-btn {
        margin-top: 1rem;
        width: 100%;
        padding: 0.6rem;
        font-weight: 600;
      }
      .valid-msg {
        color: var(--accent-ok);
      }
      .error-list {
        color: var(--accent-danger);
      }
      .preview-overlay {
        position: fixed;
        inset: 0;
        background: rgba(0, 0, 0, 0.75);
        display: flex;
        align-items: center;
        justify-content: center;
        z-index: 100;
      }
      .preview-image {
        border-radius: var(--radius-md);
        box-shadow: var(--shadow-md);
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
export class DeckBuilderPage {
  private readonly api = inject(DecksApi);
  private readonly cardsApi = inject(CardsApi);
  private readonly route = inject(ActivatedRoute);
  private readonly sound = inject(SoundService);
  private readonly destroyRef = inject(DestroyRef);

  protected readonly typeFilters: readonly string[] = TYPE_FILTERS;
  protected readonly deck = signal<DeckDto | null>(null);
  protected readonly state = signal<BuilderState>({ kind: 'loading' });
  protected readonly actionError = signal<string | null>(null);
  protected readonly validation = signal<DeckValidation | null>(null);
  protected readonly results = signal<readonly CardDto[]>([]);
  protected readonly searching = signal(false);
  protected readonly cardType = signal<string>('all');
  protected readonly preview = signal<string | null>(null);

  protected readonly searchControl = new FormControl('', { nonNullable: true });
  private readonly typeChanged = new Subject<void>();

  protected readonly totalCards = computed(() =>
    this.deck()?.cards.reduce((total, card) => total + card.quantity, 0) ?? 0,
  );

  protected readonly deckPercent = computed(() =>
    Math.min(100, (this.totalCards() / 60) * 100),
  );

  constructor() {
    const id = this.route.snapshot.paramMap.get('id');
    if (id === null) {
      this.state.set({ kind: 'error', message: 'Mazo no encontrado.' });
      return;
    }
    this.api
      .getById(id)
      .pipe(
        takeUntilDestroyed(this.destroyRef),
        catchError((response: HttpErrorResponse) => {
          const error = toApiError(response);
          this.state.set({ kind: 'error', message: error.message });
          this.sound.playError();
          return of(null);
        }),
      )
      .subscribe((deck) => {
        if (deck === null) {
          return;
        }
        this.deck.set(deck);
        this.state.set({ kind: 'ready' });
      });

    merge(
      this.searchControl.valueChanges.pipe(debounceTime(300), distinctUntilChanged()),
      this.typeChanged.asObservable(),
    )
      .pipe(
        startWith(null),
        tap(() => this.searching.set(true)),
        switchMap(() => {
          const name = this.searchControl.value.trim();
          return this.cardsApi
            .search(name.length > 0 ? name : null, this.cardType(), 0, PAGE_SIZE)
            .pipe(
              catchError(() => {
                this.sound.playError();
                return of({ items: [], total: 0, page: 0, pageSize: PAGE_SIZE });
              }),
            );
        }),
        takeUntilDestroyed(this.destroyRef),
      )
      .subscribe((page) => {
        this.results.set(page.items);
        this.searching.set(false);
      });
  }

  protected loadError(): string {
    const state = this.state();
    return state.kind === 'error' ? state.message : '';
  }

  protected onTypeChange(type: string): void {
    this.cardType.set(type);
    this.typeChanged.next();
  }

  protected slotsLabel(card: CardDto): string {
    if (this.isBasicEnergy(card)) {
      return '∞';
    }
    const used = this.deck()?.cards.find((c) => c.cardId === card.id)?.quantity ?? 0;
    return `${Math.max(0, 4 - used)} libres`;
  }

  protected canAdd(card: CardDto): boolean {
    if (this.isBasicEnergy(card)) {
      return this.totalCards() < 60;
    }
    const used = this.deck()?.cards.find((c) => c.cardId === card.id)?.quantity ?? 0;
    return used < 4 && this.totalCards() < 60;
  }

  protected add(card: CardDto): void {
    const deck = this.deck();
    if (deck === null || !this.canAdd(card)) {
      return;
    }
    this.clearActionError();
    this.api
      .addCard(deck.id, card.id, 1)
      .pipe(catchError((response: HttpErrorResponse) => this.failAction(response)))
      .subscribe((updated) => {
        if (updated === null) {
          return;
        }
        this.deck.set(updated);
        this.validation.set(null);
        this.sound.playClick();
      });
  }

  protected changeQty(cardId: string, quantity: number): void {
    const deck = this.deck();
    if (deck === null) {
      return;
    }
    this.clearActionError();
    const request =
      quantity <= 0
        ? this.api.removeCard(deck.id, cardId)
        : this.api.setQuantity(deck.id, cardId, quantity);
    request
      .pipe(catchError((response: HttpErrorResponse) => this.failAction(response)))
      .subscribe((updated) => {
        if (updated === null) {
          return;
        }
        this.deck.set(updated);
        this.validation.set(null);
      });
  }

  protected removeCard(cardId: string): void {
    void this.changeQty(cardId, 0);
  }

  protected rename(name: string): void {
    const deck = this.deck();
    const trimmed = name.trim();
    if (deck === null || trimmed.length === 0 || trimmed === deck.name) {
      return;
    }
    this.clearActionError();
    this.api
      .rename(deck.id, trimmed)
      .pipe(catchError((response: HttpErrorResponse) => this.failAction(response)))
      .subscribe((updated) => {
        if (updated !== null) {
          this.deck.set(updated);
        }
      });
  }

  protected validate(): void {
    const deck = this.deck();
    if (deck === null) {
      return;
    }
    this.clearActionError();
    this.api
      .validate(deck.id)
      .pipe(catchError((response: HttpErrorResponse) => this.failAction(response)))
      .subscribe((result) => {
        if (result === null) {
          return;
        }
        this.validation.set(result);
        this.deck.update((current) =>
          current === null ? current : { ...current, isValid: result.valid },
        );
      });
  }

  private isBasicEnergy(card: CardDto): boolean {
    return card.supertype === 'Energy' && card.specialEnergy === null;
  }

  private clearActionError(): void {
    this.actionError.set(null);
  }

  private failAction(response: HttpErrorResponse) {
    const error = toApiError(response);
    this.actionError.set(error.message);
    this.sound.playError();
    return of(null);
  }
}
