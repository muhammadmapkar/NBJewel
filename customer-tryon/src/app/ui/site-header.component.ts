// Role: The shop header on every page except the camera: back button (optional), the NB Jewel
// mark, a Shop link, and the wishlist and bag icons with counts.

import { Component, inject, input } from '@angular/core';
import { Location } from '@angular/common';
import { RouterLink } from '@angular/router';
import { StoreService } from '../catalog/store.service';
import { IconComponent } from './icon.component';

@Component({
  selector: 'ct-site-header', standalone: true, imports: [RouterLink, IconComponent],
  template: `
    <header class="bar" [class.dark]="dark()">
      @if (back()) {
        <button type="button" class="icon-btn" (click)="goBack()" aria-label="Back"><ct-icon name="back" /></button>
      }
      <a class="brand logo" routerLink="/">NB Jewel</a>
      <span class="spacer"></span>
      <a class="link" routerLink="/shop">Shop</a>
      <a class="icon-btn" routerLink="/wishlist" aria-label="Wishlist">
        <ct-icon name="heart" />@if (store.wishlist().length) { <span class="count">{{ store.wishlist().length }}</span> }
      </a>
      <a class="icon-btn" routerLink="/bag" aria-label="Bag">
        <ct-icon name="bag" />@if (store.bagCount()) { <span class="count">{{ store.bagCount() }}</span> }
      </a>
    </header>
  `,
  styles: [`
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
  readonly store = inject(StoreService);
  private readonly location = inject(Location);
  readonly back = input(false);
  readonly dark = input(false);

  goBack(): void { this.location.back(); }
}
