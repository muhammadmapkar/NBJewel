// Role: Proves assumed shop selection clears old data, applies live price policy, preserves links and uses published enquiry destinations.
import '@angular/compiler';
import { createEnvironmentInjector, EnvironmentInjector, ɵINJECTOR_SCOPE, provideZonelessChangeDetection } from '@angular/core';
import { provideHttpClient } from '@angular/common/http';
import { HttpTestingController, provideHttpClientTesting } from '@angular/common/http/testing';
import { afterEach, expect, it, vi } from 'vitest';
import { CatalogService } from './catalog.service';
import { STOREFRONT_CONFIG } from '../config/storefront-config';
let injector: EnvironmentInjector; let http: HttpTestingController; let service: CatalogService;
const api = 'https://api.example.test';
const settings = { name: 'Assumed wholesale', colorPreset: 'gold', headline: 'Assumed headline', introduction: '', story: '', showPrices: false, enquiries: true, whatsApp: '+12025550123', phone: '', email: '', openingHours: '', tryOn: false, bag: false, reservations: false, showBanner: true, showCollections: true, showFeatured: true, showStory: true, showStores: true, showMarketRates: false, visibleCollections: [], featuredItemIds: [], visibleBranchIds: [1] };
function product(slug: string) { return { id: 7, code: 'WS-ONLY', name: 'Assumed necklace', category: 'necklace', metalType: 'Gold', purity: '22K', grossWeight: 10, netWeight: 10, previewUrl: `/api/shops/${slug}/products/7/preview?v=11111111111111111111111111111111`, previewAspect: 1, price: null }; }
function setup() { vi.stubGlobal('document', { baseURI: 'https://shop.example.test/' }); injector = createEnvironmentInjector([provideZonelessChangeDetection(), { provide: ɵINJECTOR_SCOPE, useValue: 'root' }, { provide: STOREFRONT_CONFIG, useValue: { apiBaseUrl: api } }, CatalogService, provideHttpClient(), provideHttpClientTesting()], null as unknown as EnvironmentInjector); http = injector.get(HttpTestingController); service = injector.get(CatalogService); }
function finish(slug: string) { http.expectOne(`${api}/api/shops/${slug}`).flush({ slug, revision: 1, settings }); http.expectOne(`${api}/api/shops/${slug}/products?page=1&pageSize=100`).flush({ items: [product(slug)], page: 1, pageSize: 100, totalCount: 1 }); http.expectOne(`${api}/api/shops/${slug}/metal-rates`).flush({ pricesHidden: true, rates: [] }); }
afterEach(() => { http?.verify(); injector?.destroy(); vi.unstubAllGlobals(); });
it('uses scoped API-only data and accepts absent prices only under the published hidden policy', () => { setup(); service.selectShop('sample-wholesale'); finish('sample-wholesale'); expect(service.state().status).toBe('ready'); expect(service.showPrices()).toBe(false); expect(service.products()[0].apiPrice).toBeUndefined(); expect(service.actionEnabled('bag')).toBe(false); http.expectNone('https://shop.example.test/catalog/catalog.json'); });
it('clears the prior business immediately and cancels its pending catalog request', () => { setup(); service.selectShop('first-shop'); http.expectOne(`${api}/api/shops/first-shop`).flush({ slug: 'first-shop', revision: 1, settings }); const oldProducts = http.expectOne(`${api}/api/shops/first-shop/products?page=1&pageSize=100`); const oldRates = http.expectOne(`${api}/api/shops/first-shop/metal-rates`); service.selectShop('sample-wholesale'); expect(oldProducts.cancelled).toBe(true); expect(oldRates.cancelled).toBe(true); expect(service.products()).toEqual([]); expect(service.portal()).toBeNull(); finish('sample-wholesale'); expect(service.products()[0].code).toBe('WS-ONLY'); });
it('keeps product and navigation links scoped and builds enquiry text from the published name and number', () => { setup(); service.selectShop('sample-wholesale'); finish('sample-wholesale'); expect(service.link('p', '7')).toEqual(['/shops', 'sample-wholesale', 'p', '7']); expect(service.link()).toEqual(['/shops', 'sample-wholesale']); service.storeId.set('1'); const url = service.enquiry(service.products())!; expect(url).toContain('https://wa.me/12025550123?text='); expect(decodeURIComponent(url)).toContain('Assumed wholesale'); expect(decodeURIComponent(url)).toContain('/shops/sample-wholesale/p/7?store=1'); expect(decodeURIComponent(url)).not.toContain('AED'); });
it('shows an unavailable state with no catalog fallback for an offline or unknown shop', () => { setup(); service.selectShop('offline-shop'); http.expectOne(`${api}/api/shops/offline-shop`).flush({}, { status: 404, statusText: 'Not Found' }); expect(service.state().status).toBe('error'); expect(service.products()).toEqual([]); expect(service.portal()).toBeNull(); http.expectNone(`${api}/api/storefront/products?page=1&pageSize=100`); });
it('uses authenticated draft preview data and disables every customer action', () => {
  setup(); service.selectPreview('sample-wholesale');
  const request = http.expectOne(`${api}/api/businesses/sample-wholesale/portal/preview?page=1`); expect(request.request.withCredentials).toBe(true);
  request.flush({ revision: 2, settings: { ...settings, bag: true, tryOn: true }, preview: true, products: { items: [{ ...product('sample-wholesale'), previewUrl: '/api/businesses/sample-wholesale/portal/products/7/preview?v=11111111111111111111111111111111' }], totalCount: 1, page: 1, pageSize: 100 } });
  expect(service.state().status).toBe('ready'); expect(service.preview()).toBe(true); expect(service.actionEnabled('bag')).toBe(false); expect(service.actionEnabled('tryOn')).toBe(false); expect(service.enquiry(service.products())).toBeNull(); http.expectNone(`${api}/api/shops/sample-wholesale`);
});
it('revalidates staff authentication instead of reusing a cached draft on another preview navigation', () => {
  setup(); service.selectPreview('sample-wholesale');
  http.expectOne(`${api}/api/businesses/sample-wholesale/portal/preview?page=1`).flush({ revision: 2, settings, preview: true, products: { items: [], totalCount: 0, page: 1, pageSize: 100 } });
  service.selectPreview('sample-wholesale'); expect(service.portal()).toBeNull();
  http.expectOne(`${api}/api/businesses/sample-wholesale/portal/preview?page=1`).flush({}, { status: 401, statusText: 'Unauthorized' });
  expect(service.state().status).toBe('error'); expect(service.products()).toEqual([]);
});

it('keeps selected store filtering in authenticated preview requests', () => {
  setup(); service.selectPreview('sample-wholesale');
  const response = { revision: 2, settings, preview: true, products: { items: [], totalCount: 0, page: 1, pageSize: 100 } };
  http.expectOne(`${api}/api/businesses/sample-wholesale/portal/preview?page=1`).flush(response);
  service.selectStore('1'); http.expectOne(`${api}/api/businesses/sample-wholesale/portal/preview?page=1&storeId=1`).flush(response);
  expect(service.state().status).toBe('ready');
});
