// Role: Exercises static/API catalog loading, pagination, authoritative prices, failure handling
// and refresh using Angular's real HTTP client with controlled HTTP responses.
import '@angular/compiler';
import { createEnvironmentInjector, EnvironmentInjector, ɵINJECTOR_SCOPE, provideZonelessChangeDetection } from '@angular/core';
import { provideHttpClient } from '@angular/common/http';
import { HttpTestingController, provideHttpClientTesting } from '@angular/common/http/testing';
import { afterEach, expect, it, vi } from 'vitest';
import { CatalogService } from './catalog.service';
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
it('keeps the bundled catalog when no API address is configured', () => {
  setup(null); service.load(); http.expectOne(`${page}catalog/catalog.json`).flush(bundled);
  expect(service.catalog()).toEqual(bundled); service.load(); http.expectNone(`${page}catalog/catalog.json`);
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
