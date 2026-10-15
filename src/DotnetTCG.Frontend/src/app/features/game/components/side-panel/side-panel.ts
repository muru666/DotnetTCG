import { ChangeDetectionStrategy, Component, input, output } from '@angular/core';
import { ChatMessage } from '../../../../shared/models/chat/chat-message.model';
import { GameHistoryEvent } from '../../../../shared/models/ui/game-history-event.model';
import { ChatPanelComponent } from '../chat-panel/chat-panel';
import { HistoryPanelComponent } from '../history-panel/history-panel';

@Component({
  selector: 'app-side-panel',
  imports: [ChatPanelComponent, HistoryPanelComponent],
  template: `
    <div class="side-panel">
      <div class="panel-header">
        <span class="panel-title">
          {{ activeTab() === 'chat' ? 'Chat' : 'Historial' }}
          @if (activeTab() !== 'chat' && newMessageCount() > 0) {
            <span class="badge">{{ newMessageCount() }}</span>
          }
        </span>
        <button class="close-btn" (click)="toggle.emit(false)">✕</button>
      </div>

      <div class="panel-body">
        @if (activeTab() === 'chat') {
          <app-chat-panel
            [messages]="chatMessages()"
            [currentUserId]="currentUserId()"
            (sendMessage)="onSendMessage($event)"
          />
        } @else {
          <app-history-panel [events]="gameHistory()" />
        }
      </div>
    </div>
  `,
  styles: [`
    .side-panel {
      width: 100%;
      height: 100%;
      background: linear-gradient(180deg, #1a1a2e 0%, #16213e 100%);
      display: flex;
      flex-direction: column;
    }
    .panel-header {
      display: flex;
      align-items: center;
      justify-content: space-between;
      padding: 10px 14px;
      border-bottom: 1px solid rgba(255, 255, 255, 0.06);
    }
    .panel-title {
      font-family: 'Bungee', sans-serif;
      font-size: 0.7rem;
      color: #f59e0b;
      letter-spacing: 1.5px;
      text-transform: uppercase;
      display: flex;
      align-items: center;
      gap: 8px;
    }
    .badge {
      min-width: 18px;
      height: 18px;
      border-radius: 9px;
      background: #ef4444;
      color: #fff;
      font-family: 'Nunito', sans-serif;
      font-size: 0.65rem;
      font-weight: 700;
      display: flex;
      align-items: center;
      justify-content: center;
      padding: 0 4px;
      line-height: 1;
      box-shadow: 0 0 6px rgba(239, 68, 68, 0.6);
    }
    .close-btn {
      border: none;
      background: transparent;
      color: rgba(255, 255, 255, 0.3);
      font-size: 0.9rem;
      cursor: pointer;
      padding: 4px;
      transition: color 0.2s;
      line-height: 1;
    }
    .close-btn:hover {
      color: rgba(255, 255, 255, 0.7);
    }
    .panel-body {
      flex: 1;
      overflow: hidden;
    }
  `],
  changeDetection: ChangeDetectionStrategy.OnPush
})
export class SidePanelComponent {
  readonly isOpen = input(false);
  readonly activeTab = input<'chat' | 'history'>('chat');
  readonly newMessageCount = input(0);
  readonly chatMessages = input<ChatMessage[]>([]);
  readonly gameHistory = input<GameHistoryEvent[]>([]);
  readonly currentUserId = input<string>('');

  readonly sendMessage = output<ChatMessage>();
  readonly toggle = output<boolean>();

  protected onSendMessage(msg: ChatMessage): void {
    this.sendMessage.emit(msg);
  }
}