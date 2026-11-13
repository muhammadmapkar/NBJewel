// Role: One design in a grid: picture with badge and wishlist heart, name, purity and weight,
// price, and a Try on button. Used by the home page, the catalogue and the wishlist.

import { Component, computed, inject, input } from '@angular/core';
import { RouterLink } from '@angular/router';
import { Product } from '../catalog/catalog.models';
import { CatalogService } from '../catalog/catalog.service';
import { StoreService } from '../catalog/store.service';
import { IconComponent } from './icon.component';

@Component({
  selector: 'ct-product-card', standalone: true, imports: [RouterLink, IconComponent],
  template: `
    @let p = product();
    <article class="card">
      <a class="pic" [routerLink]="['/p', p.id]" [attr.aria-label]="p.name">
        <img [src]="catalog.url(p.card)" [alt]="p.name" loading="lazy" />
        @if (p.badge) { <span class="badge" [class.new]="p.badge === 'new'">{{ p.badge === 'new' ? 'New' : 'Bestseller' }}</span> }
      </a>
      <button type="button" class="heart" [class.on]="store.inWishlist(p.id)" (click)="store.toggleWishlist(p.id)"
        [attr.aria-label]="store.inWishlist(p.id) ? 'Remove from wishlist' : 'Add to wishlist'" [attr.aria-pressed]="store.inWishlist(p.id)">
        <ct-icon [name]="store.inWishlist(p.id) ? 'heartFull' : 'heart'" />
      </button>
      <div class="info">
        <a class="name serif" [routerLink]="['/p', p.id]">{{ p.name }}</a>
        <span class="meta">{{ p.purity }} {{ p.metal }} · {{ p.grossWeight }} g</span>
        <span class="price">{{ catalog.money(total()) }}</span>
        <a class="btn try" [routerLink]="['/try', p.id]"><ct-icon name="camera" /> Try on</a>
      </div>
    </article>
  `,
  styles: [`
    :host { display: block; }
    .card { position: relative; display: flex; flex-direction: column; height: 100%; border-radius: var(--radius); background: var(--card); box-shadow: var(--shadow); overflow: hidden; }
    .pic { position: relative; aspect-ratio: 1; display: grid; place-items: center; padding: 14px; background: radial-gradient(circle at 50% 40%, #fff 0%, var(--cream) 75%); }
    .pic img { max-height: 100%; object-fit: contain; filter: drop-shadow(0 6px 10px rgba(80, 50, 10, 0.18)); }
    .badge { position: absolute; top: 10px; left: 10px; padding: 3px 9px; border-radius: 999px; background: var(--ink); color: var(--gold-soft); font-size: 10.5px; font-weight: 600; letter-spacing: 0.06em; text-transform: uppercase; }
    .badge.new { background: var(--gold); color: #fff; }
    .heart { position: absolute; top: 6px; right: 6px; width: 38px; height: 38px; display: grid; place-items: center; border: 0; border-radius: 50%; background: rgba(255,255,255,0.85); color: var(--maroon); }
    .heart ct-icon { width: 20px; height: 20px; }
    .info { display: flex; flex-direction: column; gap: 3px; padding: 11px 12px 13px; flex: 1; }
    .name { color: var(--ink); text-decoration: none; font-size: 18px; font-weight: 600; line-height: 1.15; }
    .meta { color: var(--muted); font-size: 12px; }
    .price { margin-top: 2px; font-weight: 600; font-size: 15px; color: var(--maroon); }
    .try { margin-top: auto; min-height: 40px; padding: 0 14px; font-size: 14px; }
    .try ct-icon { width: 18px; height: 18px; }
    .info .try { margin-top: 10px; }
  `]
})
export class ProductCardComponent {
  readonly catalog = inject(CatalogService);
  readonly store = inject(StoreService);
  readonly product = input.required<Product>();
  readonly total = computed(() => this.catalog.price(this.product()).total);
}
