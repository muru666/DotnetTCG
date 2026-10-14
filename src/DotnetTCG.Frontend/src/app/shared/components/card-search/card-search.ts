import {
  ChangeDetectionStrategy, Component, ContentChild, inject, input, model,
  OnDestroy, OnInit, output, signal, TemplateRef
} from '@angular/core';
import { FormsModule } from '@angular/forms';
import { NgTemplateOutlet, NgOptimizedImage } from '@angular/common';
import { Subscription } from 'rxjs';
import { DeckBuilderService, Card } from '../../services/api/deck-builder.service';

@Component({
  selector: 'app-card-search',
  standalone: true,
  imports: [FormsModule, NgTemplateOutlet, NgOptimizedImage],
  templateUrl: './card-search.html',
  styleUrl: './card-search.css',
  changeDetection: ChangeDetectionStrategy.OnPush,
  host: { class: 'card-search-host' }
})
export class CardSearchComponent implements OnInit, OnDestroy {
  private readonly deckBuilderService = inject(DeckBuilderService);
  private hasLoadedOnce = false;
  private searchSubscription: Subscription | null = null;

  paginationMode = input<'full' | 'simple'>('full');
  showFilters = input<boolean>(true);
  tipoFilter = model<string>('');

  readonly cardClick = output<Card>();

  @ContentChild('cardCard', { read: TemplateRef })
  protected cardCardTemplate?: TemplateRef<{ $implicit: Card; first?: boolean }>;

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

  private loadPage(page: number): void {
    // Cancelar request anterior (para búsquedas por nombre)
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
    if (!this.hasLoadedOnce) {
      this.hasLoadedOnce = true;
    }
    window.scrollTo({ top: 0, behavior: 'smooth' });
  }

  protected onCardClick(card: Card): void {
    this.cardClick.emit(card);
  }

  protected getPaginationPages(): (number | null)[] {
    const total = this.totalPages();
    const current = this.currentPage();
    if (total <= 1) return [];

    const pages = new Set<number>();
    pages.add(0);
    pages.add(total - 1);
    for (let i = Math.max(0, current - 1); i <= Math.min(total - 1, current + 1); i++) pages.add(i);

    const sorted = [...pages].sort((a, b) => a - b);
    const result: (number | null)[] = [];
    for (let i = 0; i < sorted.length; i++) {
      if (i > 0 && sorted[i] - sorted[i - 1] > 1) result.push(null);
      result.push(sorted[i]);
    }
    return result;
  }
}