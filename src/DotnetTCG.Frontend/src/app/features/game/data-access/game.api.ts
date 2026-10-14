import { HttpClient } from '@angular/common/http';
import { inject, Injectable } from '@angular/core';
import { Observable } from 'rxjs';
import { API_URL } from '../../../core/http/api-url.token';
import { GameLogEntryDto, GameStateViewDto } from '../models/game.models';

@Injectable()
export class GameApi {
  private readonly http = inject(HttpClient);
  private readonly baseUrl = inject(API_URL);

  state(gameId: string): Observable<GameStateViewDto> {
    return this.http.get<GameStateViewDto>(`${this.baseUrl}/v1/games/${gameId}/state`);
  }

  logs(gameId: string): Observable<GameLogEntryDto[]> {
    return this.http.get<GameLogEntryDto[]>(`${this.baseUrl}/v1/games/${gameId}/logs`);
  }
}
