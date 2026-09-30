// Role: Shows an item status (Active, Draft, Discontinued) as a colored pill.

import { Component, computed, input } from '@angular/core';

@Component({
  selector: 'nb-status-pill', standalone: true,
  template: `<span class="pill" [class]="tone()">{{ status() }}</span>`,
  styles: [`
    .pill { display:inline-block; padding:3px 10px; border-radius:12px; font-size:12px; font-weight:700; white-space:nowrap; }
    .active { color:#1e6b3a; background:#dff3e5; }
    .draft { color:#8a6100; background:var(--widget); }
    .discontinued { color:var(--danger); background:#f8dedb; }
    .other { color:var(--muted); background:#ece9df; }
  `]
})
export class StatusPillComponent {
  readonly status = input.required<string>();
  readonly tone = computed(() => {
    const value = this.status().toLowerCase();
    return value === 'active' || value === 'draft' || value === 'discontinued' ? value : 'other';
  });
}
