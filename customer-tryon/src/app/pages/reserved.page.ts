// Role: The reservation confirmation (/reserved): reference number, store, date and the designs.
// Demo only: nothing was sent to a store.

import { Component, computed, inject } from '@angular/core';
import { RouterLink } from '@angular/router';
import { Product } from '../catalog/catalog.models';
import { CatalogService } from '../catalog/catalog.service';
import { StoreService } from '../catalog/store.service';
import { SiteHeaderComponent } from '../ui/site-header.component';

@Component({
  selector: 'ct-reserved', standalone: true, imports: [RouterLink, SiteHeaderComponent],
  template: `
    <ct-site-header />
    <main class="page">
      @if (store.reservation(); as r) {
        <div class="card">
          <div class="tick" aria-hidden="true">✓</div>
          <h1 class="serif">Reserved for you</h1>
          <p class="muted">Show this reference at the store.</p>
          <p class="ref">{{ r.reference }}</p>
          <dl>
            <div><dt>Name</dt><dd>{{ r.name }}</dd></div>
            <div><dt>Store</dt><dd>{{ r.branch }}</dd></div>
            <div><dt>Visit date</dt><dd>{{ r.date }}</dd></div>
          </dl>
          <ul class="items">
            @for (p of items(); track p.id) { <li><img [src]="catalog.url(p.card)" alt="" /><span>{{ p.name }}</span></li> }
          </ul>
          <p class="muted small">Bring your try-on photos to compare in store. Demo: this reservation was not sent to a real store.</p>
          <a class="btn" routerLink="/shop">Continue browsing</a>
        </div>
      } @else {
        <div class="card"><p>No reservation yet.</p><a class="btn" routerLink="/shop">Browse the collection</a></div>
      }
    </main>
  `,
  styles: [`
    :host { display: block; min-height: 100vh; }
    .page { max-width: 560px; margin: 0 auto; padding: 24px 16px 40px; }
    .card { padding: 24px 18px; border-radius: var(--radius); background: var(--card); box-shadow: var(--shadow); text-align: center; display: grid; gap: 10px; justify-items: center; }
    .tick { width: 56px; height: 56px; border-radius: 50%; display: grid; place-items: center; background: var(--maroon); color: #fff; font-size: 28px; }
    h1 { margin: 4px 0 0; font-size: 32px; }
    .muted { margin: 0; color: var(--muted); }
    .small { font-size: 12px; }
    .ref { margin: 4px 0; padding: 10px 18px; border-radius: 12px; background: var(--cream); font-size: 24px; font-weight: 700; letter-spacing: 0.08em; color: var(--maroon); }
    dl { width: 100%; margin: 6px 0; text-align: left; }
    dl div { display: flex; justify-content: space-between; padding: 8px 0; border-bottom: 1px solid var(--line); }
    dt { color: var(--muted); } dd { margin: 0; font-weight: 600; }
    .items { width: 100%; list-style: none; padding: 0; margin: 4px 0; display: grid; gap: 8px; text-align: left; }
    .items li { display: flex; align-items: center; gap: 10px; }
    .items img { width: 44px; height: 44px; object-fit: contain; border-radius: 8px; background: var(--cream); padding: 4px; }
  `]
})
export class ReservedPage {
  readonly catalog = inject(CatalogService);
  readonly store = inject(StoreService);
  readonly items = computed(() => (this.store.reservation()?.items ?? []).map(id => this.catalog.find(id)).filter((p): p is Product => !!p));
  constructor() { this.catalog.load(); }
}
