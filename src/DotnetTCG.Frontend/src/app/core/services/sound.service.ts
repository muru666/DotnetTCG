import { Injectable } from '@angular/core';

type UiSoundType = 'clicked' | 'error';

const UI_SOUND_FILES: Record<UiSoundType, string[]> = {
  clicked: ['/sounds/sfx/ui/clicked/clicked_button1.mp3'],
  error: ['/sounds/sfx/ui/error/error1.mp3']
};

const INTERACTIVE_SELECTOR = 'button, [role="button"], a.btn';

@Injectable({
  providedIn: 'root'
})
export class SoundService {
  private listenersAttached = false;

  public attachGlobalListeners(): void {
    if (this.listenersAttached) {
      return;
    }

    document.addEventListener('click', event => this.handleInteractiveEvent(event), true);
    this.listenersAttached = true;
  }

  public playError(): void {
    this.playFile(UI_SOUND_FILES.error[0]);
  }

  private handleInteractiveEvent(event: Event): void {
    if (!(event.target instanceof HTMLElement)) {
      return;
    }
    const button = event.target.closest<HTMLElement>(INTERACTIVE_SELECTOR);
    if (button === null || (button as HTMLButtonElement).disabled) {
      return;
    }

    this.playFile(UI_SOUND_FILES.clicked[0]);
  }

  private playFile(file: string): void {
    const audio = new Audio(file);
    audio.play().catch(() => {});
  }
}
