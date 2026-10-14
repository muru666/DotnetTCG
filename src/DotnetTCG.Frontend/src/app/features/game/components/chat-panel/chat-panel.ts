import { ChangeDetectionStrategy, Component, input, output, signal } from '@angular/core';
import { FormsModule } from '@angular/forms';
import { ChatMessage } from '../../../../shared/models/chat/chat-message.model';
import { MessageItemComponent } from '../message-item/message-item';

@Component({
  selector: 'app-chat-panel',
  imports: [FormsModule, MessageItemComponent],
  template: `
    <div class="chat-container">
      <div class="chat-messages">
        @for (msg of messages(); track msg.id) {
          <app-message-item [message]="msg" [isOwn]="msg.senderId === currentUserId() && currentUserId() !== ''" />
        }
        @if (messages().length === 0) {
          <div class="chat-empty">No hay mensajes aún</div>
        }
      </div>
      @if (!readOnly()) {
        <div class="chat-input-area">
          <input
            class="chat-input"
            [(ngModel)]="newMessage"
            (keyup.enter)="send()"
            placeholder="Escribe un mensaje..."
            maxlength="500"
          />
          <button class="chat-send-btn" (click)="send()" [disabled]="!newMessage().trim()">
            Enviar
          </button>
        </div>
      }
    </div>
  `,
  styles: [`
    .chat-container {
      display: flex;
      flex-direction: column;
      height: 100%;
    }
    .chat-messages {
      flex: 1;
      overflow-y: auto;
      padding: 8px 0;
    }
    .chat-empty {
      text-align: center;
      color: rgba(255, 255, 255, 0.2);
      font-family: 'Nunito', sans-serif;
      font-size: 0.8rem;
      padding: 40px 0;
    }
    .chat-input-area {
      display: flex;
      gap: 6px;
      padding: 10px 12px;
      border-top: 1px solid rgba(255, 255, 255, 0.06);
    }
    .chat-input {
      flex: 1;
      border: 1px solid rgba(255, 255, 255, 0.1);
      border-radius: 6px;
      padding: 8px 10px;
      font-size: 0.8rem;
      font-family: 'Nunito', sans-serif;
      outline: none;
      background: rgba(0, 0, 0, 0.2);
      color: #e2e8f0;
    }
    .chat-input:focus {
      border-color: #f59e0b;
    }
    .chat-input::placeholder {
      color: rgba(255, 255, 255, 0.25);
    }
    .chat-send-btn {
      background: #f59e0b;
      color: #0f172a;
      border: none;
      border-radius: 6px;
      padding: 8px 14px;
      font-size: 0.75rem;
      font-family: 'Nunito', sans-serif;
      font-weight: 700;
      cursor: pointer;
      transition: background 0.2s;
    }
    .chat-send-btn:hover:not(:disabled) {
      background: #d97706;
    }
    .chat-send-btn:disabled {
      opacity: 0.4;
      cursor: default;
    }
  `],
  changeDetection: ChangeDetectionStrategy.OnPush
})
export class ChatPanelComponent {
  readonly messages = input<ChatMessage[]>([]);
  readonly currentUserId = input<string>('');
  readonly readOnly = input(false);
  readonly sendMessage = output<ChatMessage>();

  protected newMessage = signal('');

  protected send(): void {
    if (this.readOnly()) return;
    const text = this.newMessage().trim();
    if (!text) return;

    this.sendMessage.emit({
      id: crypto.randomUUID(),
      senderId: '',
      senderName: 'Tú',
      content: text,
      timestamp: new Date()
    });
    this.newMessage.set('');
  }
}
