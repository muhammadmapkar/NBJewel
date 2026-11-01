// Role: The wishlist (/wishlist): designs the customer saved with the heart, shown as cards.

import { Component, computed, inject } from '@angular/core';
import { RouterLink } from '@angular/router';
import { Product } from '../catalog/catalog.models';
import { CatalogService } from '../catalog/catalog.service';
import { StoreService } from '../catalog/store.service';
import { ProductCardComponent } from '../ui/product-card.component';
import { SiteHeaderComponent } from '../ui/site-header.component';

@Component({
  selector: 'ct-wishlist', standalone: true, imports: [RouterLink, ProductCardComponent, SiteHeaderComponent],
  template: `
    <ct-site-header [back]="true" />
    <main class="page">
      <h1 class="serif">Wishlist</h1>
      @if (!items().length) {
        <div class="empty"><p>Tap the heart on any design to save it here.</p><a class="btn" routerLink="/shop">Browse the collection</a></div>
      } @else {
        <div class="grid">@for (p of items(); track p.id) { <ct-product-card [product]="p" /> }</div>
      }
    </main>
  `,
  styles: [`
    :host { display: block; min-height: 100vh; }
    .page { max-width: 1080px; margin: 0 auto; padding: 16px 16px 40px; }
    h1 { margin: 4px 0 14px; font-size: 32px; }
    .grid { display: grid; grid-template-columns: repeat(auto-fill, minmax(160px, 1fr)); gap: 14px; }
    .empty { padding: 40px 0; text-align: center; color: var(--muted); display: grid; gap: 14px; justify-items: center; }
  `]
})
export class WishlistPage {
  readonly catalog = inject(CatalogService);
  private readonly store = inject(StoreService);
  readonly items = computed(() => this.store.wishlist().map(id => this.catalog.find(id)).filter((p): p is Product => !!p));
  constructor() { this.catalog.load(); }
}
