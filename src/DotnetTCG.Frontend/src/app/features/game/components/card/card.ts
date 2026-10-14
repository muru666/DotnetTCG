import { ChangeDetectionStrategy, Component, input, output } from '@angular/core';
import { NgOptimizedImage } from '@angular/common';

import { CdkDrag } from '@angular/cdk/drag-drop';
import { Card } from '../../../../shared/models/dtos/game-state.model';

@Component({
  selector: 'app-card',
  imports: [CdkDrag, NgOptimizedImage],
  template: `
    <div
      class="card-wrapper"
      [class.selected]="selected()"
      [class.face-down]="faceDown()"
      (click)="clicked.emit()"
      (dblclick)="preview.emit(card()?.imageUrl ?? ''); $event.stopPropagation()"
      cdkDrag
      [cdkDragData]="card()?.id"
      [cdkDragDisabled]="!draggable() || disabled()"
    >
      @if (faceDown()) {
        <div class="card-back">
          <span class="card-back-icon">⚡</span>
        </div>
      } @else if (card(); as c) {
        <img [ngSrc]="c.imageUrl" [alt]="c.name" class="card-image" fill />
        <div class="card-name">{{ c.name }}</div>
      } @else {
        <div class="card-empty"></div>
      }
    </div>
  `,
  styles: [`
    .card-wrapper {
      width: 120px;
      height: 168px;
      border-radius: 8px;
      overflow: hidden;
      position: relative;
      cursor: pointer;
      transition: transform 0.2s, box-shadow 0.2s;
      background: #fff;
      box-shadow: 0 2px 6px rgba(0,0,0,0.15);
    }
    .card-wrapper:hover {
      transform: translateY(-4px);
      box-shadow: 0 4px 12px rgba(0,0,0,0.25);
    }
    .card-wrapper.selected {
      box-shadow: 0 0 0 3px #2196F3;
      transform: translateY(-8px);
    }
    .card-wrapper.cdk-drag-preview {
      box-shadow: 0 8px 24px rgba(0,0,0,0.3);
    }
    .card-wrapper.cdk-drag-placeholder {
      opacity: 0.3;
    }
    .card-image {
      object-fit: contain;
    }
    .card-name {
      position: absolute;
      bottom: 0;
      left: 0;
      right: 0;
      background: rgba(0,0,0,0.6);
      color: #fff;
      font-size: 0.65rem;
      padding: 2px 4px;
      text-align: center;
      white-space: nowrap;
      overflow: hidden;
      text-overflow: ellipsis;
      font-family: 'Nunito', sans-serif;
    }
    .card-back {
      width: 100%;
      height: 100%;
      background: linear-gradient(135deg, #1a237e, #283593);
      display: flex;
      align-items: center;
      justify-content: center;
      border-radius: 8px;
    }
    .card-back-icon {
      font-size: 2rem;
      color: #ffd700;
    }
    .card-empty {
      width: 100%;
      height: 100%;
      background: rgba(15, 23, 42, 0.6);
      border-radius: 8px;
    }
    .card-wrapper.disabled {
      opacity: 0.35;
      filter: grayscale(0.7);
      cursor: not-allowed;
    }
    .card-wrapper.disabled:hover {
      transform: none;
      box-shadow: 0 2px 6px rgba(0,0,0,0.15);
    }
  `],
  changeDetection: ChangeDetectionStrategy.OnPush
})
export class CardComponent {
  readonly card = input<Card | null>(null);
  readonly selected = input(false);
  readonly faceDown = input(false);
  readonly draggable = input(false);
  readonly disabled = input(false);
  readonly clicked = output<void>();
  readonly preview = output<string>();
}
