import { HttpClient, HttpParams } from '@angular/common/http';
import { inject, Injectable } from '@angular/core';
import { Observable } from 'rxjs';
import { API_URL } from '../../../core/http/api-url.token';
import { CardDto, PagedResult } from '../models/deck.models';

@Injectable({ providedIn: 'root' })
export class CardsApi {
  private readonly http = inject(HttpClient);
  private readonly baseUrl = inject(API_URL);

  search(name: string | null, type: string | null, page: number, pageSize: number): Observable<PagedResult<CardDto>> {
    let params = new HttpParams().set('page', page).set('pageSize', pageSize);
    if (name !== null && name.length > 0) {
      params = params.set('name', name);
    }
    if (type !== null && type !== 'all') {
      params = params.set('type', type);
    }
    return this.http.get<PagedResult<CardDto>>(`${this.baseUrl}/v1/cards`, { params });
  }
}
