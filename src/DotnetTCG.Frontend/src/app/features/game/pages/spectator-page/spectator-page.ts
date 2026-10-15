import { ChangeDetectionStrategy, Component, OnDestroy, OnInit, computed, inject, signal } from '@angular/core';
import { NgOptimizedImage } from '@angular/common';
import { Router, ActivatedRoute } from '@angular/router';
import { GameService } from '../../../../shared/services/api/game.service';
import { GameSignalService } from '../../../../shared/services/websocket/game-signal.service';
import { AuthService } from '../../../../core/services/auth.service';
import { ChatMessage } from '../../../../shared/models/chat/chat-message.model';
import { SpectatorPlayerZoneComponent } from '../../components/spectator-player-zone/spectator-player-zone';
import { ChatPanelComponent } from '../../components/chat-panel/chat-panel';

@Component({
  selector: 'app-spectator-page',
  imports: [NgOptimizedImage, SpectatorPlayerZoneComponent, ChatPanelComponent],
  templateUrl: './spectator-page.html',
  styleUrl: './spectator-page.css',
  changeDetection: ChangeDetectionStrategy.OnPush
})
export class SpectatorPageComponent implements OnInit, OnDestroy {
  private readonly route = inject(ActivatedRoute);
  private readonly router = inject(Router);
  protected readonly authService = inject(AuthService);
  protected readonly gameService = inject(GameService);
  private readonly gameSignalService = inject(GameSignalService);

  protected readonly loadingError = signal(false);
  protected readonly previewImageUrl = signal<string | null>(null);

  private readonly playerNamesById = signal<Map<string, string>>(new Map());

  protected readonly player1Name = computed(() => {
    const state = this.gameService.gameState();
    const id = state?.players?.[0];
    return (id && this.playerNamesById().get(id)) || 'Jugador 1';
  });

  protected readonly player2Name = computed(() => {
    const state = this.gameService.gameState();
    const id = state?.players?.[1];
    return (id && this.playerNamesById().get(id)) || 'Jugador 2';
  });

  protected readonly isGameOver = computed(() => this.gameService.gameState()?.status === 'FINISHED');

  protected readonly winnerName = computed(() => {
    const state = this.gameService.gameState();
    if (!state?.winner) return null;
    return this.playerNamesById().get(state.winner) ?? null;
  });

  ngOnInit(): void {
    const gameId = this.route.snapshot.paramMap.get('gameId');
    if (!gameId) { this.router.navigate(['/lobby']); return; }

    this.gameService.getLobbyState(gameId).subscribe({
      next: lobby => {
        const map = new Map<string, string>();
        for (const p of lobby.players) map.set(p.playerId, p.username);
        this.playerNamesById.set(map);
      },
      error: () => {}
    });

    this.gameService.loadSpectatorGame(gameId).subscribe({
      error: () => this.loadingError.set(true)
    });

  }

  ngOnDestroy(): void {
    this.gameService.disconnect();
  }

  protected goBack(): void {
    this.router.navigate(['/lobby']);
  }

  protected onSendSpectatorMessage(msg: ChatMessage): void {
    this.gameService.sendChatMessage(msg.content, this.authService.getUsername() ?? 'Espectador');
  }

  protected onPreviewCard(url: string): void {
    this.previewImageUrl.set(url);
  }

  protected closePreview(): void {
    this.previewImageUrl.set(null);
  }
}
