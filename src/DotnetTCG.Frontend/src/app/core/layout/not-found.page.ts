import { Component } from '@angular/core';
import { RouterLink } from '@angular/router';

@Component({
  imports: [RouterLink],
  template: `
    <section class="not-found">
      <h1>Página no encontrada</h1>
      <p>La ruta que buscás no existe.</p>
      <a routerLink="/decks">Ir a mis mazos</a>
    </section>
  `,
  styles: [
    `
      .not-found {
        text-align: center;
        padding: 3rem 1rem;
      }
      .not-found p {
        color: var(--text-secondary);
      }
    `,
  ],
})
export class NotFoundPage {}
