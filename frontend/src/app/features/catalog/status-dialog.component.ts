// Role: A small dialog for choosing an item status. All four statuses are
// shown. The current one is marked and disabled. Moving to an earlier status
// (backward) asks for confirmation first. It only collects the choice. The
// detail page makes the API call and reports back any error.

import { Component, computed, effect, input, output, signal } from '@angular/core';
import { isBackward, ITEM_STATUSES } from './catalog-filters.model';

@Component({
  selector: 'nb-status-dialog', standalone: true,
  host: { '(document:keydown.escape)': 'cancel.emit()' },
  template: `
    <div class="backdrop" (click)="cancel.emit()">
      <div class="dialog" role="dialog" aria-modal="true" aria-labelledby="status-title" (click)="$event.stopPropagation()">
        <h2 id="status-title">Change status</h2>
        @if (confirming()) {
          <p class="warn" role="alert">Move this item back from {{ current() }} to {{ selected() }}?</p>
        } @else {
          <fieldset [disabled]="saving()">
            @for (s of statuses; track s) {
              <label class="option" [class.is-current]="s === current()">
                <input type="radio" name="status" [value]="s" [disabled]="s === current()" [checked]="selected() === s" (change)="selected.set(s)">
                {{ s }}@if (s === current()) { <span class="current-mark"> (current)</span> }
              </label>
            }
          </fieldset>
        }
        @if (error()) { <p class="error" role="alert">{{ error() }}</p> }
        <div class="buttons">
          @if (confirming()) {
            <button type="button" class="save" [disabled]="saving()" (click)="confirm.emit(selected())">{{ saving() ? 'Saving...' : 'Yes, move back' }}</button>
            <button type="button" class="cancel" [disabled]="saving()" (click)="confirming.set(false)">No, go back</button>
          } @else {
            <button type="button" class="save" [disabled]="saving() || !canSave()" (click)="save()">{{ saving() ? 'Saving...' : 'Save' }}</button>
            <button type="button" class="cancel" [disabled]="saving()" (click)="cancel.emit()">Cancel</button>
          }
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
    .option.is-current { color:var(--muted); }
    .current-mark { font-size:12px; }
    .warn { margin:0 0 12px; line-height:1.5; }
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
  readonly confirming = signal(false);
  readonly canSave = computed(() => this.selected() !== '' && this.selected() !== this.current());

  constructor() {
    effect(() => this.selected.set(this.current()));
  }

  // A backward move needs a second click on "Yes, move back".
  save(): void {
    if (isBackward(this.current(), this.selected())) { this.confirming.set(true); return; }
    this.confirm.emit(this.selected());
  }
}
