// Role: Loads the single published ERP catalog and dated rates through the backend API.
// Legacy demo links use this same catalog; missing configuration and API failures fail closed.
import { CanActivateFn } from '@angular/router';
import { HttpClient } from '@angular/common/http';
import { Injectable, computed, inject, signal } from '@angular/core';
import { Observable, Subscription, forkJoin, map, of, switchMap } from 'rxjs';
import { STOREFRONT_CONFIG, resolveApiBase } from '../config/storefront-config';
import { ApiPage, ApiProduct, catalogFromApi } from './catalog-api.models';
import { checkedPortal, PortalSettings, PublicPortal } from './portal.models';
import { Catalog, PriceBreakdown, Product, PublicMetalRate, formatMoney, priceOf } from './catalog.models';
export type CatalogState = { status: 'loading' } | { status: 'ready'; catalog: Catalog } | { status: 'error' };
@Injectable({ providedIn: 'root' })
export class CatalogService {
  private readonly http = inject(HttpClient);
  private readonly config = inject(STOREFRONT_CONFIG);
  readonly state = signal<CatalogState>({ status: 'loading' });
  readonly catalog = computed(() => { const s = this.state(); return s.status === 'ready' ? s.catalog : null; });
  readonly products = computed(() => this.catalog()?.products ?? []);
  readonly source = signal<'demo' | 'erp'>('erp');
  readonly storeId = signal<string | null>(null);
  readonly shopSlug = signal<string | null>(null);
  readonly portal = signal<PortalSettings | null>(null);
  readonly preview = signal(false);
  readonly showPrices = computed(() => this.portal()?.showPrices ?? this.shopSlug() === null);
  selectShop(slug: string, store: string | null = null): void {
    if (!/^[a-z0-9-]{3,40}$/.test(slug)) throw new Error('Invalid shop.');
    if (slug === this.shopSlug()) { if (store !== this.storeId()) this.selectStore(store); else this.load(); return; }
    this.active?.unsubscribe(); this.shopSlug.set(slug); this.portal.set(null); this.storeId.set(store); this.requested = false; this.state.set({ status: 'loading' }); this.load();
  }
  link(...parts: (string | number)[]): string[] { return ['/shops', this.shopSlug() ?? 'nb-jewel-demo', ...parts.flatMap(part => String(part).split('/').filter(Boolean))]; }
  contextParams(extra: Record<string, unknown> = {}): Record<string, unknown> { return { ...this.linkParams(), ...extra }; }
  actionEnabled(action: 'bag' | 'reservations' | 'tryOn' | 'enquiries'): boolean { return !this.preview() && (this.shopSlug() === null || (this.state().status === 'ready' && this.portal()?.[action] === true)); }
  assetUrl(id: string | null | undefined): string | null { if (!id || !/^[a-f0-9]{32}$/.test(id)) return null; const base = resolveApiBase(this.config.apiBaseUrl ?? this.config.portalApiBaseUrl, document.baseURI); return base ? `${base}/api/shops/${this.shopSlug()}/assets/${id}` : null; }
  enquiry(products: Product[]): string | null {
    const portal = this.portal(); if (!portal || !this.actionEnabled('enquiries') || !/^\+[1-9][0-9]{7,14}$/.test(portal.whatsApp)) return null;
    const lines = products.map(p => { const url = new URL(this.url(this.link('p', p.id).join('/'))); if (this.storeId()) url.searchParams.set('store', this.storeId()!); return `${p.name} (${p.code})${this.showPrices() ? ' ' + this.money(this.price(p).total) : ''} ${url.href}`; });
    return `https://wa.me/${portal.whatsApp.slice(1)}?text=${encodeURIComponent(`Hello ${portal.name}, I would like to enquire about:\n${lines.join('\n')}`)}`;
  }
  readonly linkParams = computed(() => ({ source: this.source(), store: this.storeId() }));
  selectStore(id: string | null, source: 'demo' | 'erp' = 'erp'): void {
    if (source === 'demo') id = null; // Old demo store IDs are not ERP store IDs.
    source = 'erp';
    if (this.storeId() === id && this.source() === source) { this.load(); return; }
    this.storeId.set(id); this.source.set(source); this.load(true);
  }
  private settle: () => void = () => {};
  private readyPromise: Promise<void> = Promise.resolve();
  whenReady(): Promise<void> { return this.readyPromise; }
  private requested = false;
  private active?: Subscription;
  load(force = false): void {
    if (this.requested && !force) return;
    this.requested = true; this.active?.unsubscribe(); this.settle(); this.readyPromise = new Promise(resolve => { this.settle = resolve; }); this.state.set({ status: 'loading' });
    try {
      if (this.config.error) throw new Error(this.config.error);
      const base = resolveApiBase(this.config.apiBaseUrl ?? this.config.portalApiBaseUrl, document.baseURI);
      if (!base) throw new Error('API not configured.');
      const slug = this.shopSlug(); const prefix = slug ? `/api/shops/${slug}` : '/api/storefront';
      const configuration: Observable<PortalSettings | null> = slug ? this.http.get<PublicPortal>(`${base}${prefix}`).pipe(map(value => checkedPortal(value, slug))) : of(null);
      const source = configuration.pipe(switchMap(settings => {
        this.portal.set(settings);
        return forkJoin({ products: this.apiPage(base, prefix), rates: this.http.get<PublicMetalRate[] | { pricesHidden: boolean; rates: PublicMetalRate[] }>(`${base}${prefix}/metal-rates`) })
          .pipe(map(({ products, rates }) => catalogFromApi(products, Array.isArray(rates) ? rates : rates.pricesHidden && settings?.showPrices === false ? rates.rates : [], base, slug ? { pricesHidden: settings?.showPrices === false, slug } : undefined)));
      }));
      this.active = source.subscribe({ next: catalog => { this.state.set({ status: 'ready', catalog }); this.settle(); }, error: () => { this.portal.set(null); this.state.set({ status: 'error' }); this.settle(); } });
    } catch { this.portal.set(null); this.state.set({ status: 'error' }); this.settle(); }
  }
  private apiPage(base: string, prefix: string, page = 1, previous: ApiProduct[] = [], expected?: number): Observable<ApiProduct[]> {
    return this.http.get<ApiPage>(`${base}${prefix}/products`, { params: { page, pageSize: 100, ...(this.storeId() ? { storeId: this.storeId()! } : {}) } }).pipe(switchMap(batch => {
      if (!Array.isArray(batch.items) || !Number.isInteger(batch.totalCount) || batch.totalCount < 0 || batch.totalCount > 10000 ||
          batch.page !== page || batch.pageSize !== 100 || (expected !== undefined && expected !== batch.totalCount)) throw new Error('Invalid catalog pagination.');
      const all = [...previous, ...batch.items];
      if (all.length > batch.totalCount || (all.length < batch.totalCount && batch.items.length !== 100)) throw new Error('Incomplete catalog page.');
      return all.length < batch.totalCount ? this.apiPage(base, prefix, page + 1, all, batch.totalCount) : of(all);
    }));
  }
  find(id: string): Product | undefined { return this.products().find(p => p.id === id); }
  price(product: Product): PriceBreakdown { if (!this.showPrices()) return { gold: 0, making: 0, stones: 0, vat: 0, total: 0, rate: 0, available: false }; return priceOf(product, this.catalog() ?? { rates: {}, vatPercent: 0 }); }
  money(amount: number): string { return formatMoney(amount, this.catalog()?.currency ?? 'AED'); }
  url(path: string): string { return new URL(path, document.baseURI).href; }
}

// Restore the catalog source before opening a shared product, camera or account link.
export const catalogContextGuard: CanActivateFn = route => {
  const catalog = inject(CatalogService); const source = route.queryParamMap.get('source');
  if (source === 'demo' || source === 'erp') catalog.selectStore(route.queryParamMap.get('store'), source);
  else catalog.selectStore(route.queryParamMap.get('store'));
  return true;
};
