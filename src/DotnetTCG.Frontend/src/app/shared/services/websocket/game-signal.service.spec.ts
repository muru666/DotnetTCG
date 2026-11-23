import { TestBed } from '@angular/core/testing';
import { vi } from 'vitest';
import { GameSignalService } from './game-signal.service';
import { AuthService } from '../../../core/services/auth.service';

// Hub falso: los spies se crean con vi.hoisted porque vi.mock se eleva arriba
// de los imports.
const hub = vi.hoisted(() => ({
  start: vi.fn(() => Promise.resolve()),
  stop: vi.fn(() => Promise.resolve()),
  invoke: vi.fn(() => Promise.resolve()),
  on: vi.fn(),
  onreconnecting: vi.fn(),
  onreconnected: vi.fn(),
  onclose: vi.fn(),
  built: 0,
}));

vi.mock('@microsoft/signalr', () => {
  class FakeConnection {
    on = hub.on;
    onreconnecting = hub.onreconnecting;
    onreconnected = hub.onreconnected;
    onclose = hub.onclose;
    start = hub.start;
    stop = hub.stop;
    invoke = hub.invoke;
  }

  class FakeBuilder {
    withUrl() {
      return this;
    }
    withAutomaticReconnect() {
      return this;
    }
    build() {
      hub.built++;
      return new FakeConnection();
    }
  }

  return { HubConnectionBuilder: FakeBuilder };
});

describe('GameSignalService', () => {
  let service: GameSignalService;

  beforeEach(() => {
    hub.start.mockClear();
    hub.invoke.mockClear();
    hub.built = 0;
    TestBed.configureTestingModule({
      providers: [
        { provide: AuthService, useValue: { getToken: () => 'token', getUserId: () => 'u1' } },
      ],
    });
    service = TestBed.inject(GameSignalService);
  });

  it('sin conexión, los métodos rechazan en vez de lanzar síncrono', async () => {
    // Regresión FE-002: el throw síncrono escapaba del .catch del llamador y, en
    // GameService.executeAction, dejaba isLoading en true (overlay eterno).
    await expect(service.joinLobby('g1')).rejects.toThrow('Not connected');
    await expect(service.executeAction('g1', { type: 'Attack' })).rejects.toThrow('Not connected');
    await expect(service.leaveLobby('g1')).rejects.toThrow('Not connected');
    expect(hub.invoke).not.toHaveBeenCalled();
  });

  it('reconnect reconecta primero cuando la conexión murió', async () => {
    // Regresión FE-002: el botón "Reconectar" no volvía a llamar a connect().
    await service.reconnect('g1');

    expect(hub.start).toHaveBeenCalledTimes(1);
    expect(hub.invoke).toHaveBeenCalledWith('Reconnect', 'g1');
  });

  it('reconnect reutiliza la conexión existente sin reconectar', async () => {
    await service.connect();
    hub.start.mockClear();

    await service.reconnect('g1');

    expect(hub.start).not.toHaveBeenCalled();
    expect(hub.invoke).toHaveBeenCalledWith('Reconnect', 'g1');
  });

  // Nota: FE-010 (un connect() que falla deja this.connection seteado) sigue
  // abierto en docs/AUDIT-2026-11.md; queda fuera del alcance de este fix.
});
