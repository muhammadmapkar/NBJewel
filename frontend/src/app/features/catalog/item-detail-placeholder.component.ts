// Role: Temporary page for /catalog/:id until the real detail screen is built in part B.

import { Component, inject } from '@angular/core';
import { ActivatedRoute, RouterLink } from '@angular/router';

@Component({
  selector: 'nb-item-detail-placeholder', standalone: true, imports: [RouterLink],
  template: `
    <div class="card">
      <h1>Item {{ id }}</h1>
      <p>The item detail page is coming soon.</p>
      <a routerLink="/catalog">Back to Item Catalog</a>
    </div>
  `,
  styles: [`
    .card { background:white; padding:24px 20px; border-radius:8px; }
    h1 { margin:0 0 8px; font-size:25px; }
    p { margin:0 0 16px; color:var(--muted); }
    a { color:var(--gold); font-weight:700; }
  `]
})
export class ItemDetailPlaceholderComponent {
  readonly id = inject(ActivatedRoute).snapshot.paramMap.get('id');
}
