import { Injectable, signal, computed } from '@angular/core';
import { HttpClient } from '@angular/common/http';
import { Observable, forkJoin, of, EMPTY } from 'rxjs';
import { map, tap, catchError, switchMap } from 'rxjs/operators';
import { environment } from '../../../../environments/environment';
import { extractHttpError } from '../../utils/http-error';

export interface Attack {
  name: string;
  description?: string;
  damage?: string;
  cost?: string[];
}

export interface Ability {
  name: string;
  description?: string;
  type?: string;
}

export interface Weakness {
  type?: string;
  value?: string;
}

export interface Resistance {
  type?: string;
  value?: string;
}

export interface Card {
  id: string;
  nombre: string;
  tipo: 'POKEMON' | 'ENERGY' | 'TRAINER';
  imageUrl: string;
  hp?: number;
  rarity?: string;
  subtypes?: string[];
  rules?: string[];
  setId?: string;
  evolvesFrom?: string;
  retreatCost?: number;
  energyType?: string;
  specialEnergyType?: string;
  attacks?: Attack[];
  abilities?: Ability[];
  weaknesses?: Weakness[];
  resistances?: Resistance[];
}

export interface SaveDeckCard {
  cardId: string;
  quantity: number;
}

export interface DeckCard {
  id: string;
  card: Card;
  quantity: number;
}

export interface Deck {
  id: string;
  name: string;
  isValid: boolean;
  createdAt?: string;
  version: number;
  totalCards: number;
  deckCards: DeckCard[];
}

interface BackendDeckOp {
  cardId: string;
  quantity: number;
}

interface BackendCardDto {
  id: string;
  name: string;
  supertype: string;
  subtypes: string[];
  setId: string;
  imageUrl: string;
  hp?: number | null;
  retreatCost?: number | null;
  evolvesFrom?: string | null;
  pokemonType?: string | null;
  attacks?: { name: string; description: string; damage: string; cost: string[] }[];
  abilities?: { name: string; text: string; type: string }[];
  weaknesses?: { type: string; multiplier: string }[];
  resistances?: { type: string; reduction: string }[];
  energyType?: string | null;
  specialEnergy?: string | null;
  trainerSubtype?: string | null;
}

interface BackendDeckCard {
  cardId: string;
  cardName: string;
  quantity: number;
}

interface BackendDeck {
  id: string;
  name: string;
  isValid: boolean;
  version: number;
  cards: BackendDeckCard[];
}

@Injectable({
  providedIn: 'root'
})
export class DeckBuilderService {
  private apiUrl = environment.apiUrl;

  currentDeck = signal<Deck | null>(null);
  userDecks = signal<Deck[]>([]);
  searchResults = signal<Card[]>([]);
  currentPage = signal<number>(0);
  totalPages = signal<number>(0);
  totalElements = signal<number>(0);
  searchSize = 20;
  lastSearchQuery = signal('');
  lastSearchTipo = signal<string | undefined>(undefined);
  isLoading = signal<boolean>(false);
  error = signal<string | null>(null);
  successMessage = signal<string | null>(null);

  totalCards = computed(() => {
    const deck = this.currentDeck();
    return deck?.deckCards.reduce((sum, dc) => sum + dc.quantity, 0) ?? 0;
  });
  remainingSlots = computed(() => Math.max(0, 60 - this.totalCards()));
  canAddMore = computed(() => this.remainingSlots() > 0);
  deckCards = computed(() =>
    [...(this.currentDeck()?.deckCards ?? [])].sort((a, b) =>
      a.card.nombre.localeCompare(b.card.nombre)
    )
  );

  private successTimer: ReturnType<typeof setTimeout> | null = null;
  private readonly cardCache = new Map<string, Card>();

  private showSuccess(msg: string): void {
    if (this.successTimer) clearTimeout(this.successTimer);
    this.successMessage.set(msg);
    this.successTimer = setTimeout(() => this.successMessage.set(null), 2500);
  }

  constructor(private http: HttpClient) {}
  /** Resetear el estado de búsqueda al salir de Pokédex/DeckBuilder */
  resetSearchState(): void {
    this.currentPage.set(0);
    this.totalPages.set(0);
    this.totalElements.set(0);
    this.searchResults.set([]);
    this.lastSearchQuery.set('');
    this.lastSearchTipo.set(undefined);
    this.isLoading.set(false);
    this.error.set(null);
  }

  searchCards(query: string, tipo?: string, page: number = 0): Observable<Card[]> {
    this.isLoading.set(true);
    this.error.set(null);
    this.lastSearchQuery.set(query);
    this.lastSearchTipo.set(tipo);

    let url = `${this.apiUrl}/v1/cards?page=${page}&pageSize=${this.searchSize}`;
    if (query && query.trim()) {
      url += `&name=${encodeURIComponent(query)}`;
    }
    if (tipo) {
      url += `&supertype=${encodeURIComponent(tipo)}`;
    }
    return this.http.get<{ items: BackendCardDto[]; total: number; page: number; pageSize: number }>(url).pipe(
      tap(resp => {
        const cards = resp.items.map(c => this.mapCard(c));
        this.searchResults.set(cards);
        this.totalElements.set(resp.total);
        this.totalPages.set(Math.ceil(resp.total / this.searchSize));
        this.currentPage.set(resp.page);
        this.isLoading.set(false);
      }),
      map(resp => resp.items.map(c => this.mapCard(c))),
      catchError(() => {
        this.error.set('Error al buscar cartas');
        this.isLoading.set(false);
        return of([]);
      })
    );
  }

  nextPage(query: string, tipo?: string): void {
    if (this.currentPage() < this.totalPages() - 1) {
      this.searchCards(query, tipo, this.currentPage() + 1).subscribe();
    }
  }

  prevPage(query: string, tipo?: string): void {
    if (this.currentPage() > 0) {
      this.searchCards(query, tipo, this.currentPage() - 1).subscribe();
    }
  }

  getUserDecks(): Observable<Deck[]> {
    this.isLoading.set(true);
    this.error.set(null);

    return this.http.get<BackendDeck[]>(`${this.apiUrl}/v1/decks`).pipe(
      switchMap(decks => this.enrichDecks(decks)),
      tap(decks => {
        this.userDecks.set(decks);
        this.isLoading.set(false);
      }),
      catchError(err => {
        this.error.set(extractHttpError(err, 'Error al cargar mazos'));
        this.isLoading.set(false);
        return of([]);
      })
    );
  }

  createDeck(name: string, cards: SaveDeckCard[] = []): Observable<Deck> {
    this.isLoading.set(true);
    this.error.set(null);

    return this.http.post<BackendDeck>(`${this.apiUrl}/v1/decks`, { name }).pipe(
      switchMap(deck => this.syncDeckCards(deck.id, cards)),
      switchMap(deck => this.enrichDeck(deck)),
      tap(deck => {
        this.currentDeck.set(deck);
        this.userDecks.update(decks => [...decks, deck]);
        this.isLoading.set(false);
        this.showSuccess('Mazo creado');
      }),
      catchError(err => {
        this.error.set(extractHttpError(err, 'Error al crear mazo'));
        this.isLoading.set(false);
        return of(null as unknown as Deck);
      })
    );
  }

  loadDeck(deckId: string): Observable<Deck> {
    this.isLoading.set(true);
    this.error.set(null);

    return this.http.get<BackendDeck>(`${this.apiUrl}/v1/decks/${deckId}`).pipe(
      switchMap(deck => this.enrichDeck(deck)),
      tap(deck => {
        this.currentDeck.set(deck);
        this.isLoading.set(false);
      }),
      catchError(err => {
        this.error.set(extractHttpError(err, 'Error al cargar mazo'));
        this.isLoading.set(false);
        return of(null as unknown as Deck);
      })
    );
  }

  addCardToDeck(deckId: string, cardId: string, quantity: number = 1): Observable<Deck> {
    this.error.set(null);

    return this.http.post<BackendDeck>(`${this.apiUrl}/v1/decks/${deckId}/cards`, { cardId, quantity }).pipe(
      switchMap(deck => this.enrichDeck(deck)),
      tap(deck => {
        this.currentDeck.set(deck);
      }),
      catchError(err => {
        const msg = extractHttpError(err, 'Error al agregar carta');
        this.error.set(msg);
        return of(null as unknown as Deck);
      })
    );
  }

  removeCardFromDeck(deckId: string, cardId: string): Observable<Deck> {
    this.error.set(null);

    return this.http.delete<BackendDeck>(`${this.apiUrl}/v1/decks/${deckId}/cards/${cardId}`).pipe(
      switchMap(deck => this.enrichDeck(deck)),
      tap(deck => {
        this.currentDeck.set(deck);
      }),
      catchError(err => {
        this.error.set(extractHttpError(err, 'Error al quitar carta'));
        return of(null as unknown as Deck);
      })
    );
  }

  updateCardQuantity(deckId: string, cardId: string, quantity: number): Observable<Deck> {
    this.error.set(null);

    return this.http.put<BackendDeck>(`${this.apiUrl}/v1/decks/${deckId}/cards/${cardId}`, { quantity }).pipe(
      switchMap(deck => this.enrichDeck(deck)),
      tap(deck => {
        this.currentDeck.set(deck);
      }),
      catchError(err => {
        this.error.set(extractHttpError(err, 'Error al actualizar cantidad'));
        return of(null as unknown as Deck);
      })
    );
  }

  saveDeck(deckId: string, name: string, cards: SaveDeckCard[] = []): Observable<Deck> {
    this.error.set(null);

    return this.http.put<BackendDeck>(`${this.apiUrl}/v1/decks/${deckId}`, { name }).pipe(
      switchMap(() => this.syncDeckCards(deckId, cards)),
      switchMap(deck => this.enrichDeck(deck)),
      tap(deck => {
        this.currentDeck.set(deck);
        this.userDecks.update(decks => decks.map(d => d.id === deck.id ? deck : d));
        this.showSuccess('Mazo guardado');
      }),
      catchError(err => {
        this.error.set(extractHttpError(err, 'Error al guardar mazo'));
        return of(null as unknown as Deck);
      })
    );
  }

  deleteDeck(deckId: string): Observable<void> {
    return this.http.delete<void>(`${this.apiUrl}/v1/decks/${deckId}`).pipe(
      tap(() => {
        if (this.currentDeck()?.id === deckId) {
          this.currentDeck.set(null);
        }
        this.userDecks.update(decks => decks.filter(d => d.id !== deckId));
        this.error.set(null);
      }),
      catchError(err => {
        this.error.set(extractHttpError(err, 'Error al eliminar mazo'));
        return EMPTY;
      })
    );
  }

  /**
   * Lleva las cartas del servidor al estado del borrador: altas, cambios de
   * cantidad y bajas. Devuelve el mazo fresco del servidor.
   */
  private syncDeckCards(deckId: string, cards: SaveDeckCard[]): Observable<BackendDeck> {
    const base = `${this.apiUrl}/v1/decks/${deckId}`;
    return this.http.get<BackendDeck>(base).pipe(
      switchMap(server => {
        const serverQty = new Map(server.cards.map(c => [c.cardId, c.quantity] as const));
        const draftQty = new Map(cards.map(c => [c.cardId, c.quantity] as const));
        const ops: Observable<unknown>[] = [];
        for (const [cardId, quantity] of draftQty) {
          if (!serverQty.has(cardId)) {
            ops.push(this.http.post(`${base}/cards`, { cardId, quantity }));
          } else if (serverQty.get(cardId) !== quantity) {
            ops.push(this.http.put(`${base}/cards/${cardId}`, { quantity }));
          }
        }
        for (const cardId of serverQty.keys()) {
          if (!draftQty.has(cardId)) {
            ops.push(this.http.delete(`${base}/cards/${cardId}`));
          }
        }
        if (ops.length === 0) return of(server);
        return forkJoin(ops).pipe(switchMap(() => this.http.get<BackendDeck>(base)));
      })
    );
  }

  private enrichDecks(decks: BackendDeck[]): Observable<Deck[]> {
    if (decks.length === 0) return of([]);
    return forkJoin(decks.map(d => this.enrichDeck(d)));
  }

  private enrichDeck(deck: BackendDeck): Observable<Deck> {
    const ids = [...new Set(deck.cards.map(c => c.cardId))];
    if (ids.length === 0) {
      return of(this.toDeck(deck, new Map()));
    }
    const missing = ids.filter(id => !this.cardCache.has(id));
    if (missing.length === 0) {
      return of(this.toDeck(deck, this.cardCache));
    }
    return forkJoin(
      missing.map(id =>
        this.http.get<BackendCardDto>(`${this.apiUrl}/v1/cards/${id}`).pipe(
          catchError(() => of(null))
        )
      )
    ).pipe(
      map(details => {
        for (const dto of details) {
          if (dto) this.cardCache.set(dto.id, this.mapCard(dto));
        }
        return this.toDeck(deck, this.cardCache);
      })
    );
  }

  private toDeck(deck: BackendDeck, cache: Map<string, Card>): Deck {
    const deckCards: DeckCard[] = deck.cards.map(c => {
      const cached = cache.get(c.cardId);
      const card: Card = cached ?? {
        id: c.cardId,
        nombre: c.cardName,
        tipo: 'POKEMON',
        imageUrl: '',
      };
      return { id: c.cardId, card, quantity: c.quantity };
    });
    return {
      id: deck.id,
      name: deck.name,
      isValid: deck.isValid,
      version: deck.version,
      totalCards: deckCards.reduce((sum, dc) => sum + dc.quantity, 0),
      deckCards,
    };
  }

  private mapCard(dto: BackendCardDto): Card {
    const card: Card = {
      id: dto.id,
      nombre: dto.name,
      tipo: dto.supertype.toUpperCase() as Card['tipo'],
      imageUrl: dto.imageUrl,
      hp: dto.hp ?? undefined,
      subtypes: dto.subtypes ?? [],
      rules: [],
      setId: dto.setId,
      evolvesFrom: dto.evolvesFrom ?? undefined,
      retreatCost: dto.retreatCost ?? undefined,
      energyType: dto.energyType?.toUpperCase(),
      specialEnergyType: dto.specialEnergy?.toUpperCase(),
      attacks: (dto.attacks ?? []).map(a => ({
        name: a.name,
        description: a.description,
        damage: a.damage,
        cost: (a.cost ?? []).map(c => c.toUpperCase()),
      })),
      abilities: (dto.abilities ?? []).map(a => ({
        name: a.name,
        description: a.text,
        type: a.type?.toUpperCase(),
      })),
      weaknesses: (dto.weaknesses ?? []).map(w => ({
        type: w.type?.toUpperCase(),
        value: w.multiplier,
      })),
      resistances: (dto.resistances ?? []).map(r => ({
        type: r.type?.toUpperCase(),
        value: r.reduction,
      })),
    };
    this.cardCache.set(card.id, card);
    return card;
  }
}
