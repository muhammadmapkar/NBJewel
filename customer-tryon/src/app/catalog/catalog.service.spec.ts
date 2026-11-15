// Role: Exercises static/API catalog loading, pagination, authoritative prices, failure handling
// and refresh using Angular's real HTTP client with controlled HTTP responses.
import '@angular/compiler';
import { createEnvironmentInjector, EnvironmentInjector, runInInjectionContext, ɵINJECTOR_SCOPE, provideZonelessChangeDetection } from '@angular/core';
import { provideHttpClient } from '@angular/common/http';
import { HttpTestingController, provideHttpClientTesting } from '@angular/common/http/testing';
import { afterEach, expect, it, vi } from 'vitest';
import { ActivatedRouteSnapshot, RouterStateSnapshot, convertToParamMap } from '@angular/router';
import { CatalogService, catalogContextGuard } from './catalog.service';
import { Catalog } from './catalog.models';
import { STOREFRONT_CONFIG } from '../config/storefront-config';
let injector: EnvironmentInjector; let http: HttpTestingController; let service: CatalogService;
const api = 'https://api.example.test'; const page = 'https://shop.example.test/base/';
const bundled: Catalog = { currency: 'AED', rates: { '22K': 66 }, ratesNote: 'sample', vatPercent: 5, branches: [], sample: true, products: [] };
const rate = { id: 2, metalType: 'Gold', purity: '22K', ratePerGram: 66, currency: 'AED', effectiveDate: '2026-09-30' };
function product(id = 7) { return { id, code: `SAMPLE-${id}`, name: 'Sample necklace', category: 'necklace', metalType: 'Gold', purity: '22K', grossWeight: 11, netWeight: 10,
  imageUrl: null, storefront: { description: 'Assumed sample', collection: 'Sample', occasions: [], stones: [], isSample: true, addedOn: '2026-09-30' },
  previewUrl: `/api/storefront/products/${id}/preview?v=11111111111111111111111111111111`, previewAspect: 0.6,
  price: { itemId: id, currency: 'AED', netWeight: 10, ratePerGram: 66, metalValue: 660, makingType: 'flat', makingValue: 66, makingCharge: 66, stoneValue: 50, vatPercent: 5, vatAmount: 38.8, totalPrice: 814.8, effectiveDate: '2026-09-30', rateId: 2, change: { explanation: 'Recorded sample rate' } } }; }
function setup(apiBaseUrl: string | null) {
  vi.stubGlobal('document', { baseURI: page });
  injector = createEnvironmentInjector([provideZonelessChangeDetection(), { provide: ɵINJECTOR_SCOPE, useValue: 'root' }, { provide: STOREFRONT_CONFIG, useValue: { apiBaseUrl } }, CatalogService, provideHttpClient(), provideHttpClientTesting()], null as unknown as EnvironmentInjector);
  http = injector.get(HttpTestingController); service = injector.get(CatalogService);
}
afterEach(() => { http?.verify(); injector?.destroy(); vi.unstubAllGlobals(); });
it('fails visibly without an API address instead of loading demo products', () => {
  setup(null); service.load(); expect(service.state().status).toBe('error');
  http.expectNone(`${page}catalog/catalog.json`);
});
it('legacy demo links load the unified ERP catalog', () => {
  setup(api); service.selectStore(null, 'demo');
  http.expectOne(`${api}/api/storefront/products?page=1&pageSize=100`).flush({ items: [], page: 1, pageSize: 100, totalCount: 0 });
  http.expectOne(`${api}/api/storefront/metal-rates`).flush([rate]);
  expect(service.source()).toBe('erp'); http.expectNone(`${page}catalog/catalog.json`);
});
it('loads published API products and rates anonymously and keeps authoritative pricing', () => {
  setup(api + '/'); service.load();
  const products = http.expectOne(`${api}/api/storefront/products?page=1&pageSize=100`);
  expect(products.request.withCredentials).toBe(false); products.flush({ items: [product()], page: 1, pageSize: 100, totalCount: 1 });
  http.expectOne(`${api}/api/storefront/metal-rates`).flush([rate]);
  expect(service.state().status).toBe('ready'); const p = service.find('7')!;
  expect(p.card).toBe(`${api}${product().previewUrl}`); expect(p.parts.necklace?.aspect).toBe(0.6);
  expect(service.price(p)).toMatchObject({ gold: 660, making: 66, stones: 50, vat: 38.8, total: 814.8, rate: 66, vatPercent: 5, effectiveDate: '2026-09-30' });
  http.expectNone(`${page}catalog/catalog.json`);
});
it('fetches every API page without truncating published products', () => {
  setup(api); service.load();
  http.expectOne(`${api}/api/storefront/products?page=1&pageSize=100`).flush({ items: Array.from({ length: 100 }, (_, i) => product(i + 1)), page: 1, pageSize: 100, totalCount: 101 });
  http.expectOne(`${api}/api/storefront/products?page=2&pageSize=100`).flush({ items: [product(101)], page: 2, pageSize: 100, totalCount: 101 });
  http.expectOne(`${api}/api/storefront/metal-rates`).flush([rate]); expect(service.products()).toHaveLength(101);
});
it('shows API errors without falling back to the bundled catalog', () => {
  setup(api); service.load(); http.expectOne(`${api}/api/storefront/metal-rates`).flush([rate]);
  http.expectOne(`${api}/api/storefront/products?page=1&pageSize=100`).flush({}, { status: 503, statusText: 'Unavailable' });
  expect(service.state().status).toBe('error'); expect(service.products()).toEqual([]); http.expectNone(`${page}catalog/catalog.json`);
});
it('explicit refresh reads revocation instead of keeping the first loaded catalog', () => {
  setup(api); service.load(); http.expectOne(`${api}/api/storefront/products?page=1&pageSize=100`).flush({ items: [product()], page: 1, pageSize: 100, totalCount: 1 }); http.expectOne(`${api}/api/storefront/metal-rates`).flush([rate]);
  expect(service.products()).toHaveLength(1); service.load(true);
  http.expectOne(`${api}/api/storefront/products?page=1&pageSize=100`).flush({ items: [], page: 1, pageSize: 100, totalCount: 0 }); http.expectOne(`${api}/api/storefront/metal-rates`).flush([rate]);
  expect(service.products()).toEqual([]);
});
it('rejects an incomplete API price or unsupported product instead of inventing values', () => {
  setup(api); service.load(); http.expectOne(`${api}/api/storefront/metal-rates`).flush([rate]);
  http.expectOne(`${api}/api/storefront/products?page=1&pageSize=100`).flush({ items: [{ ...product(), price: null }], page: 1, pageSize: 100, totalCount: 1 }); expect(service.state().status).toBe('error');
});

it('reads published, updated and revoked sample data through the same runtime service without a rebuild', () => {
  setup(api);
  const respond = (items: ReturnType<typeof product>[]) => {
    http.expectOne(`${api}/api/storefront/products?page=1&pageSize=100`).flush({ items, page: 1, pageSize: 100, totalCount: items.length });
    http.expectOne(`${api}/api/storefront/metal-rates`).flush([rate]);
  };
  service.load(); respond([product()]);
  expect(service.find('7')?.name).toBe('Sample necklace'); expect(service.price(service.find('7')!).total).toBe(814.8);
  const updated = product(); updated.name = 'Updated sample necklace'; updated.storefront.description = 'Assumed updated copy';
  updated.price.stoneValue = 123; updated.price.vatAmount = 42.45; updated.price.totalPrice = 891.45;
  service.load(true); respond([updated]);
  expect(service.find('7')?.description).toBe('Assumed updated copy'); expect(service.find('7')?.name).toBe(updated.name);
  expect(service.price(service.find('7')!)).toMatchObject({ stones: 123, vat: 42.45, total: 891.45, effectiveDate: '2026-09-30', changeExplanation: 'Recorded sample rate' });
  service.load(true); respond([]); expect(service.find('7')).toBeUndefined(); expect(service.products()).toEqual([]);
  http.expectNone(`${page}catalog/catalog.json`);
});

it('selects a store-specific ERP catalog and retains fail-closed API behavior', () => {
  setup(api); service.selectStore('1', 'erp');
  http.expectOne(`${api}/api/storefront/products?page=1&pageSize=100&storeId=1`).flush({ items: [product()], page: 1, pageSize: 100, totalCount: 1 });
  http.expectOne(`${api}/api/storefront/metal-rates`).flush([rate]); expect(service.find('7')).toBeDefined();
  service.selectStore('2', 'erp'); http.expectOne(`${api}/api/storefront/metal-rates`).flush([rate]);
  http.expectOne(`${api}/api/storefront/products?page=1&pageSize=100&storeId=2`).flush({}, { status:503, statusText:'Unavailable' });
  expect(service.products()).toEqual([]); expect(service.state().status).toBe('error'); http.expectNone(`${page}catalog/catalog.json`);
});

it('restores the ERP store from a shared product URL before loading', () => {
  setup(null); injector.get(STOREFRONT_CONFIG).portalApiBaseUrl = api;
  runInInjectionContext(injector, () => catalogContextGuard({ queryParamMap: convertToParamMap({ source: 'erp', store: '2' }) } as ActivatedRouteSnapshot, {} as RouterStateSnapshot));
  http.expectOne(`${api}/api/storefront/products?page=1&pageSize=100&storeId=2`).flush({ items: [product()], page: 1, pageSize: 100, totalCount: 1 });
  http.expectOne(`${api}/api/storefront/metal-rates`).flush([rate]);
  expect(service.linkParams()).toEqual({ source: 'erp', store: '2' }); expect(service.find('7')).toBeDefined();
  http.expectNone(`${page}catalog/catalog.json`);
});
