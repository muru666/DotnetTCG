import { Injectable, inject } from '@angular/core';
import { BehaviorSubject, Subject } from 'rxjs';
import * as signalR from '@microsoft/signalr';
import { environment } from '../../../../environments/environment';
import { AuthService } from '../../../core/services/auth.service';

export interface HubGameActionRequest {
  type: string;
  cardId?: string;
  target?: { zone: string; index: number };
  attackName?: string;
  energyIdsToDiscard?: string[];
  choiceId?: string;
  selectedOptions?: string[];
  abilityName?: string;
}

export interface HubActionResult {
  success: boolean;
  error?: string | null;
  pendingChoice?: unknown | null;
  events?: string[];
}

/**
 * Transporte SignalR del GameHub (/hubs/game). Reemplaza al cliente STOMP
 * artesanal: reconexión automática del framework, sin frames manuales.
 */
@Injectable({
  providedIn: 'root'
})
export class GameSignalService {
  private connection: signalR.HubConnection | null = null;
  private connectionStatusSubject = new BehaviorSubject<boolean>(false);
  private readonly authService = inject(AuthService);

  private readonly lobbySubject = new Subject<unknown>();
  private readonly gameStartedSubject = new Subject<{ gameId: string }>();
  private readonly actionResultSubject = new Subject<HubActionResult>();
  private readonly gameStateSubject = new Subject<unknown>();
  private readonly chatSubject = new Subject<{ senderId: string; text: string; at: string }>();
  private readonly hubErrorSubject = new Subject<{ code: string; message: string }>();

  connectionStatus$ = this.connectionStatusSubject.asObservable();
  lobby$ = this.lobbySubject.asObservable();
  gameStarted$ = this.gameStartedSubject.asObservable();
  actionResult$ = this.actionResultSubject.asObservable();
  gameState$ = this.gameStateSubject.asObservable();
  chat$ = this.chatSubject.asObservable();
  hubError$ = this.hubErrorSubject.asObservable();

  isConnected(): boolean {
    return this.connectionStatusSubject.value;
  }

  async connect(): Promise<void> {
    if (this.connection) {
      return;
    }
    const token = this.authService.getToken();
    if (!token) {
      throw new Error('Missing auth token');
    }
    const connection = new signalR.HubConnectionBuilder()
      .withUrl(`${environment.apiUrl}/hubs/game`, {
        accessTokenFactory: () => this.authService.getToken() ?? ''
      })
      .withAutomaticReconnect()
      .build();

    connection.on('lobby', (payload) => this.lobbySubject.next(payload));
    connection.on('game-started', (payload) => this.gameStartedSubject.next(payload));
    connection.on('action-result', (payload) => this.actionResultSubject.next(payload));
    connection.on('game-state', (payload) => this.gameStateSubject.next(payload));
    connection.on('chat', (payload) => this.chatSubject.next(payload));
    connection.on('error', (payload) => this.hubErrorSubject.next(payload));
    connection.onreconnecting(() => this.connectionStatusSubject.next(false));
    connection.onreconnected(() => this.connectionStatusSubject.next(true));
    connection.onclose(() => {
      this.connectionStatusSubject.next(false);
      if (this.connection === connection) {
        this.connection = null;
      }
    });

    this.connection = connection;
    await connection.start();
    this.connectionStatusSubject.next(true);
  }

  async disconnect(): Promise<void> {
    const connection = this.connection;
    this.connection = null;
    this.connectionStatusSubject.next(false);
    if (connection) {
      await connection.stop().catch(() => {});
    }
  }

  private requireConnection(): signalR.HubConnection {
    if (!this.connection) {
      throw new Error('Not connected to game hub');
    }
    return this.connection;
  }

  joinLobby(gameId: string, password?: string | null): Promise<void> {
    return this.requireConnection().invoke('JoinLobby', gameId, password ?? null);
  }

  leaveLobby(gameId: string): Promise<void> {
    return this.requireConnection().invoke('LeaveLobby', gameId);
  }

  selectDeck(gameId: string, deckId: string): Promise<void> {
    return this.requireConnection().invoke('SelectDeck', gameId, deckId);
  }

  setReady(gameId: string, ready: boolean): Promise<void> {
    return this.requireConnection().invoke('SetReady', gameId, ready);
  }

  startGame(gameId: string): Promise<void> {
    return this.requireConnection().invoke('StartGame', gameId);
  }

  executeAction(gameId: string, request: HubGameActionRequest): Promise<void> {
    return this.requireConnection().invoke('ExecuteAction', gameId, request);
  }

  mulligan(gameId: string): Promise<void> {
    return this.requireConnection().invoke('Mulligan', gameId);
  }

  pauseGame(gameId: string): Promise<void> {
    return this.requireConnection().invoke('PauseGame', gameId);
  }

  reconnect(gameId: string): Promise<void> {
    return this.requireConnection().invoke('Reconnect', gameId);
  }

  joinAsSpectator(gameId: string): Promise<void> {
    return this.requireConnection().invoke('JoinAsSpectator', gameId);
  }

  sendChat(gameId: string, text: string): Promise<void> {
    return this.requireConnection().invoke('SendChat', gameId, text);
  }
}
