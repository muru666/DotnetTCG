import { ChangeDetectionStrategy, Component, inject, OnInit, OnDestroy, signal, computed } from '@angular/core';
import { Router, RouterLink } from '@angular/router';
import { DatePipe } from '@angular/common';
import { FormsModule } from '@angular/forms';
import { Subject, Subscription } from 'rxjs';
import { debounceTime, distinctUntilChanged } from 'rxjs/operators';
import { BackgroundImageService } from '../../../../core/services/background-image.service';
import { GameService, GameListItem } from '../../../../shared/services/api/game.service';

type GameFilter = 'todas' | 'publicas' | 'privadas' | 'espectar';

@Component({
  selector: 'app-game-searcher',
  standalone: true,
  imports: [RouterLink, DatePipe, FormsModule],
  templateUrl: './lobby-page.html',
  styleUrl: './lobby-page.css',
  changeDetection: ChangeDetectionStrategy.OnPush
})
export class GameSearcherComponent implements OnInit, OnDestroy {
  private readonly backgroundImageService = inject(BackgroundImageService);
  private readonly gameService = inject(GameService);
  private readonly router = inject(Router);

  protected readonly backgroundImage = signal(this.backgroundImageService.getAuthBackgroundImage());
  protected allGames = signal<GameListItem[]>([]);
  protected isLoading = signal(false);
  protected gamesLoading = signal(false);
  protected error = signal<string | null>(null);
  protected activeFilter = signal<GameFilter>('todas');
  protected creatorSearch = signal('');
  protected activeGame = signal<{ gameId: string; status: string } | null>(null);
  protected resumableGames = signal<GameListItem[]>([]);
  protected showResumableGamesDialog = signal(false);

  protected passwordDialogGameId = signal<string | null>(null);
  protected passwordInput = signal('');
  protected passwordError = signal<string | null>(null);

  private readonly searchSubject = new Subject<string>();
  private searchSub?: Subscription;

  protected readonly filteredGames = computed(() => {
    const search = this.creatorSearch().toLowerCase().trim();
    const games = this.allGames();
    if (search.length < 3) return games;
    return games.filter(g =>
      g.creatorUsername?.toLowerCase().includes(search) ||
      g.players.some(p => p.username?.toLowerCase().includes(search))
    );
  });

  ngOnInit(): void {
    const navState = history.state as { error?: string } | null;
    if (navState?.error) {
      this.error.set(navState.error);
    }
    this.checkActiveGame();
    this.loadResumableGames();
    this.loadGames();
    this.searchSub = this.searchSubject.pipe(
      debounceTime(300),
      distinctUntilChanged()
    ).subscribe(value => this.creatorSearch.set(value));
  }

  ngOnDestroy(): void {
    this.searchSub?.unsubscribe();
    this.searchSubject.complete();
  }

  private checkActiveGame(): void {
    this.gameService.getMyActiveGame().subscribe({
      // PAUSED games are resumed through the explicit "Cargar Partida" ready-lobby flow,
      // not through this quick-resume button.
      next: game => this.activeGame.set(game && game.status !== 'PAUSED' ? game : null)
    });
  }

  private loadResumableGames(): void {
    this.gameService.listMyResumableGames().subscribe({
      next: games => this.resumableGames.set(games)
    });
  }

  private loadGames(): void {
    this.gamesLoading.set(true);
    this.gameService.listGames(this.activeFilter()).subscribe({
      next: games => {
        this.allGames.set(games);
        this.gamesLoading.set(false);
      },
      error: () => this.gamesLoading.set(false)
    });
  }

  protected selectFilter(filter: GameFilter): void {
    this.activeFilter.set(filter);
    this.loadGames();
  }

  protected onSearchInput(value: string): void {
    this.searchSubject.next(value);
  }

  protected createGame(): void {
    this.isLoading.set(true);
    this.error.set(null);

    this.gameService.createGame().subscribe({
      next: gameId => {
        this.isLoading.set(false);
        this.router.navigate(['/lobby', gameId]);
      },
      error: err => {
        const msg = typeof err === 'string' ? err : (err?.message ?? 'No se pudo crear la partida');
        this.error.set(msg);
        this.isLoading.set(false);
      }
    });
  }

  protected joinGame(game: GameListItem): void {
    if (this.activeFilter() === 'espectar') {
      this.router.navigate(['/lobby', game.gameId], { state: { spectator: true } });
      return;
    }
    if (game.players.length >= 2) {
      this.error.set('La partida está llena');
      return;
    }
    if (game.isPublic === false) {
      this.passwordDialogGameId.set(game.gameId);
      this.passwordInput.set('');
      this.passwordError.set(null);
    } else {
      this.router.navigate(['/lobby', game.gameId]);
    }
  }

  protected confirmPassword(): void {
    const gameId = this.passwordDialogGameId();
    const pwd = this.passwordInput().trim();
    if (!gameId || !pwd) return;

    this.gameService.checkPassword(gameId, pwd).subscribe({
      next: () => {
        this.passwordDialogGameId.set(null);
        this.passwordInput.set('');
        this.passwordError.set(null);
        this.router.navigate(['/lobby', gameId], { state: { password: pwd } });
      },
      error: (err: Error) => {
        this.passwordError.set(err.message ?? 'Contraseña incorrecta');
      }
    });
  }

  protected cancelPassword(): void {
    this.passwordDialogGameId.set(null);
    this.passwordInput.set('');
    this.passwordError.set(null);
  }

  protected rejoinGame(): void {
    const game = this.activeGame();
    if (!game) return;
    // La partida se reanuda desde el propio tablero (overlay de pausa).
    this.router.navigate(['/game', game.gameId]);
  }

  protected openResumableGamesDialog(): void {
    if (this.resumableGames().length === 0) return;
    this.showResumableGamesDialog.set(true);
  }

  protected closeResumableGamesDialog(): void {
    this.showResumableGamesDialog.set(false);
  }

  protected resumeGame(game: GameListItem): void {
    this.showResumableGamesDialog.set(false);
    // El tablero maneja la pausa con su overlay (Reconectar).
    this.router.navigate(['/game', game.gameId]);
  }

  protected refreshGames(): void {
    this.loadGames();
  }
}
