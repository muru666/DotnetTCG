import { ChangeDetectionStrategy, Component, DestroyRef, inject, OnInit, signal } from '@angular/core';
import { CommonModule, NgOptimizedImage } from '@angular/common';
import { ReactiveFormsModule, Validators } from '@angular/forms';
import { FormBuilder } from '@angular/forms';
import { ActivatedRoute, Router } from '@angular/router';
import { takeUntilDestroyed } from '@angular/core/rxjs-interop';
import { Observable, of } from 'rxjs';
import {
  DeckBuilderService,
  Card,
  Deck,
  DeckCard,
  SaveDeckCard
} from '../../../../shared/services/api/deck-builder.service';
import { CardSearchComponent } from '../../../../shared/components/card-search/card-search';
import { BackgroundImageService } from '../../../../core/services/background-image.service';

@Component({
  selector: 'app-deck-builder-page',
  standalone: true,
  imports: [CommonModule, ReactiveFormsModule, CardSearchComponent, NgOptimizedImage],
  templateUrl: './deck-builder-page.html',
  styleUrl: './deck-builder-page.css',
  changeDetection: ChangeDetectionStrategy.OnPush,
  host: {
    '[style.background-image]': 'backgroundImage()'
  }
})
export class DeckBuilderPageComponent implements OnInit {
  private readonly fb = inject(FormBuilder);
  private readonly route = inject(ActivatedRoute);
  private readonly router = inject(Router);
  private readonly destroyRef = inject(DestroyRef);
  readonly deckService = inject(DeckBuilderService);
  private readonly backgroundImageService = inject(BackgroundImageService);
  protected readonly backgroundImage = signal(this.backgroundImageService.getAuthBackgroundImage());



  readonly deckNameForm = this.fb.group({
    name: ['', Validators.required]
  });

  readonly Math = Math;
  readonly selectedCard = signal<Card | null>(null);
  readonly addQuantity = signal(1);
  readonly hasUnsavedChanges = signal(false);
  readonly isNewDeck = signal(false);
  readonly showReqModal = signal(false);
  readonly showUnsavedModal = signal(false);
  private readonly discardResolve = signal<((value: boolean) => void) | null>(null);

  ngOnInit(): void {
    const deckId = this.route.snapshot.paramMap.get('id');

    this.deckNameForm.valueChanges.pipe(takeUntilDestroyed(this.destroyRef)).subscribe(() => {
      if (this.deckService.currentDeck()) {
        this.hasUnsavedChanges.set(true);
      }
    });

    this.deckService.getUserDecks().subscribe(decks => {
      if (deckId) {
        this.deckService.loadDeck(deckId).subscribe({
          next: deck => this.applyLoadedDeck(deck),
          error: () => this.router.navigate(['/deck-builder'])
        });
      } else if (decks.length > 0) {
        this.deckService.loadDeck(decks[0].id).subscribe({
          next: deck => this.applyLoadedDeck(deck)
        });
      }
    });
  }

  selectCard(card: Card): void {
    this.selectedCard.set(card);
    this.addQuantity.set(1);
  }

  addToDeck(): void {
    const deck = this.deckService.currentDeck();
    const card = this.selectedCard();
    if (!deck || !card) return;

    const quantityToAdd = Math.min(this.addQuantity(), this.maxAddQuantity(card));
    if (quantityToAdd < 1) return;

    this.deckService.currentDeck.update(current => {
      if (!current) return current;
      const existing = current.deckCards.find(dc => dc.card.id === card.id);
      const deckCards = existing
        ? current.deckCards.map(dc =>
            dc.card.id === card.id ? { ...dc, quantity: dc.quantity + quantityToAdd } : dc
          )
        : [...current.deckCards, { id: `draft-${card.id}`, card, quantity: quantityToAdd }];
      return { ...current, deckCards };
    });
    this.hasUnsavedChanges.set(true);
    this.selectedCard.set(null);
  }

  removeFromDeck(cardId: string): void {
    const deck = this.deckService.currentDeck();
    if (!deck) return;

    this.deckService.currentDeck.update(current => current
      ? { ...current, deckCards: current.deckCards.filter(dc => dc.card.id !== cardId) }
      : current
    );
    this.hasUnsavedChanges.set(true);
  }

  updateQuantity(cardId: string, quantity: number): void {
    const deck = this.deckService.currentDeck();
    if (!deck || quantity < 1) return;

    const deckCard = deck.deckCards.find(dc => dc.card.id === cardId);
    if (!deckCard) return;

    const maxQuantity = this.maxQuantityForExistingCard(deckCard.card, deckCard.quantity);
    const nextQuantity = Math.min(quantity, maxQuantity);
    if (nextQuantity === deckCard.quantity) return;

    this.deckService.currentDeck.update(current => current
      ? {
          ...current,
          deckCards: current.deckCards.map(dc =>
            dc.card.id === cardId ? { ...dc, quantity: nextQuantity } : dc
          )
        }
      : current
    );
    this.hasUnsavedChanges.set(true);
  }

  createNewDeck(): void {
    this.confirmDiscardChanges().subscribe(confirmed => {
      if (!confirmed) return;

      this.deckService.currentDeck.set({
        id: 'draft-new-deck',
        name: '',
        isValid: false,
        version: 0,
        totalCards: 0,
        deckCards: []
      });
      this.deckNameForm.patchValue({ name: '' }, { emitEvent: false });
      this.isNewDeck.set(true);
      this.hasUnsavedChanges.set(false);
      this.selectedCard.set(null);
    });
  }

  saveDeck(): void {
    if (this.deckService.isLoading()) return;
    const deck = this.deckService.currentDeck();
    const name = this.deckNameForm.getRawValue().name;
    if (!name?.trim()) {
      this.deckService.error.set('El nombre del mazo es obligatorio');
      return;
    }
    const cards = this.toSaveDeckCards(deck?.deckCards ?? []);

    if (!deck || this.isNewDeck()) {
      this.deckService.createDeck(name.trim(), cards).subscribe({
        next: savedDeck => this.applyLoadedDeck(savedDeck)
      });
      return;
    }

    this.deckService.saveDeck(deck.id, name.trim(), cards).subscribe({
      next: savedDeck => this.applyLoadedDeck(savedDeck)
    });
  }

  selectDeck(deckId: string): void {
    this.confirmDiscardChanges().subscribe(confirmed => {
      if (!confirmed) return;

      this.deckService.loadDeck(deckId).subscribe({
        next: deck => this.applyLoadedDeck(deck)
      });
    });
  }

  deleteDeck(): void {
    const deck = this.deckService.currentDeck();
    if (!deck) return;

    if (this.isNewDeck()) {
      this.deckService.currentDeck.set(null);
      this.deckNameForm.patchValue({ name: '' }, { emitEvent: false });
      this.isNewDeck.set(false);
      this.hasUnsavedChanges.set(false);
      return;
    }

    this.deckService.deleteDeck(deck.id).subscribe({
      next: () => this.router.navigate(['/deck-builder'])
    });
  }

  goBack(): void {
    this.router.navigate(['/main-menu']);
  }

  canDeactivate(): Observable<boolean> {
    return this.confirmDiscardChanges();
  }

  onDiscardCancel(): void {
    this.discardResolve()?.(false);
  }

  onDiscardConfirm(): void {
    this.discardResolve()?.(true);
  }

  isBasicEnergy(card: Card): boolean {
    return card.tipo === 'ENERGY' && !card.specialEnergyType;
  }

  maxAddQuantity(card: Card): number {
    const currentQuantity = this.currentQuantity(card.id);
    const remainingSlots = this.deckService.remainingSlots();
    const copySlots = this.isBasicEnergy(card) ? 60 : Math.max(0, 4 - currentQuantity);
    return Math.min(remainingSlots, copySlots);
  }

  maxQuantityForExistingCard(card: Card, currentQuantity: number): number {
    const otherCardsTotal = this.deckService.totalCards() - currentQuantity;
    const deckSlots = 60 - otherCardsTotal;
    const copySlots = this.isBasicEnergy(card) ? 60 : 4;
    return Math.min(deckSlots, copySlots);
  }

  canIncrease(deckCard: DeckCard): boolean {
    return deckCard.quantity < this.maxQuantityForExistingCard(deckCard.card, deckCard.quantity);
  }

  increaseAddQuantity(): void {
    const card = this.selectedCard();
    if (!card) return;

    this.addQuantity.update(quantity => Math.min(this.maxAddQuantity(card), quantity + 1));
  }

  decreaseAddQuantity(): void {
    this.addQuantity.update(quantity => Math.max(1, quantity - 1));
  }

  trackCardId(_index: number, card: Card): string {
    return card.id;
  }

  trackDeckCardId(_index: number, dc: DeckCard): string {
    return dc.id || dc.card.id;
  }

  private applyLoadedDeck(deck: Deck | null): void {
    if (!deck) return;

    this.deckService.currentDeck.set(deck);
    this.deckNameForm.patchValue({ name: deck.name }, { emitEvent: false });
    this.isNewDeck.set(false);
    this.hasUnsavedChanges.set(false);
  }

  private currentQuantity(cardId: string): number {
    return this.deckService.currentDeck()?.deckCards.find(dc => dc.card.id === cardId)?.quantity ?? 0;
  }

  private toSaveDeckCards(deckCards: DeckCard[]): SaveDeckCard[] {
    return deckCards.map(dc => ({
      cardId: dc.card.id,
      quantity: dc.quantity
    }));
  }

  private confirmDiscardChanges(): Observable<boolean> {
    if (!this.hasUnsavedChanges()) return of(true);

    this.showUnsavedModal.set(true);
    return new Observable<boolean>(observer => {
      this.discardResolve.set((value: boolean) => {
        this.showUnsavedModal.set(false);
        this.discardResolve.set(null);
        observer.next(value);
        observer.complete();
      });
    });
  }
}
