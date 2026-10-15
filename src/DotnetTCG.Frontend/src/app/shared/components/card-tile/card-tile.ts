import { ChangeDetectionStrategy, Component, input } from '@angular/core';
import { NgOptimizedImage } from '@angular/common';

import { Card } from '../../services/api/deck-builder.service';

const TYPE_LABEL: Record<Card['tipo'], string> = {
  POKEMON: 'Pokémon',
  ENERGY: 'Energía',
  TRAINER: 'Entrenador',
};

@Component({
  selector: 'app-card-tile',
  imports: [NgOptimizedImage],
  template: `
    @if (card(); as c) {
      <div class="tile" [class.tile--selected]="selected()"
        [class.tile--pokemon]="c.tipo === 'POKEMON'"
        [class.tile--energy]="c.tipo === 'ENERGY'"
        [class.tile--trainer]="c.tipo === 'TRAINER'">
        <img [ngSrc]="c.imageUrl" [alt]="c.nombre" class="tile-img" width="120" height="168" [priority]="first()" />
        <div class="tile-info">
          <span class="tile-name">{{ c.nombre }}</span>
          <span class="tile-meta">
            <span class="tile-badge">{{ TYPE_LABEL[c.tipo] }}</span>
            @if (c.tipo === 'POKEMON' && c.hp !== undefined) {
              <span class="tile-hp">PS {{ c.hp }}</span>
            }
          </span>
        </div>
      </div>
    }
  `,
  styles: [`
    .tile {
      border-radius: 0.7rem;
      border: 1px solid rgba(148, 163, 184, 0.25);
      background: rgba(15, 23, 42, 0.55);
      overflow: hidden;
      cursor: pointer;
      display: flex;
      flex-direction: column;
      transition: transform 160ms ease, box-shadow 160ms ease, border-color 160ms ease;
    }
    .tile:hover {
      transform: translateY(-3px);
      box-shadow: 0 6px 16px rgba(0, 0, 0, 0.4);
      border-color: rgba(245, 158, 11, 0.55);
    }
    .tile--selected {
      border-color: #f59e0b;
      box-shadow: 0 0 0 2px rgba(245, 158, 11, 0.7), 0 6px 16px rgba(0, 0, 0, 0.4);
    }
    .tile-img {
      width: 100%;
      height: auto;
      aspect-ratio: 2.5 / 3.5;
      object-fit: contain;
      display: block;
      background: rgba(0, 0, 0, 0.25);
    }
    .tile-info {
      padding: 0.4rem 0.5rem 0.5rem;
      display: flex;
      flex-direction: column;
      gap: 0.25rem;
    }
    .tile-name {
      font-weight: 800;
      font-size: 0.78rem;
      white-space: nowrap;
      overflow: hidden;
      text-overflow: ellipsis;
      font-family: 'Nunito', sans-serif;
      color: #f1f5f9;
    }
    .tile-meta {
      display: flex;
      align-items: center;
      justify-content: space-between;
      gap: 0.3rem;
    }
    .tile-badge {
      font-size: 0.62rem;
      font-weight: 800;
      text-transform: uppercase;
      letter-spacing: 0.4px;
      padding: 0.1rem 0.45rem;
      border-radius: 1rem;
      font-family: 'Nunito', sans-serif;
    }
    .tile--pokemon .tile-badge { background: rgba(59, 130, 246, 0.25); color: #93c5fd; }
    .tile--energy .tile-badge { background: rgba(34, 197, 94, 0.22); color: #86efac; }
    .tile--trainer .tile-badge { background: rgba(168, 85, 247, 0.25); color: #d8b4fe; }
    .tile-hp {
      font-size: 0.68rem;
      font-weight: 700;
      color: #f87171;
      font-family: 'Nunito', sans-serif;
      white-space: nowrap;
    }
  `],
  changeDetection: ChangeDetectionStrategy.OnPush
})
export class CardTileComponent {
  readonly card = input.required<Card>();
  readonly selected = input(false);
  readonly first = input(false);

  protected readonly TYPE_LABEL = TYPE_LABEL;
}
