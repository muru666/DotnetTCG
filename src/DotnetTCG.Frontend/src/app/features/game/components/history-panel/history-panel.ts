import { ChangeDetectionStrategy, Component, input } from '@angular/core';
import { GameHistoryEvent } from '../../../../shared/models/ui/game-history-event.model';
import { HistoryEventComponent } from '../history-event/history-event';

@Component({
  selector: 'app-history-panel',
  imports: [HistoryEventComponent],
  template: `
    <div class="history-container">
      @for (evt of events(); track evt.id) {
        <app-history-event [event]="evt" />
      }
      @if (events().length === 0) {
        <div class="history-empty">No hay eventos aún</div>
      }
    </div>
  `,
  styles: [`
    .history-container {
      padding: 8px 0;
      overflow-y: auto;
      height: 100%;
    }
    .history-empty {
      text-align: center;
      color: rgba(255, 255, 255, 0.2);
      font-family: 'Nunito', sans-serif;
      font-size: 0.8rem;
      padding: 40px 0;
    }
  `],
  changeDetection: ChangeDetectionStrategy.OnPush
})
export class HistoryPanelComponent {
  readonly events = input<GameHistoryEvent[]>([]);
}
