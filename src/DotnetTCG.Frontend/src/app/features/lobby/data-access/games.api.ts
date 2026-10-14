import { HttpClient } from '@angular/common/http';
import { inject, Injectable } from '@angular/core';
import { Observable } from 'rxjs';
import { API_URL } from '../../../core/http/api-url.token';
import { LobbyDto } from '../models/lobby.models';

export interface CreateGameBody {
  readonly isPublic: boolean;
  readonly acceptsSpectators: boolean;
  readonly password?: string;
}

@Injectable({ providedIn: 'root' })
export class GamesApi {
  private readonly http = inject(HttpClient);
  private readonly baseUrl = inject(API_URL);

  listOpen(): Observable<LobbyDto[]> {
    return this.http.get<LobbyDto[]>(`${this.baseUrl}/v1/games`);
  }

  create(body: CreateGameBody): Observable<LobbyDto> {
    return this.http.post<LobbyDto>(`${this.baseUrl}/v1/games`, body);
  }
}
