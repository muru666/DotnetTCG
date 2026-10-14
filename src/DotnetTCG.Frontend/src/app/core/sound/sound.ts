import { Injectable, signal } from '@angular/core';

const MUTE_KEY = 'dotnettcp.muted';

const CLICK_URL = '/sounds/sfx/ui/clicked/clicked_button1.mp3';
const ERROR_URL = '/sounds/sfx/ui/error/error1.mp3';

/** Efectos UI del proyecto anterior (sin música). Respetan el mute persistido. */
@Injectable({ providedIn: 'root' })
export class SoundService {
  private readonly mutedState = signal(
    typeof localStorage !== 'undefined' && localStorage.getItem(MUTE_KEY) === '1',
  );

  readonly muted = this.mutedState.asReadonly();

  toggleMute(): void {
    const next = !this.mutedState();
    this.mutedState.set(next);
    try {
      localStorage.setItem(MUTE_KEY, next ? '1' : '0');
    } catch {
      // Almacenamiento no disponible: el mute vive solo en memoria.
    }
  }

  playClick(): void {
    void this.play(CLICK_URL);
  }

  playError(): void {
    void this.play(ERROR_URL);
  }

  private async play(url: string): Promise<void> {
    if (this.mutedState()) {
      return;
    }
    try {
      await new Audio(url).play();
    } catch {
      // Autoplay bloqueado o archivo ausente: efecto opcional, se ignora.
    }
  }
}
