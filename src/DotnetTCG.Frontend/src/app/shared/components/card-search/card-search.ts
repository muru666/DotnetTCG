import {
  ChangeDetectionStrategy, Component, inject, input, model,
  OnDestroy, OnInit, output, signal
} from '@angular/core';
import { FormsModule } from '@angular/forms';
import { Subscription } from 'rxjs';
import { DeckBuilderService, Card } from '../../services/api/deck-builder.service';
import { CardTileComponent } from '../card-tile/card-tile';

const FILTER_TYPES = ['POKEMON', 'ENERGY', 'TRAINER'] as const;

@Component({
  selector: 'app-card-search',
  standalone: true,
  imports: [FormsModule, CardTileComponent],
  templateUrl: './card-search.html',
  styleUrl: './card-search.css',
  changeDetection: ChangeDetectionStrategy.OnPush,
  host: { class: 'card-search-host' }
})
export class CardSearchComponent implements OnInit, OnDestroy {
  private readonly deckBuilderService = inject(DeckBuilderService);
  private searchSubscription: Subscription | null = null;

  showFilters = input<boolean>(true);
  tipoFilter = model<string>('');
  /** Resalta la carta con este id (p. ej. la seleccionada en el builder). */
  selectedId = input<string | null>(null);

  readonly cardClick = output<Card>();

  protected readonly filterTypes: readonly string[] = FILTER_TYPES;
  protected results = signal<Card[]>([]);
  protected isLoading = signal(false);
  protected currentPage = signal(0);
  protected totalPages = signal(0);
  protected totalElements = signal(0);
  protected query = signal('');

  ngOnInit(): void {
    this.loadPage(0);
  }

  protected onSearch(value: string): void {
    this.query.set(value);
    this.loadPage(0);
  }

  protected setTipo(tipo: string): void {
    const newValue = this.tipoFilter() === tipo ? '' : tipo;
    this.tipoFilter.set(newValue);
    this.loadPage(0);
  }

  protected goToPage(page: number): void {
    if (page < 0 || page >= this.totalPages()) return;
    this.loadPage(page);
  }

  protected typeLabel(tipo: string): string {
    return tipo === 'POKEMON' ? 'Pokémon' : tipo === 'ENERGY' ? 'Energía' : 'Entrenador';
  }

  private loadPage(page: number): void {
    // Cancelar request anterior (para búsquedas por nombre).
    // Los resultados vigentes se mantienen visibles hasta que llegan los nuevos.
    this.searchSubscription?.unsubscribe();

    this.isLoading.set(true);
    this.currentPage.set(page);

    this.searchSubscription = this.deckBuilderService.searchCards(
      this.query(),
      this.tipoFilter() || undefined,
      page
    ).subscribe({
      next: () => this.syncFromService(),
      error: () => this.isLoading.set(false)
    });
  }

  ngOnDestroy(): void {
    this.searchSubscription?.unsubscribe();
    this.deckBuilderService.resetSearchState();
  }

  private syncFromService(): void {
    this.results.set(this.deckBuilderService.searchResults());
    this.currentPage.set(this.deckBuilderService.currentPage());
    this.totalPages.set(this.deckBuilderService.totalPages());
    this.totalElements.set(this.deckBuilderService.totalElements());
    this.isLoading.set(false);
  }

  protected onCardClick(card: Card): void {
    this.cardClick.emit(card);
  }
}
