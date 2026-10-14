import { ChangeDetectionStrategy, Component, input, output } from '@angular/core';
import { CdkDropList, CdkDragDrop } from '@angular/cdk/drag-drop';

@Component({
  selector: 'app-card-slot',
  imports: [CdkDropList],
  template: `
    <div
      class="slot"
      [class.empty]="isEmpty()"
      [class.highlight]="highlight()"
      cdkDropList
      [cdkDropListData]="slotId()"
      (cdkDropListDropped)="onDrop($event)"
    >
      @if (isEmpty()) {
        <span class="slot-label">{{ label() }}</span>
      }
    </div>
  `,
  styles: [`
    .slot {
      width: 100px;
      height: 140px;
      border-radius: 8px;
      display: flex;
      align-items: center;
      justify-content: center;
      transition: all 0.2s;
      position: relative;
    }
    .slot.empty {
      background: rgba(15, 23, 42, 0.8);
      border: 2px dashed rgba(255, 255, 255, 0.12);
    }
    .slot.empty:hover {
      background: rgba(30, 41, 59, 0.8);
      border-color: rgba(245, 158, 11, 0.3);
    }
    .slot.highlight {
      border-color: rgba(245, 158, 11, 0.4);
      box-shadow: 0 0 0 3px rgba(245, 158, 11, 0.15);
    }
    .slot-label {
      color: rgba(255, 255, 255, 0.25);
      font-size: 0.7rem;
      text-align: center;
      font-family: 'Nunito', sans-serif;
      font-weight: 600;
      text-transform: uppercase;
      letter-spacing: 0.5px;
    }
  `],
  changeDetection: ChangeDetectionStrategy.OnPush
})
export class CardSlotComponent {
  readonly label = input('');
  readonly isEmpty = input(true);
  readonly highlight = input(false);
  readonly slotId = input('');

  readonly cardDropped = output<{ slotId: string; cardId: string }>();

  protected onDrop(event: CdkDragDrop<string>): void {
    this.cardDropped.emit({
      slotId: event.container.data,
      cardId: event.item.data
    });
  }
}
