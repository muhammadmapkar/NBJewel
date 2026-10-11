// Role: A small dialog for moving an item to its next status. Status can only
// move forward one step, so it offers just that step. It only collects the
// choice. The detail page makes the API call and reports back any error.

import { Component, computed, input, output } from '@angular/core';
import { nextStatus } from './catalog-filters.model';
import { StatusPillComponent } from './status-pill.component';

@Component({
  selector: 'nb-status-dialog', standalone: true,
  imports: [StatusPillComponent],
  host: { '(document:keydown.escape)': 'cancel.emit()' },
  template: `
    <div class="backdrop" (click)="cancel.emit()">
      <div class="dialog" role="dialog" aria-modal="true" aria-labelledby="status-title" (click)="$event.stopPropagation()">
        <h2 id="status-title">Change status</h2>
        @if (next(); as n) {
          <p class="move">Move this item from <nb-status-pill [status]="current()" /> to <nb-status-pill [status]="n" />?</p>
          <p class="hint">Status can only move forward one step at a time.</p>
        } @else {
          <p class="move">This item is {{ current() }}. There is no later status.</p>
        }
        @if (error()) { <p class="error" role="alert">{{ error() }}</p> }
        <div class="buttons">
          @if (next(); as n) {
            <button type="button" class="save" [disabled]="saving()" (click)="confirm.emit(n)">{{ saving() ? 'Saving...' : 'Move to ' + n }}</button>
          }
          <button type="button" class="cancel" [disabled]="saving()" (click)="cancel.emit()">{{ next() ? 'Cancel' : 'Close' }}</button>
        </div>
      </div>
    </div>
  `,
  styles: [`
    .backdrop { position:fixed; inset:0; z-index:60; display:grid; place-items:center; padding:16px; background:#0006; }
    .dialog { width:100%; max-width:380px; padding:20px; border-radius:8px; background:white; box-shadow:0 12px 36px #0003; }
    h2 { margin:0 0 12px; font-size:18px; color:var(--gold); }
    .move { margin:0 0 8px; line-height:1.8; }
    .hint { margin:0 0 12px; color:var(--muted); font-size:12px; }
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

  readonly next = computed(() => nextStatus(this.current()));
}
