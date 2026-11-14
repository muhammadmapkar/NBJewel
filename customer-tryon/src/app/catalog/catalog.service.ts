// Role: Loads the bundled sample catalog when no runtime API address is configured; otherwise
// fetches all published API products and dated rates. API failures never restore static products.
import { CanActivateFn } from '@angular/router';
import { HttpClient } from '@angular/common/http';
import { Injectable, computed, inject, signal } from '@angular/core';
import { Observable, Subscription, forkJoin, map, of, switchMap } from 'rxjs';
import { STOREFRONT_CONFIG, resolveApiBase } from '../config/storefront-config';
import { ApiPage, ApiProduct, catalogFromApi } from './catalog-api.models';
import { Catalog, PriceBreakdown, Product, PublicMetalRate, formatMoney, priceOf } from './catalog.models';
export type CatalogState = { status: 'loading' } | { status: 'ready'; catalog: Catalog } | { status: 'error' };
@Injectable({ providedIn: 'root' })
export class CatalogService {
  private readonly http = inject(HttpClient);
  private readonly config = inject(STOREFRONT_CONFIG);
  readonly state = signal<CatalogState>({ status: 'loading' });
  readonly catalog = computed(() => { const s = this.state(); return s.status === 'ready' ? s.catalog : null; });
  readonly products = computed(() => this.catalog()?.products ?? []);
  readonly source = signal<'demo' | 'erp'>(this.config.apiBaseUrl ? 'erp' : 'demo');
  readonly storeId = signal<string | null>(null);
  readonly linkParams = computed(() => ({ source: this.source(), store: this.storeId() }));
  selectStore(id: string | null, source: 'demo' | 'erp' = this.config.apiBaseUrl ? 'erp' : 'demo'): void {
    if (this.storeId() === id && this.source() === source) { this.load(); return; }
    this.storeId.set(id); this.source.set(source); this.load(true);
  }
  private requested = false;
  private active?: Subscription;
  load(force = false): void {
    if (this.requested && !force) return;
    this.requested = true; this.active?.unsubscribe(); this.state.set({ status: 'loading' });
    try {
      if (this.config.error) throw new Error(this.config.error);
      const base = this.source() === 'erp' ? resolveApiBase(this.config.apiBaseUrl ?? this.config.portalApiBaseUrl, document.baseURI) : null;
      if (this.source() === 'erp' && !base) throw new Error('API not configured.');
      const source = base ? forkJoin({ products: this.apiPage(base), rates: this.http.get<PublicMetalRate[]>(`${base}/api/storefront/metal-rates`) })
        .pipe(map(({ products, rates }) => catalogFromApi(products, rates, base)))
        : this.http.get<Catalog>(this.url('catalog/catalog.json')).pipe(map(catalog => {
          if (catalog.products.some(product => !priceOf(product, catalog).available)) throw new Error('Missing matching metal rate.');
          return catalog;
        })).pipe(switchMap(catalog => this.storeId() ? this.http.get<import('../customer/store-directory.service').StoreLocation[]>('catalog/store-directory.json').pipe(map(stores => { const selected = stores.find(s => s.id === this.storeId() && s.source === 'demo'); if (!selected) throw new Error('Unknown store.'); return { ...catalog, products: catalog.products.filter(p => selected.productIds.includes(p.id)) }; })) : of(catalog)));
      this.active = source.subscribe({ next: catalog => this.state.set({ status: 'ready', catalog }), error: () => this.state.set({ status: 'error' }) });
    } catch { this.state.set({ status: 'error' }); }
  }
  private apiPage(base: string, page = 1, previous: ApiProduct[] = [], expected?: number): Observable<ApiProduct[]> {
    return this.http.get<ApiPage>(`${base}/api/storefront/products`, { params: { page, pageSize: 100, ...(this.storeId() ? { storeId: this.storeId()! } : {}) } }).pipe(switchMap(batch => {
      if (!Array.isArray(batch.items) || !Number.isInteger(batch.totalCount) || batch.totalCount < 0 || batch.totalCount > 10000 ||
          batch.page !== page || batch.pageSize !== 100 || (expected !== undefined && expected !== batch.totalCount)) throw new Error('Invalid catalog pagination.');
      const all = [...previous, ...batch.items];
      if (all.length > batch.totalCount || (all.length < batch.totalCount && batch.items.length !== 100)) throw new Error('Incomplete catalog page.');
      return all.length < batch.totalCount ? this.apiPage(base, page + 1, all, batch.totalCount) : of(all);
    }));
  }
  find(id: string): Product | undefined { return this.products().find(p => p.id === id); }
  price(product: Product): PriceBreakdown { return priceOf(product, this.catalog() ?? { rates: {}, vatPercent: 0 }); }
  money(amount: number): string { return formatMoney(amount, this.catalog()?.currency ?? 'AED'); }
  url(path: string): string { return new URL(path, document.baseURI).href; }
}

// Restore the catalog source before opening a shared product, camera or account link.
export const catalogContextGuard: CanActivateFn = route => {
  const catalog = inject(CatalogService); const source = route.queryParamMap.get('source');
  if (source === 'demo' || source === 'erp') catalog.selectStore(route.queryParamMap.get('store'), source);
  else catalog.load();
  return true;
};
