import { ChangeDetectionStrategy, Component, computed, input, output, signal, OnInit, OnDestroy } from '@angular/core';

@Component({
  selector: 'app-coin-flip',
  template: `
    <div class="coin-flip-overlay" (click)="onDismiss()">
      <div class="coin-flip-card" (click)="$event.stopPropagation()">

        <div class="coin-flip-label">{{ displayLabel() }}</div>

        <div class="pokeball-wrapper" [class.spinning]="isFlipping()" [class.result]="!isFlipping()">
          <div class="pokeball-glow"></div>
          <div class="pokeball-coin">
            <div class="pokeball-top"></div>
            <div class="pokeball-line"></div>
            <div class="pokeball-bottom"></div>
            <div class="pokeball-center-outside"></div>
            <div class="pokeball-center-button"></div>
          </div>
        </div>

        @if (showResult()) {
          <div class="coin-result">
            <div class="result-label" [class.heads-result]="isHeads()" [class.tails-result]="!isHeads()">
              {{ resultText() }}
            </div>
            @if (setup()) {
              <p class="result-subtitle">{{ resultSubtitle() }}</p>
            }
          </div>
        }

      </div>
    </div>
  `,
  styles: [`
    .coin-flip-overlay {
      position: fixed;
      inset: 0;
      background: rgba(0, 0, 0, 0.85);
      display: flex;
      align-items: center;
      justify-content: center;
      z-index: 900;
      backdrop-filter: blur(6px);
      animation: fadeIn 0.4s ease-out;
    }
    @keyframes fadeIn {
      from { opacity: 0; }
      to { opacity: 1; }
    }
    .coin-flip-card {
      background: linear-gradient(180deg, #1a1a2e 0%, #16213e 100%);
      border: 2px solid rgba(245, 158, 11, 0.3);
      border-radius: 16px;
      padding: 32px 40px;
      text-align: center;
      max-width: 360px;
      width: 90%;
      box-shadow: 0 16px 48px rgba(0, 0, 0, 0.5);
      animation: cardIn 0.5s ease-out;
    }
    @keyframes cardIn {
      from { opacity: 0; transform: translateY(20px); }
      to { opacity: 1; transform: translateY(0); }
    }
    .coin-flip-label {
      font-family: 'Nunito', sans-serif;
      font-size: 0.85rem;
      color: #e2e8f0;
      margin-bottom: 24px;
      letter-spacing: 1px;
    }

    .pokeball-wrapper {
      position: relative;
      width: 140px;
      height: 140px;
      margin: 0 auto 20px;
      perspective: 600px;
    }
    .pokeball-glow {
      position: absolute;
      inset: -30px;
      border-radius: 50%;
      background: radial-gradient(circle, rgba(245, 158, 11, 0.25), transparent 70%);
      opacity: 0.4;
      animation: glowPulse 0.6s ease-in-out infinite alternate;
      pointer-events: none;
    }
    @keyframes glowPulse {
      from { opacity: 0.3; transform: scale(0.95); }
      to { opacity: 0.7; transform: scale(1.15); }
    }
    .pokeball-coin {
      width: 140px;
      height: 140px;
      border-radius: 50%;
      position: relative;
      overflow: hidden;
      border: 4px solid #222;
      box-shadow: 0 8px 32px rgba(0, 0, 0, 0.6), 0 0 20px rgba(245, 158, 11, 0.15);
      transform-style: preserve-3d;
    }
    .spinning .pokeball-coin {
      animation: pokeballSpin 0.8s linear infinite;
    }
    .result .pokeball-coin {
      animation: pokeballLand 0.4s ease-out forwards;
    }
    @keyframes pokeballSpin {
      0% { transform: rotateX(0deg) scale(1); }
      100% { transform: rotateX(360deg) scale(1); }
    }
    @keyframes pokeballLand {
      0% { transform: scale(1.15); }
      50% { transform: scale(0.92); }
      75% { transform: scale(1.04); }
      100% { transform: scale(1); }
    }
    .pokeball-top {
      position: absolute; top: 0; left: 0; right: 0; height: 50%;
      background: linear-gradient(180deg, #ff2222 0%, #cc0000 60%, #aa0000 100%);
      border-radius: 70px 70px 0 0;
    }
    .pokeball-top::before {
      content: ''; position: absolute; top: 12px; left: 18px;
      width: 30px; height: 16px; background: rgba(255,255,255,0.25);
      border-radius: 50%; transform: rotate(-25deg);
    }
    .pokeball-line {
      position: absolute; top: 50%; left: 0; right: 0; height: 8px;
      transform: translateY(-50%); background: #222; z-index: 2;
    }
    .pokeball-bottom {
      position: absolute; bottom: 0; left: 0; right: 0; height: 50%;
      background: linear-gradient(180deg, #f0f0f0 0%, #e0e0e0 50%, #d0d0d0 100%);
      border-radius: 0 0 70px 70px;
    }
    .pokeball-center-outside {
      position: absolute; top: 50%; left: 50%;
      transform: translate(-50%, -50%); width: 32px; height: 32px;
      border-radius: 50%; background: #f5f5f5; border: 4px solid #222; z-index: 3;
      box-shadow: 0 2px 6px rgba(0,0,0,0.3);
    }
    .pokeball-center-button {
      position: absolute; top: 50%; left: 50%;
      transform: translate(-50%, -50%); width: 14px; height: 14px;
      border-radius: 50%;
      background: radial-gradient(circle, #ffffff 30%, #ddd 70%, #bbb 100%);
      border: 2px solid #555; z-index: 4;
    }

    .coin-result {
      margin-bottom: 12px;
    }

    .result-label {
      font-family: 'Bungee', sans-serif;
      font-size: 1.8rem;
      letter-spacing: 4px;
      text-shadow: 0 2px 8px rgba(0, 0, 0, 0.5);
    }

    .heads-result {
      color: #fbbf24;
    }

    .tails-result {
      color: #94a3b8;
    }

    .result-subtitle {
      font-family: 'Nunito', sans-serif;
      font-size: 0.85rem;
      color: rgba(255, 255, 255, 0.6);
      margin: 8px 0 0;
    }


  `],
  changeDetection: ChangeDetectionStrategy.OnPush
})
export class CoinFlipComponent implements OnInit, OnDestroy {
  readonly result = input.required<'HEADS' | 'TAILS'>();
  readonly label = input('Lanzamiento de moneda');
  readonly setup = input(false);
  readonly playerName = input('');
  readonly opponentName = input('');

  readonly dismiss = output<void>();

  protected readonly isFlipping = signal(true);
  protected readonly showResult = signal(false);

  protected readonly isHeads = computed(() => this.result() === 'HEADS');

  protected readonly displayLabel = computed(() => {
    if (this.setup()) {
      return this.label() || 'Decidiendo quien empieza...';
    }
    return this.label();
  });

  protected readonly resultText = computed(() => {
    if (this.setup()) {
      return this.isHeads() ? '¡EMPIEZAS TÚ!' : 'Empieza el oponente';
    }
    return this.isHeads() ? 'CARA' : 'CRUZ';
  });

  protected readonly resultSubtitle = computed(() => {
    if (this.setup()) {
      return this.isHeads()
        ? `${this.playerName()} juega primero este turno`
        : `${this.opponentName()} juega primero este turno`;
    }
    return '';
  });

  private spinTimer: ReturnType<typeof setTimeout> | null = null;
  private autoDismissTimer: ReturnType<typeof setTimeout> | null = null;

  ngOnInit(): void {
    const flipDuration = 1200 + Math.random() * 600;
    this.spinTimer = setTimeout(() => {
      this.isFlipping.set(false);
      this.showResult.set(true);

      this.autoDismissTimer = setTimeout(() => {
        this.dismiss.emit();
      }, 2000);
    }, flipDuration);
  }

  ngOnDestroy(): void {
    if (this.spinTimer) clearTimeout(this.spinTimer);
    if (this.autoDismissTimer) clearTimeout(this.autoDismissTimer);
  }

  protected onDismiss(): void {
    if (this.showResult()) {
      if (this.autoDismissTimer) { clearTimeout(this.autoDismissTimer); this.autoDismissTimer = null; }
      if (this.spinTimer) { clearTimeout(this.spinTimer); this.spinTimer = null; }
      this.dismiss.emit();
    }
  }
}
