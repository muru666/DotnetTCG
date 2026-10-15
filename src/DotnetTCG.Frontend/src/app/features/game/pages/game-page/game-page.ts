import { ChangeDetectionStrategy, Component, OnInit, OnDestroy, inject, signal, computed, effect } from '@angular/core';
import { Subscription } from 'rxjs';
import { NgOptimizedImage } from '@angular/common';
import { Router, ActivatedRoute } from '@angular/router';
import { CdkDropListGroup, CdkDropList, CdkDragDrop } from '@angular/cdk/drag-drop';
import { GameService } from '../../../../shared/services/api/game.service';
import { GameSignalService } from '../../../../shared/services/websocket/game-signal.service';
import { AuthService } from '../../../../core/services/auth.service';
import { ChatMessage } from '../../../../shared/models/chat/chat-message.model';
import { GameHistoryEvent } from '../../../../shared/models/ui/game-history-event.model';
import { SidePanelState } from '../../../../shared/models/ui/side-panel.model';
import { Card } from '../../../../shared/models/dtos/game-state.model';
import { GameEventDTO } from '../../../../shared/models/dtos/action-result.model';
import { PlayerZoneComponent } from '../../components/player-zone/player-zone';
import { SidePanelComponent } from '../../components/side-panel/side-panel';
import { SetupPhaseComponent } from '../../components/setup-phase/setup-phase';
import { CardComponent } from '../../components/card/card';
import { ChoiceDialogComponent } from '../../components/choice-dialog/choice-dialog';
import { CoinFlipComponent } from '../../components/coin-flip/coin-flip';

interface Toast {
  id: string;
  message: string;
  type: string;
  timestamp: string;
}

@Component({
  selector: 'app-game-page',
  imports: [
    NgOptimizedImage,
    PlayerZoneComponent, SidePanelComponent, SetupPhaseComponent,
    CardComponent, ChoiceDialogComponent, CoinFlipComponent,
    CdkDropListGroup, CdkDropList
  ],
  templateUrl: './game-page.html',
  styleUrl: './game-page.css',
  changeDetection: ChangeDetectionStrategy.OnPush
})
export class GamePageComponent implements OnInit, OnDestroy {
  private readonly route = inject(ActivatedRoute);
  private readonly router = inject(Router);
  protected readonly authService = inject(AuthService);
  protected readonly gameService = inject(GameService);
  private readonly gameSignalService = inject(GameSignalService);

  protected readonly loadingError = signal(false);
  protected readonly selectedCardIndex = signal<number | null>(null);

  protected readonly sidePanel = signal<SidePanelState>({
    isOpen: false,
    activeTab: 'chat',
    newMessageCount: 0
  });

  protected readonly gameHistory = signal<GameHistoryEvent[]>([]);
  protected readonly showSurrenderConfirm = signal(false);

  protected readonly isDisconnected = signal(!this.gameSignalService.isConnected());
  protected readonly showCoinFlip = signal(false);
  protected readonly previewImageUrl = signal<string | null>(null);
  protected readonly toasts = signal<Toast[]>([]);
  private readonly dismissTimers = new Map<string, ReturnType<typeof setTimeout>>();
  private readonly MAX_TOASTS = 5;
  private readonly previousStatus = signal<string | null>(null);
  private connectionSub: Subscription | null = null;
  private lastChatMessageCount = 0;

  protected readonly isSetup = computed(() => {
    return this.gameService.gameState()?.status === 'SETUP';
  });

  protected readonly isPaused = computed(() => {
    return this.gameService.gameState()?.status === 'PAUSED';
  });

  protected readonly isGameOver = computed(() => {
    return this.gameService.gameState()?.status === 'FINISHED';
  });

  protected readonly isSpectator = computed(() => {
    return this.gameService.gameState()?.spectator === true;
  });

  protected readonly gameWinnerName = computed(() => {
    const state = this.gameService.gameState();
    if (!state?.winner) return null;
    const userId = this.authService.getUserId();
    return state.winner === userId ? this.playerName() : this.opponentName();
  });

  protected readonly didPlayerWin = computed(() => {
    const state = this.gameService.gameState();
    return state?.winner === this.authService.getUserId();
  });

  constructor() {
    effect((onCleanup) => {
      const state = this.gameService.gameState();
      if (state?.status === 'PAUSED') {
        const intervalId = setInterval(() => {
          const id = this.gameService.gameId();
          if (id) this.gameSignalService.reconnect(id).catch(() => {});
        }, 3000);
        onCleanup(() => clearInterval(intervalId));
      }
    });

    effect(() => {
      const state = this.gameService.gameState();
      const prev = this.previousStatus();
      if (state?.status === 'SETUP' && prev !== 'SETUP') {
        this.showCoinFlip.set(true);
      } else if (state?.status && state.status !== 'SETUP') {
        this.showCoinFlip.set(false);
      }
      if (state?.status) {
        this.previousStatus.set(state.status);
      }
    });
  


    effect(() => {
      const events = this.gameService.gameEvents();
      for (const event of events) {
        this.showToast(event);
      }
    });

    effect(() => {
      const msgs = this.gameService.chatMessages();
      const count = msgs.length;
      if (count > this.lastChatMessageCount) {
        const panel = this.sidePanel();
        if (!panel.isOpen || panel.activeTab !== 'chat') {
          this.sidePanel.update(s => ({ ...s, newMessageCount: s.newMessageCount + (count - this.lastChatMessageCount) }));
        }
      }
      this.lastChatMessageCount = count;
    });

    this.connectionSub = this.gameSignalService.connectionStatus$.subscribe(connected => {
      this.isDisconnected.set(!connected);
    });
  }

  ngOnDestroy(): void {
    this.connectionSub?.unsubscribe();
    this.gameService.disconnect();
  }

  protected onReconnect(): void {
    const gameId = this.gameService.gameId();
    if (!gameId) return;
    this.gameSignalService.reconnect(gameId).catch(() => {});
  }

  protected readonly evolvableSlots = computed<number[]>(() => {
    const state = this.gameService.gameState();
    const idx = this.selectedCardIndex();
    if (idx === null || !state?.myBoard) return [];
    const hand = state.myBoard.hand;
    if (!Array.isArray(hand)) return [];
    const card = hand[idx];
    if (!card || !card.evolvesFrom) return [];
    const evolvesFrom = card.evolvesFrom;
    const slots: number[] = [];
    if (state.myBoard.activePokemon?.pokemonCard?.name === evolvesFrom) {
      slots.push(-1);
    }
    state.myBoard.bench.forEach((poke, i) => {
      if (poke.pokemonCard?.name === evolvesFrom) slots.push(i);
    });
    return slots;
  });

  ngOnInit(): void {
    const gameId = this.route.snapshot.paramMap.get('gameId');
    if (!gameId) { this.router.navigate(['/lobby']); return; }

    this.gameService.loadGame(gameId).subscribe({
      next: state => {
        if (state.status === 'IN_LOBBY') {
          this.router.navigate(['/lobby', gameId]);
        } else {
          this.loadHistory(gameId);
        }
      },
      error: () => this.loadingError.set(true)
    });
  }

  private loadHistory(gameId: string): void {
    this.gameService.getGameLogs(gameId).subscribe({
      next: logs => {
        const events: GameHistoryEvent[] = logs.map((log, i) => ({
          id: `log-${i}`,
          turn: log.turnNumber,
          action: `${log.actionType}: ${log.action}`,
          player: log.actingPlayerUsername ?? '',
          timestamp: new Date(log.createdAt)
        }));
        this.gameHistory.set(events);
      },
      error: () => console.warn('Could not load game history')
    });
  }

  protected goBack(): void {
    this.router.navigate(['/lobby']);
  }

  protected opponentName(): string {
    const state = this.gameService.gameState();
    if (!state?.playerNames || !state?.players) return 'Oponente';
    const myId = this.authService.getUserId();
    const opponentId = state.players.find(id => id !== myId);
    return opponentId ? (state.playerNames[opponentId] ?? 'Oponente') : 'Oponente';
  }

  protected playerName(): string {
    return this.authService.getUsername() ?? 'Tú';
  }

  protected playerHand(): Card[] {
    const state = this.gameService.gameState();
    const hand = state?.myBoard?.hand;
    return Array.isArray(hand) ? hand : [];
  }

  protected opponentHandCount(): number {
    const hand = this.gameService.gameState()?.opponentBoard?.hand;
    if (typeof hand === 'number') return hand;
    return Array.isArray(hand) ? hand.length : 0;
  }

  protected opponentHandArray(): number[] {
    return new Array(Math.min(this.opponentHandCount(), 7));
  }

  protected onHandCardDrop(event: CdkDragDrop<string>): void {
    // Intencionalmente vacio: las cartas no vuelven a la mano por arrastre.
    void event;
  }

  protected onCardClick(index: number): void {
    if (this.isSpectator()) return;
    this.selectedCardIndex.set(this.selectedCardIndex() === index ? null : index);
  }

  protected onPlaceCard(event: { targetIndex: number }): void {
    if (this.isSpectator()) return;
    const idx = this.selectedCardIndex();
    if (idx === null) return;
    this.playCardByType(idx, event.targetIndex);
    this.selectedCardIndex.set(null);
  }

  protected onActiveClick(): void {
    if (this.isSpectator()) return;
    const idx = this.selectedCardIndex();
    if (idx === null) return;
    this.playCardByType(idx, -1);
    this.selectedCardIndex.set(null);
  }

  private playCardByType(cardIndex: number, targetIndex?: number): void {
    const state = this.gameService.gameState();
    const hand = state?.myBoard?.hand;
    if (!Array.isArray(hand)) return;
    const card = hand[cardIndex];
    if (!card) return;

    if (card.supertype === 'ENERGY') {
      this.gameService.attachEnergy(cardIndex, targetIndex ?? -1).subscribe();
    } else if (card.supertype === 'TRAINER' && card.trainerSubtype === 'Tool') {
      if (targetIndex === undefined) return;
      this.gameService.playCard(cardIndex, targetIndex).subscribe();
    } else if (card.supertype === 'TRAINER') {
      this.gameService.playCard(cardIndex, targetIndex ?? -1).subscribe();
    } else {
      const evolvesFrom = card.evolvesFrom;
      if (evolvesFrom && targetIndex !== undefined) {
        const target = targetIndex === -1
          ? state?.myBoard?.activePokemon
          : state?.myBoard?.bench?.[targetIndex];
        if (target?.pokemonCard?.name === evolvesFrom) {
          this.gameService.evolve(targetIndex, cardIndex).subscribe();
          return;
        }
      }
      this.gameService.playCard(cardIndex, targetIndex).subscribe();
    }
  }

  private parseTargetIndex(targetSlot: string): number | undefined {
    if (targetSlot.startsWith('active')) return -1;
    const parts = targetSlot.split('-');
    const last = parts[parts.length - 1];
    const idx = parseInt(last, 10);
    return isNaN(idx) ? undefined : idx;
  }

  protected onAttack(attackName: string): void {
    if (this.isSpectator()) return;
    this.gameService.attack(attackName).subscribe();
  }

  protected onAbility(event: { abilityName: string; pokemonId: string }): void {
    if (this.isSpectator()) return;
    this.gameService.useAbility(event.abilityName, event.pokemonId).subscribe();
  }

  protected onRetreat(benchPokemonIndex: number): void {
    if (this.isSpectator()) return;
    this.gameService.retreat(benchPokemonIndex).subscribe();
  }

  protected onEndTurn(): void {
    if (this.isSpectator()) return;
    this.gameService.endTurn().subscribe();
  }

  protected onSendMessage(msg: ChatMessage): void {
    this.gameService.sendChatMessage(msg.content, msg.senderName ?? 'Tú');
  }

  protected onSurrenderClick(): void {
    if (this.isSpectator()) return;
    this.showSurrenderConfirm.set(true);
  }

  protected onSurrenderCancel(): void {
    this.showSurrenderConfirm.set(false);
  }

  protected onSurrenderConfirm(): void {
    this.showSurrenderConfirm.set(false);
    this.onSurrender();
  }

  protected onSurrender(): void {
    if (this.isSpectator()) return;
    this.gameService.concede().subscribe();
  }

  protected onChoiceResolve(event: { choiceId: string; selectedOptionIds: string[] }): void {
    this.gameService.resolveChoice(event.choiceId, event.selectedOptionIds).subscribe();
  }

  protected onCoinFlipDismiss(): void {
    this.showCoinFlip.set(false);
  }

  protected onOpenChat(): void {
    this.sidePanel.update(s => ({ ...s, isOpen: true, activeTab: 'chat', newMessageCount: 0 }));
  }

  protected onOpenHistory(): void {
    const gameId = this.gameService.gameId();
    if (gameId) this.loadHistory(gameId);
    this.sidePanel.update(s => ({ ...s, isOpen: true, activeTab: 'history', newMessageCount: 0 }));
  }

  protected onSidePanelToggle(open: boolean): void {
    this.sidePanel.update(s => ({ ...s, isOpen: open, newMessageCount: 0 }));
  }

  protected onPreviewCard(url: string): void {
    this.previewImageUrl.set(url);
  }

  protected closePreview(): void {
    this.previewImageUrl.set(null);
  }

  protected dismissToast(toast: Toast): void {
    const timer = this.dismissTimers.get(toast.id);
    if (timer) {
      clearTimeout(timer);
      this.dismissTimers.delete(toast.id);
    }
    this.toasts.update(t => t.filter(x => x.id !== toast.id));
  }

  private showToast(event: GameEventDTO): void {
    const playerPart = event.playerName ? `[${event.playerName}] ` : '';
    const message = event.details
      ? `${playerPart}${event.details}`
      : `${playerPart}${this.getEventMessage(event.eventType)}`;
    const toast: Toast = { id: crypto.randomUUID(), message, type: event.eventType, timestamp: event.timestamp };
    this.toasts.update(t => {
      const updated = [...t, toast];
      return updated.length > this.MAX_TOASTS ? updated.slice(-this.MAX_TOASTS) : updated;
    });
    const timer = setTimeout(() => this.dismissToast(toast), 3000);
    this.dismissTimers.set(toast.id, timer);
  }

  private getEventMessage(eventType: string): string {
    const map: Record<string, string> = {
      POKEMON_KO: '¡Pokémon debilitado!',
      PRIZE_TAKEN: 'Carta de premio obtenida',
      STATUS_APPLIED: 'Condición especial aplicada',
      TURN_STARTED: 'Turno iniciado',
      GAME_ENDED: '¡Juego terminado!',
      ENERGY_ATTACHED: 'Energía adjuntada',
      EVOLUTION: 'Evolución',
      RETREAT: 'Retirada',
      MULLIGAN: 'Mulligan',
      SETUP_COMPLETE: 'Setup completado',
      GAME_START: '¡Comienza la partida!',
      TURN_END: 'Turno finalizado',
      ATTACK: 'Ataque',
    };
    return map[eventType] ?? eventType;
  }

  protected onCardDrop(event: { cardId: string; targetSlot: string }): void {
    const state = this.gameService.gameState();
    if (!state || this.isSpectator()) return;

    const cardIndex = this.playerHand().findIndex(c => c.id === event.cardId);
    if (cardIndex < 0) return;

    if (state.status === 'SETUP') {
      if (event.targetSlot.startsWith('active')) {
        this.gameService.setupPlaceActive(event.cardId).subscribe();
      } else {
        this.gameService.setupPlaceBench(event.cardId).subscribe();
      }
    } else {
      const targetIndex = this.parseTargetIndex(event.targetSlot);
      this.playCardByType(cardIndex, targetIndex);
    }

    this.selectedCardIndex.set(null);
  }
}
