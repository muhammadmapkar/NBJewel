// Role: Shows a short success message in the corner of the screen and hides
// itself after a few seconds or when the user closes it.

import { Component, effect, input, output } from '@angular/core';

@Component({
  selector: 'nb-toast', standalone: true,
  template: `
    @if (message()) {
      <div class="toast" role="status">
        <span>{{ message() }}</span>
        <button type="button" aria-label="Close message" (click)="dismissed.emit()">×</button>
      </div>
    }
  `,
  styles: [`
    .toast { position:fixed; top:74px; right:20px; z-index:50; display:flex; align-items:center; gap:14px; max-width:340px; padding:12px 16px; border-radius:8px; color:#1e6b3a; background:#dff3e5; border:1px solid #9bd3ac; box-shadow:0 8px 24px #0002; font-weight:600; }
    button { border:0; background:transparent; color:inherit; font-size:20px; line-height:1; }
  `]
})
export class ToastComponent {
  readonly message = input<string | null>(null);
  readonly dismissed = output<void>();

  constructor() {
    effect(onCleanup => {
      if (!this.message()) return;
      const timer = setTimeout(() => this.dismissed.emit(), 4000);
      onCleanup(() => clearTimeout(timer));
    });
  }
}
