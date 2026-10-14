import { Component, input, output } from '@angular/core';
import { FormControl, FormGroup, ReactiveFormsModule, Validators } from '@angular/forms';
import { ChatMessageDto } from '../models/game.models';

export interface ChatLine {
  readonly label: string;
  readonly text: string;
  readonly at: string;
}

@Component({
  selector: 'app-side-panel',
  imports: [ReactiveFormsModule],
  template: `
    <div class="side">
      <section aria-label="Registro">
        <h2>Registro</h2>
        <ol class="log">
          @for (line of log(); track $index) {
            <li><span class="turn">T{{ line.turn }}</span> {{ line.text }}</li>
          } @empty {
            <li class="empty">Sin eventos todavía.</li>
          }
        </ol>
      </section>
      <section aria-label="Chat">
        <h2>Chat</h2>
        <ol class="chat">
          @for (message of chat(); track $index) {
            <li><strong>{{ label(message) }}:</strong> {{ message.text }}</li>
          } @empty {
            <li class="empty">Sin mensajes.</li>
          }
        </ol>
        <form [formGroup]="form" (ngSubmit)="submitChat()">
          <label class="visually-hidden" for="chat-input">Mensaje</label>
          <input id="chat-input" type="text" formControlName="text" maxlength="500" placeholder="Escribí…" />
          <button type="submit" [disabled]="form.invalid">Enviar</button>
        </form>
      </section>
    </div>
  `,
  styles: [
    `
      .side {
        display: flex;
        flex-direction: column;
        gap: 1rem;
        background: var(--surface-1);
        border: 1px solid var(--surface-border);
        border-radius: var(--radius-md);
        padding: 0.75rem;
        max-height: 100%;
        overflow-y: auto;
      }
      h2 {
        margin: 0 0 0.5rem;
        font-size: 0.9rem;
        color: var(--text-secondary);
      }
      .log,
      .chat {
        list-style: none;
        margin: 0 0 0.75rem;
        padding: 0;
        display: flex;
        flex-direction: column;
        gap: 0.3rem;
        font-size: 0.85rem;
      }
      .turn {
        color: var(--text-muted);
      }
      .empty {
        color: var(--text-muted);
      }
      form {
        display: flex;
        gap: 0.4rem;
      }
      form input {
        flex: 1;
      }
      form button {
        background: var(--accent);
        color: var(--accent-text);
        border: none;
        border-radius: var(--radius-sm);
        padding: 0.5rem 0.8rem;
      }
      .visually-hidden {
        position: absolute;
        width: 1px;
        height: 1px;
        overflow: hidden;
        clip: rect(0 0 0 0);
      }
    `,
  ],
})
export class SidePanelComponent {
  readonly log = input<readonly { turn: number; text: string }[]>([]);
  readonly chat = input<readonly ChatMessageDto[]>([]);
  readonly labelOf = input<(senderId: string) => string>((id) => id);

  readonly sendMessage = output<string>();

  protected readonly form = new FormGroup({
    text: new FormControl('', {
      nonNullable: true,
      validators: [Validators.required, Validators.maxLength(500)],
    }),
  });

  protected label(message: ChatMessageDto): string {
    return this.labelOf()(message.senderId);
  }

  protected submitChat(): void {
    if (this.form.invalid) {
      return;
    }
    const text = this.form.getRawValue().text.trim();
    this.form.reset();
    if (text.length > 0) {
      this.sendMessage.emit(text);
    }
  }
}
