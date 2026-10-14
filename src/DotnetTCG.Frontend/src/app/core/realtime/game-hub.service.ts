import { inject, Injectable, signal } from '@angular/core';
import { HubConnection, HubConnectionBuilder, HubConnectionState } from '@microsoft/signalr';
import { Observable } from 'rxjs';
import { API_URL } from '../http/api-url.token';
import { SessionStore } from '../auth/session';

export type GameHubState = 'disconnected' | 'connecting' | 'connected' | 'reconnecting';

/**
 * Conexión SignalR al GameHub. Una sola conexión por app; los componentes
 * se suscriben a eventos y la liberan al destruirse. Reconexión automática.
 */
@Injectable({ providedIn: 'root' })
export class GameHubService {
  private readonly baseUrl = inject(API_URL);
  private readonly session = inject(SessionStore);
  private connection: HubConnection | null = null;

  readonly state = signal<GameHubState>('disconnected');

  async ensureConnected(): Promise<HubConnection> {
    const existing = this.connection;
    if (existing !== null && existing.state === HubConnectionState.Connected) {
      return existing;
    }
    return this.connect();
  }

  on<T>(event: string, handler: (payload: T) => void): () => void {
    const connection = this.connection;
    if (connection === null) {
      return () => undefined;
    }
    connection.on(event, handler);
    return () => connection.off(event, handler);
  }

  async invoke<T>(method: string, ...args: unknown[]): Promise<T> {
    const connection = await this.ensureConnected();
    return connection.invoke<T>(method, ...args);
  }

  async disconnect(): Promise<void> {
    const connection = this.connection;
    this.connection = null;
    this.state.set('disconnected');
    if (connection !== null) {
      await connection.stop();
    }
  }

  private async connect(): Promise<HubConnection> {
    await this.disconnect();
    this.state.set('connecting');
    const connection = new HubConnectionBuilder()
      .withUrl(`${this.baseUrl}/hubs/game`, {
        accessTokenFactory: () => this.session.token() ?? '',
      })
      .withAutomaticReconnect()
      .build();
    connection.onreconnecting(() => this.state.set('reconnecting'));
    connection.onreconnected(() => this.state.set('connected'));
    connection.onclose(() => {
      if (this.connection === connection) {
        this.connection = null;
        this.state.set('disconnected');
      }
    });
    this.connection = connection;
    await connection.start();
    this.state.set('connected');
    return connection;
  }
}
