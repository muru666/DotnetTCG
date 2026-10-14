import { Component, inject, input, output } from '@angular/core';
import { resource } from '@angular/core';
import { GameSession, TargetRef } from '../data-access/game-session';
import { FieldPokemonDto } from '../models/game.models';

@Component({
  selector: 'app-ability-buttons',
  template: `
    @if (abilities(); as list) {
      @if (list.length > 0) {
        <div class="abilities">
          @for (ability of list; track ability) {
            <button type="button" [disabled]="!enabled()" (click)="fire(ability)">
              {{ ability }}
            </button>
          }
        </div>
      }
    }
  `,
  styles: [
    `
      .abilities {
        display: flex;
        gap: 0.3rem;
        flex-wrap: wrap;
        margin-top: 0.3rem;
      }
      button {
        background: var(--surface-3);
        color: var(--text-primary);
        border: 1px solid var(--surface-border);
        border-radius: var(--radius-sm);
        padding: 0.3rem 0.55rem;
        font-size: 0.8rem;
      }
      button:hover:not(:disabled) {
        border-color: var(--accent);
      }
    `,
  ],
})
export class AbilityButtonsComponent {
  private readonly session = inject(GameSession);

  readonly pokemon = input<FieldPokemonDto | null>(null);
  readonly userRef = input<TargetRef | null>(null);
  readonly enabled = input(false);

  readonly use = output<{ abilityName: string; user: TargetRef }>();

  protected fire(ability: string): void {
    const user = this.userRef();
    if (user !== null) {
      this.use.emit({ abilityName: ability, user });
    }
  }

  protected readonly abilities = resource({
    params: () => this.pokemon()?.cardId ?? null,
    loader: async ({ params }) => {
      if (params === null) {
        return [];
      }
      const card = await this.session.card(params);
      return card?.abilities.map((ability) => ability.name) ?? [];
    },
  }).value;
}
