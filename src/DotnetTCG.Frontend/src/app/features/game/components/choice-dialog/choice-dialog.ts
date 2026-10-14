import { ChangeDetectionStrategy, Component, effect, input, output, signal } from '@angular/core';
import { NgOptimizedImage } from '@angular/common';
import { PendingChoice } from '../../../../shared/models/dtos/game-state.model';

@Component({
  selector: 'app-choice-dialog',
  imports: [NgOptimizedImage],
  template: `
    @if (pendingChoice(); as pc) {
      <div class="choice-overlay" (click)="onCancel()">
        <div class="choice-dialog" (click)="$event.stopPropagation()">
          <div class="choice-prompt">{{ pc.promptLabel }}</div>
          <div class="choice-options">
            @for (opt of pc.options; track opt.optionId) {
              <button class="choice-option" [class.selected]="selectedIds().has(opt.optionId)"
                (click)="onToggle(opt.optionId)">
                @if (opt.imageUrl) {
                  <img [ngSrc]="opt.imageUrl" [alt]="opt.label" class="choice-option-img" width="36" height="50">
                }
                <span class="choice-option-label">{{ opt.label }}</span>
                <span class="choice-check">@if (selectedIds().has(opt.optionId)) { ✓ }</span>
              </button>
            }
          </div>
          <div class="choice-actions">
            <button class="choice-confirm" [class.btn-disabled]="selectedIds().size === 0"
              [disabled]="selectedIds().size === 0" (click)="onConfirm(pc.choiceId)">Confirmar</button>
            <button class="choice-cancel" (click)="onCancel()">Cancelar</button>
          </div>
        </div>
      </div>
    }
  `,
  styles: [`
    .choice-overlay {
      position: fixed; inset: 0; z-index: 1000;
      background: rgba(0,0,0,0.6); backdrop-filter: blur(2px);
      display: flex; align-items: center; justify-content: center;
    }
    .choice-dialog {
      background: #1a2340; border: 1px solid rgba(99,102,241,0.3);
      border-radius: 12px; padding: 20px; min-width: 280px; max-width: 400px;
      box-shadow: 0 8px 32px rgba(0,0,0,0.5);
    }
    .choice-prompt {
      font-family: 'Nunito', sans-serif; font-size: 0.9rem; font-weight: 700;
      color: #e2e8f0; margin-bottom: 14px; text-align: center;
    }
    .choice-options { display: flex; flex-direction: column; gap: 6px; margin-bottom: 10px; max-height: 300px; overflow-y: auto; }
    .choice-option {
      border: 1px solid rgba(99,102,241,0.3); border-radius: 8px;
      padding: 8px 12px; background: rgba(99,102,241,0.1);
      color: #a5b4fc; font-family: 'Nunito', sans-serif; font-size: 0.8rem;
      font-weight: 600; cursor: pointer; transition: all 0.15s; text-align: left;
      display: flex; align-items: center; gap: 10px;
    }
    .choice-option:hover { background: rgba(99,102,241,0.25); border-color: rgba(99,102,241,0.6); }
    .choice-option.selected { background: rgba(99,102,241,0.3); border-color: #6366f1; }
    .choice-option-img { width: 36px; height: 50px; object-fit: cover; border-radius: 3px; flex-shrink: 0; }
    .choice-option-label { flex: 1; }
    .choice-check { font-size: 1rem; color: #6366f1; font-weight: 700; flex-shrink: 0; width: 16px; text-align: center; }
    .choice-actions { display: flex; flex-direction: column; gap: 6px; }
    .choice-confirm {
      width: 100%; border: none; border-radius: 8px; padding: 8px;
      background: #6366f1; color: #fff;
      font-family: 'Nunito', sans-serif; font-size: 0.8rem; font-weight: 700; cursor: pointer;
    }
    .choice-confirm:hover { background: #4f46e5; }
    .choice-confirm.btn-disabled { opacity: 0.35; cursor: not-allowed; }
    .choice-cancel {
      width: 100%; border: none; border-radius: 8px; padding: 8px;
      background: rgba(255,255,255,0.05); color: rgba(255,255,255,0.4);
      font-family: 'Nunito', sans-serif; font-size: 0.75rem; cursor: pointer;
    }
    .choice-cancel:hover { background: rgba(255,255,255,0.1); }
  `],
  changeDetection: ChangeDetectionStrategy.OnPush
})
export class ChoiceDialogComponent {
  readonly pendingChoice = input<PendingChoice | null>(null);
  readonly resolve = output<{ choiceId: string; selectedOptionIds: string[] }>();
  readonly cancel = output<void>();

  protected readonly selectedIds = signal(new Set<string>());

  constructor() {
    effect(() => {
      this.pendingChoice();
      this.selectedIds.set(new Set());
    });
  }

  protected onToggle(optionId: string): void {
    const next = new Set(this.selectedIds());
    if (next.has(optionId)) {
      next.delete(optionId);
    } else {
      next.add(optionId);
    }
    this.selectedIds.set(next);
  }

  protected onConfirm(choiceId: string): void {
    if (this.selectedIds().size === 0) return;
    this.resolve.emit({ choiceId, selectedOptionIds: [...this.selectedIds()] });
  }

  protected onCancel(): void {
    this.cancel.emit();
  }
}
