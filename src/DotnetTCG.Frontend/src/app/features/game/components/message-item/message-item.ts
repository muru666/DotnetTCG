import { ChangeDetectionStrategy, Component, input } from '@angular/core';
import { ChatMessage } from '../../../../shared/models/chat/chat-message.model';

@Component({
  selector: 'app-message-item',
  template: `
    <div class="message" [class.own]="isOwn()">
      <div class="message-header">
        <span class="message-player">{{ message().senderName }}</span>
        <span class="message-time">{{ formatTime(message().timestamp) }}</span>
      </div>
      <div class="message-body">{{ message().content }}</div>
    </div>
  `,
  styles: [`
    .message {
      padding: 6px 10px;
      margin: 4px 8px;
      border-radius: 8px;
      background: rgba(255, 255, 255, 0.05);
      max-width: 85%;
    }
    .message.own {
      background: rgba(245, 158, 11, 0.1);
      margin-left: auto;
    }
    .message-header {
      display: flex;
      justify-content: space-between;
      align-items: center;
      margin-bottom: 2px;
    }
    .message-player {
      font-size: 0.7rem;
      font-weight: 700;
      color: #f59e0b;
      font-family: 'Nunito', sans-serif;
    }
    .message-time {
      font-size: 0.6rem;
      color: rgba(255, 255, 255, 0.3);
      font-family: 'Nunito', sans-serif;
    }
    .message-body {
      font-size: 0.8rem;
      color: #cbd5e1;
      word-wrap: break-word;
      font-family: 'Nunito', sans-serif;
    }
  `],
  changeDetection: ChangeDetectionStrategy.OnPush
})
export class MessageItemComponent {
  readonly message = input.required<ChatMessage>();
  readonly isOwn = input(false);

  protected formatTime(ts: Date | string): string {
    const d = new Date(ts);
    return d.toLocaleTimeString([], { hour: '2-digit', minute: '2-digit' });
  }
}
