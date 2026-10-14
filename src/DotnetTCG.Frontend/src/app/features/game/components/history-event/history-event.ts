import { ChangeDetectionStrategy, Component, input } from '@angular/core';
import { GameHistoryEvent } from '../../../../shared/models/ui/game-history-event.model';

@Component({
  selector: 'app-history-event',
  template: `
    <div class="event">
      <span class="event-turn">Turno {{ event().turn }}</span>
      <span class="event-action">{{ event().action }}</span>
      <span class="event-player">{{ event().player }}</span>
      <span class="event-time">{{ formatTime(event().timestamp) }}</span>
    </div>
  `,
  styles: [`
    .event {
      display: flex;
      gap: 8px;
      align-items: center;
      padding: 6px 10px;
      margin: 2px 8px;
      font-size: 0.75rem;
      font-family: 'Nunito', sans-serif;
      color: #94a3b8;
      border-bottom: 1px solid rgba(255, 255, 255, 0.04);
    }
    .event-turn {
      font-weight: 700;
      color: #f59e0b;
      min-width: 55px;
    }
    .event-action {
      flex: 1;
    }
    .event-player {
      color: rgba(255, 255, 255, 0.35);
      font-size: 0.7rem;
    }
    .event-time {
      color: rgba(255, 255, 255, 0.2);
      font-size: 0.65rem;
      min-width: 40px;
      text-align: right;
    }
  `],
  changeDetection: ChangeDetectionStrategy.OnPush
})
export class HistoryEventComponent {
  readonly event = input.required<GameHistoryEvent>();

  protected formatTime(ts: Date | string): string {
    const d = new Date(ts);
    return d.toLocaleTimeString([], { hour: '2-digit', minute: '2-digit' });
  }
}
