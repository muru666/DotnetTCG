import { HttpClient } from '@angular/common/http';
import { inject, Injectable } from '@angular/core';
import { Observable } from 'rxjs';
import { API_URL } from '../../../core/http/api-url.token';
import { DeckDto, DeckValidation } from '../models/deck.models';

@Injectable({ providedIn: 'root' })
export class DecksApi {
  private readonly http = inject(HttpClient);
  private readonly baseUrl = inject(API_URL);

  list(): Observable<DeckDto[]> {
    return this.http.get<DeckDto[]>(`${this.baseUrl}/v1/decks`);
  }

  create(name: string): Observable<DeckDto> {
    return this.http.post<DeckDto>(`${this.baseUrl}/v1/decks`, { name });
  }

  getById(id: string): Observable<DeckDto> {
    return this.http.get<DeckDto>(`${this.baseUrl}/v1/decks/${id}`);
  }

  rename(id: string, name: string): Observable<DeckDto> {
    return this.http.put<DeckDto>(`${this.baseUrl}/v1/decks/${id}`, { name });
  }

  delete(id: string): Observable<void> {
    return this.http.delete<void>(`${this.baseUrl}/v1/decks/${id}`);
  }

  addCard(id: string, cardId: string, quantity: number): Observable<DeckDto> {
    return this.http.post<DeckDto>(`${this.baseUrl}/v1/decks/${id}/cards`, {
      cardId,
      quantity,
    });
  }

  removeCard(id: string, cardId: string): Observable<DeckDto> {
    return this.http.delete<DeckDto>(`${this.baseUrl}/v1/decks/${id}/cards/${cardId}`);
  }

  setQuantity(id: string, cardId: string, quantity: number): Observable<DeckDto> {
    return this.http.put<DeckDto>(`${this.baseUrl}/v1/decks/${id}/cards/${cardId}`, {
      quantity,
    });
  }

  validate(id: string): Observable<DeckValidation> {
    return this.http.post<DeckValidation>(`${this.baseUrl}/v1/decks/${id}/validate`, {});
  }
}
