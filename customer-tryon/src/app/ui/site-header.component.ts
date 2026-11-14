// Role: The shop header on every page except the camera: back button (optional), the NB Jewel
// mark, a Shop link, and the wishlist and bag icons with counts.

import { Component, computed, inject, input } from '@angular/core';
import { Location } from '@angular/common';
import { RouterLink } from '@angular/router';
import { CatalogService } from '../catalog/catalog.service';
import { effectiveDateLabel, recordedRates } from '../catalog/catalog.models';
import { StoreService } from '../catalog/store.service';
import { CustomerSession } from '../customer/customer-session';
import { MarketRatesComponent } from './market-rates.component';
import { IconComponent } from './icon.component';

@Component({
  selector: 'ct-site-header', standalone: true, imports: [RouterLink, IconComponent, MarketRatesComponent],
  template: `
    <header class="bar" [class.dark]="dark()">
      @if (back()) {
        <button type="button" class="icon-btn" (click)="goBack()" aria-label="Back"><ct-icon name="back" /></button>
      }
      <a class="brand logo" routerLink="/">NB Jewel</a>
      <span class="spacer"></span>
      <a class="link desktop" routerLink="/shop" [queryParams]="catalog.linkParams()">Shop</a>
      <a class="link desktop" routerLink="/stores">Stores Map</a>
      <a class="icon-btn" routerLink="/wishlist" [queryParams]="catalog.linkParams()" aria-label="Wishlist">
        <ct-icon name="heart" />@if (store.wishlist().length) { <span class="count">{{ store.wishlist().length }}</span> }
      </a>
      <a class="icon-btn" routerLink="/bag" [queryParams]="catalog.linkParams()" aria-label="Bag">
        <ct-icon name="bag" />@if (store.bagCount()) { <span class="count">{{ store.bagCount() }}</span> }
      </a>
      @if (session.identity()) { <a class="link desktop" routerLink="/profile" [queryParams]="catalog.linkParams()">Customer Profile</a> }
      @else { <button class="link signin" type="button" (click)="session.require()">Sign In</button> }
    </header>
    <ct-market-rates />
    @if (rates().length) {
      <details class="rates">
        <summary>Recorded sample metal rates</summary>
        @for (rate of rates(); track rate.metalType + rate.purity) {
          <p>{{ rate.metalType }} {{ rate.purity }}: {{ rate.currency }} {{ rate.ratePerGram }} / g. {{ dateLabel(rate.effectiveDate) }}.</p>
        }
      </details>
    }
  `,
  styles: [`
    .signin { border:0; background:none; cursor:pointer; }
    @media(max-width:767px) { .desktop { display:none; } }
    .rates { padding: 10px 18px; background: var(--cream); color: var(--muted); font-size: 12px; border-bottom: 1px solid var(--line); }
    .rates summary { cursor: pointer; }
    .rates p { margin: 8px 0 0; }
    .bar { position: sticky; top: 0; z-index: 20; display: flex; align-items: center; gap: 6px; height: calc(58px + env(safe-area-inset-top)); padding: env(safe-area-inset-top) 10px 0 14px; background: rgba(250, 246, 239, 0.94); backdrop-filter: blur(10px); border-bottom: 1px solid var(--line); color: var(--ink); }
    .bar.dark { background: rgba(42, 13, 18, 0.92); border-color: transparent; color: var(--ivory); }
    .logo { font-size: 17px; color: inherit; text-decoration: none; }
    .bar:not(.dark) .logo { color: var(--maroon); }
    .spacer { flex: 1; }
    .link { padding: 8px 10px; color: inherit; text-decoration: none; font-weight: 500; font-size: 14px; }
    .icon-btn { position: relative; width: 42px; height: 42px; display: grid; place-items: center; border: 0; border-radius: 50%; background: transparent; color: inherit; text-decoration: none; }
    .count { position: absolute; top: 4px; right: 3px; min-width: 18px; height: 18px; padding: 0 5px; border-radius: 9px; background: var(--gold); color: #fff; font-size: 11px; font-weight: 700; display: grid; place-items: center; }
  `]
})
export class SiteHeaderComponent {
  readonly session = inject(CustomerSession);
  readonly catalog = inject(CatalogService);
  readonly rates = computed(() => { const c = this.catalog.catalog(); return c ? recordedRates(c) : []; });
  readonly dateLabel = effectiveDateLabel;
  readonly store = inject(StoreService);
  private readonly location = inject(Location);
  readonly back = input(false);
  readonly dark = input(false);

  goBack(): void { this.location.back(); }
}
