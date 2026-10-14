import { ChangeDetectionStrategy, Component, inject, signal, computed } from '@angular/core';
import { NgOptimizedImage } from '@angular/common';
import { RouterLink } from '@angular/router';
import { BackgroundImageService } from '../../../../core/services/background-image.service';
import { DeckBuilderService, Card } from '../../../../shared/services/api/deck-builder.service';
import { CardSearchComponent } from '../../../../shared/components/card-search/card-search';

@Component({
  selector: 'app-pokedex-page',
  standalone: true,
  imports: [RouterLink, CardSearchComponent, NgOptimizedImage],
  templateUrl: './pokedex-page.html',
  styleUrl: './pokedex-page.css',
  changeDetection: ChangeDetectionStrategy.OnPush
})
export class PokedexPage {
  private readonly backgroundImageService = inject(BackgroundImageService);
  private readonly deckBuilderService = inject(DeckBuilderService);

  protected readonly backgroundImage = signal(this.backgroundImageService.getAuthBackgroundImage());
  protected selectedCard = signal<Card | null>(null);

  private readonly results = this.deckBuilderService.searchResults;

  protected selectedCardIndex = computed(() => {
    const card = this.selectedCard();
    if (!card) return -1;
    return this.results().findIndex(c => c.id === card.id);
  });

  protected hasPrevCard = computed(() => {
    const idx = this.selectedCardIndex();
    if (idx < 0) return false;
    return idx > 0 || this.deckBuilderService.currentPage() > 0;
  });

  protected hasNextCard = computed(() => {
    const idx = this.selectedCardIndex();
    if (idx < 0) return false;
    return idx < this.results().length - 1
      || this.deckBuilderService.currentPage() < this.deckBuilderService.totalPages() - 1;
  });

  protected openCard(card: Card): void {
    this.selectedCard.set(card);
  }

  protected closeCard(): void {
    this.selectedCard.set(null);
  }

  protected prevCard(): void {
    const idx = this.selectedCardIndex();
    if (idx > 0) {
      this.selectedCard.set(this.results()[idx - 1]);
    } else if (idx === 0 && this.deckBuilderService.currentPage() > 0) {
      const prevPage = this.deckBuilderService.currentPage() - 1;
      this.deckBuilderService.searchCards(
        this.deckBuilderService.lastSearchQuery(),
        this.deckBuilderService.lastSearchTipo(),
        prevPage
      ).subscribe(() => {
        const r = this.results();
        this.selectedCard.set(r[r.length - 1]);
      });
    }
  }

  protected nextCard(): void {
    const idx = this.selectedCardIndex();
    if (idx < this.results().length - 1) {
      this.selectedCard.set(this.results()[idx + 1]);
    } else if (
      idx === this.results().length - 1
      && this.deckBuilderService.currentPage() < this.deckBuilderService.totalPages() - 1
    ) {
      const nextPage = this.deckBuilderService.currentPage() + 1;
      this.deckBuilderService.searchCards(
        this.deckBuilderService.lastSearchQuery(),
        this.deckBuilderService.lastSearchTipo(),
        nextPage
      ).subscribe(() => {
        this.selectedCard.set(this.results()[0]);
      });
    }
  }
}
