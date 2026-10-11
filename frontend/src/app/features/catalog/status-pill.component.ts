// Role: Shows an item status (Draft, With Smith, Received, Complete) as a colored badge.

import { Component, computed, input } from '@angular/core';

const KNOWN = ['draft', 'with-smith', 'received', 'complete'];

@Component({
  selector: 'nb-status-pill', standalone: true,
  template: `<span class="pill" [class]="tone()">{{ status() }}</span>`,
  styles: [`
    .pill { display:inline-block; padding:3px 10px; border-radius:12px; font-size:12px; font-weight:700; white-space:nowrap; }
    .draft { color:var(--muted); background:#ece9df; }
    .with-smith { color:#8a6100; background:var(--widget); }
    .received { color:#1d4f8a; background:#dbe8f7; }
    .complete { color:#1e6b3a; background:#dff3e5; }
    .other { color:var(--danger); background:#f8dedb; }
  `]
})
export class StatusPillComponent {
  readonly status = input.required<string>();
  // An unexpected status still shows, in the "other" color, so it is never hidden.
  readonly tone = computed(() => {
    const value = this.status().trim().toLowerCase().replace(/\s+/g, '-');
    return KNOWN.includes(value) ? value : 'other';
  });
}
