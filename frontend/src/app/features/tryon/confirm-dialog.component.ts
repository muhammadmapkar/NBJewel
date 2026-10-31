// Role: A small yes or no dialog used before deleting a try-on jewelry item. It copies the
// look of the catalog status dialog (same backdrop, card and button styles). Escape cancels.

import { Component, input, output } from '@angular/core';

@Component({
  selector: 'nb-confirm-dialog', standalone: true,
  host: { '(document:keydown.escape)': 'busy() || cancel.emit()' },
  template: `
    <div class="backdrop" (click)="cancel.emit()">
      <div class="dialog" role="alertdialog" aria-modal="true" [attr.aria-label]="title()" (click)="$event.stopPropagation()">
        <h2>{{ title() }}</h2>
        <p class="warn">{{ message() }}</p>
        @if (error()) { <p class="error">{{ error() }}</p> }
        <div class="buttons">
          <button type="button" class="save" [disabled]="busy()" (click)="confirm.emit()">{{ busy() ? 'Working...' : confirmLabel() }}</button>
          <button type="button" class="cancel" [disabled]="busy()" (click)="cancel.emit()">Cancel</button>
        </div>
      </div>
    </div>
  `,
  styles: [`
    .backdrop { position:fixed; inset:0; z-index:60; display:grid; place-items:center; padding:16px; background:#0006; }
    .dialog { width:100%; max-width:340px; padding:20px; border-radius:8px; background:white; box-shadow:0 12px 36px #0003; }
    h2 { margin:0 0 12px; font-size:18px; color:var(--gold); }
    .warn { margin:0 0 12px; line-height:1.5; }
    .error { margin:0 0 12px; color:var(--danger); font-size:12px; }
    .buttons { display:flex; gap:10px; }
    .save { min-height:38px; padding:0 20px; border:0; border-radius:6px; background:var(--button); color:#333; font-weight:700; }
    .cancel { min-height:38px; padding:0 20px; border:1px solid var(--gold); border-radius:6px; background:white; color:var(--gold); font-weight:700; }
    button:disabled { opacity:.5; cursor:default; }
  `]
})
export class ConfirmDialogComponent {
  readonly title = input('Are you sure?');
  readonly message = input('');
  readonly confirmLabel = input('Yes');
  readonly busy = input(false);
  readonly error = input<string | null>(null);
  readonly confirm = output<void>();
  readonly cancel = output<void>();
}
