import { Component } from '@angular/core';
import { AppShell } from './core/layout/app-shell';

@Component({
  imports: [AppShell],
  selector: 'app-root',
  template: `<app-shell />`,
  styles: [
    `
      :host {
        display: block;
        min-height: 100dvh;
      }
    `,
  ],
})
export class App {}
