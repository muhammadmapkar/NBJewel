// Role: A small dialog for choosing a new item status. It only collects the
// choice. The detail page makes the API call and reports back any error.

import { Component, effect, input, output, signal } from '@angular/core';
import { ITEM_STATUSES } from './catalog-filters.model';

@Component({
  selector: 'nb-status-dialog', standalone: true,
  host: { '(document:keydown.escape)': 'cancel.emit()' },
  template: `
    <div class="backdrop" (click)="cancel.emit()">
      <div class="dialog" role="dialog" aria-modal="true" aria-labelledby="status-title" (click)="$event.stopPropagation()">
        <h2 id="status-title">Change status</h2>
        <fieldset [disabled]="saving()">
          @for (s of statuses; track s) {
            <label class="option">
              <input type="radio" name="status" [value]="s" [checked]="selected() === s" (change)="selected.set(s)"> {{ s }}
            </label>
          }
        </fieldset>
        @if (error()) { <p class="error" role="alert">{{ error() }}</p> }
        <div class="buttons">
          <button type="button" class="save" [disabled]="saving() || selected() === current()" (click)="confirm.emit(selected())">{{ saving() ? 'Saving...' : 'Save' }}</button>
          <button type="button" class="cancel" [disabled]="saving()" (click)="cancel.emit()">Cancel</button>
        </div>
      </div>
    </div>
  `,
  styles: [`
    .backdrop { position:fixed; inset:0; z-index:60; display:grid; place-items:center; padding:16px; background:#0006; }
    .dialog { width:100%; max-width:340px; padding:20px; border-radius:8px; background:white; box-shadow:0 12px 36px #0003; }
    h2 { margin:0 0 12px; font-size:18px; color:var(--gold); }
    fieldset { border:0; padding:0; margin:0 0 12px; display:grid; gap:10px; }
    .option { display:flex; align-items:center; gap:8px; }
    .option input { accent-color:var(--gold); }
    .error { margin:0 0 12px; color:var(--danger); font-size:12px; }
    .buttons { display:flex; gap:10px; }
    .save { min-height:38px; padding:0 20px; border:0; border-radius:6px; background:var(--button); color:#333; font-weight:700; }
    .cancel { min-height:38px; padding:0 20px; border:1px solid var(--gold); border-radius:6px; background:white; color:var(--gold); font-weight:700; }
    button:disabled { opacity:.5; cursor:default; }
  `]
})
export class StatusDialogComponent {
  readonly current = input.required<string>();
  readonly saving = input(false);
  readonly error = input<string | null>(null);
  readonly confirm = output<string>();
  readonly cancel = output<void>();

  readonly statuses = ITEM_STATUSES;
  readonly selected = signal('');

  constructor() {
    effect(() => this.selected.set(this.current()));
  }
}
