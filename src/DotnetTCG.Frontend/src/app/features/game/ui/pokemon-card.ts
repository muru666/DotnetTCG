import { Component, computed, input, output } from '@angular/core';
import { NgOptimizedImage } from '@angular/common';
import { FieldPokemonDto, STATUS_LABELS } from '../models/game.models';

@Component({
  selector: 'app-pokemon-card',
  imports: [NgOptimizedImage],
  template: `
    @if (pokemon(); as poke) {
      <div
        class="pokemon"
        [class.selected]="selected()"
        [class.dimmed]="dimmed()"
        (click)="clicked.emit()"
        (dblclick)="previewImage(); $event.stopPropagation()"
        role="button"
        tabindex="0"
        [attr.aria-label]="poke.name"
        (keydown.enter)="clicked.emit()"
      >
        @if (imageUrl() !== null) {
          <img
            [ngSrc]="imageUrl()!"
            [alt]="poke.name"
            width="120"
            height="168"
            class="pokemon-img"
          />
        } @else {
          <div class="pokemon-fallback">{{ poke.name }}</div>
        }
        <div class="hp-bar" aria-hidden="true"><span [style.width.%]="hpPercent()"></span></div>
        <div class="badges">
          @if (poke.damageCounters > 0) {
            <span class="badge dmg" title="Contadores de daño">🩸{{ poke.damageCounters }}</span>
          }
          @if (poke.totalEnergy > 0) {
            <span class="badge energy" title="Energías unidas">⚡{{ poke.totalEnergy }}</span>
          }
          @if (poke.tool !== null) {
            <span class="badge tool" title="Herramienta equipada">🧰</span>
          }
          @for (status of poke.statuses; track status) {
            <span class="badge status" [title]="status">{{ statusLabel(status) }}</span>
          }
        </div>
      </div>
    } @else {
      <div class="pokemon-empty" (click)="clicked.emit()" role="button" tabindex="0" (keydown.enter)="clicked.emit()">
        <span>+</span>
      </div>
    }
  `,
  styles: [
    `
      .pokemon {
        position: relative;
        width: 120px;
        border-radius: var(--radius-md);
        overflow: hidden;
        cursor: pointer;
        background: var(--surface-2);
        border: 2px solid transparent;
        transition: transform 150ms ease, border-color 150ms ease;
      }
      .pokemon:hover {
        transform: translateY(-4px);
      }
      .pokemon.selected {
        border-color: var(--accent);
        transform: translateY(-6px);
      }
      .pokemon.dimmed {
        opacity: 0.45;
        filter: grayscale(0.6);
      }
      .pokemon-img {
        display: block;
        width: 120px;
        height: 168px;
        object-fit: cover;
      }
      .pokemon-fallback {
        width: 120px;
        height: 168px;
        display: flex;
        align-items: center;
        justify-content: center;
        text-align: center;
        padding: 0.5rem;
        font-weight: 600;
      }
      .hp-bar {
        height: 5px;
        background: var(--bg-deep);
      }
      .hp-bar span {
        display: block;
        height: 100%;
        background: var(--accent-ok);
      }
      .badges {
        position: absolute;
        top: 4px;
        left: 4px;
        display: flex;
        gap: 3px;
        flex-wrap: wrap;
      }
      .badge {
        font-size: 0.65rem;
        font-weight: 700;
        background: rgba(0, 0, 0, 0.7);
        border-radius: 4px;
        padding: 1px 4px;
      }
      .pokemon-empty {
        width: 120px;
        height: 168px;
        border-radius: var(--radius-md);
        border: 2px dashed var(--surface-border);
        display: flex;
        align-items: center;
        justify-content: center;
        color: var(--text-muted);
        font-size: 1.5rem;
        cursor: pointer;
      }
      .pokemon-empty:hover {
        border-color: var(--accent);
        color: var(--accent);
      }
    `,
  ],
})
export class PokemonCardComponent {
  readonly pokemon = input<FieldPokemonDto | null>(null);
  readonly imageUrl = input<string | null>(null);
  readonly selected = input(false);
  readonly dimmed = input(false);
  readonly clicked = output<void>();
  readonly preview = output<string>();

  protected readonly hpPercent = computed(() => {
    const poke = this.pokemon();
    if (poke === null || poke.hp <= 0) {
      return 0;
    }
    return Math.max(0, Math.min(100, (poke.hpRemaining / poke.hp) * 100));
  });

  protected statusLabel(status: string): string {
    return STATUS_LABELS[status] ?? status;
  }

  protected previewImage(): void {
    const url = this.imageUrl();
    if (url !== null) {
      this.preview.emit(url);
    }
  }
}
