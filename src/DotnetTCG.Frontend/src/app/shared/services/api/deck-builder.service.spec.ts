import { TestBed } from '@angular/core/testing';
import { HttpTestingController, provideHttpClientTesting } from '@angular/common/http/testing';
import { provideHttpClient } from '@angular/common/http';
import { DeckBuilderService, Deck } from './deck-builder.service';
import { environment } from '../../../../environments/environment';

const API = environment.apiUrl;

function backendDeck(cards: { cardId: string; quantity: number }[]): object {
  return { id: 'deck-1', name: 'Mazo', isValid: true, version: 1, cards };
}

function detailCard(id: string): object {
  return { id, name: id, supertype: 'Pokemon', subtypes: ['Basic'], setId: 'xy1', imageUrl: 'img' };
}

describe('DeckBuilderService', () => {
  let service: DeckBuilderService;
  let httpMock: HttpTestingController;

  beforeEach(() => {
    TestBed.configureTestingModule({
      providers: [provideHttpClient(), provideHttpClientTesting()],
    });
    service = TestBed.inject(DeckBuilderService);
    httpMock = TestBed.inject(HttpTestingController);
  });

  afterEach(() => httpMock.verify());

  it('searchCards usa supertype (no el filtro por elemento)', () => {
    service.searchCards('pika', 'POKEMON', 2).subscribe();

    const req = httpMock.expectOne(r => r.url.startsWith(`${API}/v1/cards`));
    expect(req.request.url).toContain('name=pika');
    expect(req.request.url).toContain('supertype=POKEMON');
    expect(req.request.url).toContain('page=2');
    // No debe mandar el filtro por tipo de elemento (type=); solo supertype=.
    expect(req.request.url).not.toMatch(/[?&]type=/);
    req.flush({ items: [], total: 0, page: 2, pageSize: 12 });
  });

  it('searchCards sin tipo no manda filtro', () => {
    service.searchCards('').subscribe();

    const req = httpMock.expectOne(r => r.url.startsWith(`${API}/v1/cards`));
    expect(req.request.url).not.toContain('supertype=');
    expect(req.request.url).not.toContain('type=');
    req.flush({ items: [], total: 0, page: 0, pageSize: 12 });
  });

  it('saveDeck sincroniza altas, bajas y cambios de cantidad', () => {
    let saved: Deck | null = null;
    service.saveDeck('deck-1', 'Mazo', [
      { cardId: 'pikachu', quantity: 4 },   // ya existía igual → no-op
      { cardId: 'raichu', quantity: 2 },    // cantidad cambió → PUT
      { cardId: 'charmander', quantity: 3 }, // nueva → POST
    ]).subscribe(d => (saved = d));

    // rename
    httpMock.expectOne(`${API}/v1/decks/deck-1`).flush(backendDeck([
      { cardId: 'pikachu', quantity: 4 },
      { cardId: 'raichu', quantity: 1 },
      { cardId: 'bulbasaur', quantity: 2 }, // se elimina → DELETE
    ]));

    // estado actual del servidor (base del diff)
    httpMock.expectOne(`${API}/v1/decks/deck-1`).flush(backendDeck([
      { cardId: 'pikachu', quantity: 4 },
      { cardId: 'raichu', quantity: 1 },
      { cardId: 'bulbasaur', quantity: 2 },
    ]));

    // diff en orden secuencial: PUT (cantidad), POST (alta) y DELETE (baja)
    const put = httpMock.expectOne(`${API}/v1/decks/deck-1/cards/raichu`);
    expect(put.request.method).toBe('PUT');
    expect(put.request.body).toEqual({ quantity: 2 });
    put.flush(null);

    const post = httpMock.expectOne(`${API}/v1/decks/deck-1/cards`);
    expect(post.request.method).toBe('POST');
    expect(post.request.body).toEqual({ cardId: 'charmander', quantity: 3 });
    post.flush(null);

    const del = httpMock.expectOne(`${API}/v1/decks/deck-1/cards/bulbasaur`);
    expect(del.request.method).toBe('DELETE');
    del.flush(null);

    // estado fresco tras el sync
    httpMock.expectOne(`${API}/v1/decks/deck-1`).flush(backendDeck([
      { cardId: 'pikachu', quantity: 4 },
      { cardId: 'raichu', quantity: 2 },
      { cardId: 'charmander', quantity: 3 },
    ]));

    // enrichDeck resuelve el detalle de cada carta
    for (const id of ['pikachu', 'raichu', 'charmander']) {
      httpMock.expectOne(`${API}/v1/cards/${id}`).flush(detailCard(id));
    }

    expect(saved).not.toBeNull();
    expect(saved!.deckCards.map(c => `${c.card.id}x${c.quantity}`).sort())
      .toEqual(['charmanderx3', 'pikachux4', 'raichux2']);
  });

  it('saveDeck sin cambios de cartas no llama a los endpoints de tarjetas', () => {
    service.saveDeck('deck-1', 'Mazo', [{ cardId: 'pikachu', quantity: 4 }]).subscribe();

    // rename
    httpMock.expectOne(`${API}/v1/decks/deck-1`).flush(backendDeck([{ cardId: 'pikachu', quantity: 4 }]));
    // sync base: coincide, no genera ops
    httpMock.expectOne(`${API}/v1/decks/deck-1`).flush(backendDeck([{ cardId: 'pikachu', quantity: 4 }]));
    // solo el enriquecimiento de la carta, sin POST/PUT/DELETE de sincronización
    httpMock.expectOne(`${API}/v1/cards/pikachu`).flush(detailCard('pikachu'));

    // cualquier petición extra a /cards haría fallar httpMock.verify()
  });

  it('saveDeck ejecuta las mutaciones en secuencia (una en vuelo a la vez)', () => {
    // FE-005: con forkJoin las N mutaciones volaban juntas sobre endpoints
    // read-modify-write y se pisaban entre sí.
    let saved: Deck | null = null;
    service.saveDeck('deck-1', 'Mazo', [
      { cardId: 'raichu', quantity: 2 },      // PUT
      { cardId: 'charmander', quantity: 3 },  // POST
    ]).subscribe(d => (saved = d));

    httpMock.expectOne(`${API}/v1/decks/deck-1`).flush(backendDeck([
      { cardId: 'raichu', quantity: 1 },
    ]));
    httpMock.expectOne(`${API}/v1/decks/deck-1`).flush(backendDeck([
      { cardId: 'raichu', quantity: 1 },
    ]));

    // Solo UNA mutación en vuelo: con forkJoin habría dos pendientes acá.
    let pending = httpMock.match(() => true);
    expect(pending.length).toBe(1);
    expect(pending[0].request.method).toBe('PUT');
    pending[0].flush(null);

    pending = httpMock.match(() => true);
    expect(pending.length).toBe(1);
    expect(pending[0].request.method).toBe('POST');
    pending[0].flush(null);

    httpMock.expectOne(`${API}/v1/decks/deck-1`).flush(backendDeck([
      { cardId: 'raichu', quantity: 2 },
      { cardId: 'charmander', quantity: 3 },
    ]));
    httpMock.expectOne(`${API}/v1/cards/raichu`).flush(detailCard('raichu'));
    httpMock.expectOne(`${API}/v1/cards/charmander`).flush(detailCard('charmander'));

    expect(saved).not.toBeNull();
  });
});
