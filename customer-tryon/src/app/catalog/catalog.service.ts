// Role: Loads the customer catalog once from public/catalog/catalog.json (bundled with the app,
// so it works on a static host with no API), shares it between pages, and prices products from
// the catalog's sample gold rate.

import { HttpClient } from '@angular/common/http';
import { Injectable, computed, inject, signal } from '@angular/core';
import { Catalog, PriceBreakdown, Product, formatMoney, priceOf } from './catalog.models';

export type CatalogState = { status: 'loading' } | { status: 'ready'; catalog: Catalog } | { status: 'error' };

@Injectable({ providedIn: 'root' })
export class CatalogService {
  private readonly http = inject(HttpClient);
  readonly state = signal<CatalogState>({ status: 'loading' });
  readonly catalog = computed(() => { const s = this.state(); return s.status === 'ready' ? s.catalog : null; });
  readonly products = computed(() => this.catalog()?.products ?? []);
  private requested = false;

  load(force = false): void {
    if (this.requested && !force) return;
    this.requested = true;
    this.state.set({ status: 'loading' });
    this.http.get<Catalog>(this.url('catalog/catalog.json')).subscribe({
      next: catalog => this.state.set({ status: 'ready', catalog }),
      error: () => this.state.set({ status: 'error' })
    });
  }

  find(id: string): Product | undefined { return this.products().find(p => p.id === id); }

  price(product: Product): PriceBreakdown {
    const c = this.catalog();
    return priceOf(product, c ?? { rates: {}, vatPercent: 0 });
  }

  money(amount: number): string { return formatMoney(amount, this.catalog()?.currency ?? 'AED'); }

  url(path: string): string { return new URL(path, document.baseURI).href; }
}
