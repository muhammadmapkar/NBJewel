// Role: Verifies multipart upload/revision payloads and the UI's conservative readiness gate.
import '@angular/compiler';
import { createEnvironmentInjector, EnvironmentInjector, ɵINJECTOR_SCOPE, provideZonelessChangeDetection } from '@angular/core';
import { provideHttpClient } from '@angular/common/http';
import { HttpTestingController, provideHttpClientTesting } from '@angular/common/http/testing';
import { afterEach, beforeEach, expect, it } from 'vitest';
import { environment } from '../../../environments/environment';
import { Item } from '../models';
import { StorefrontAdminService, publicationReady, StorefrontAsset } from './storefront-admin.service';
let injector: EnvironmentInjector; let http: HttpTestingController; let service: StorefrontAdminService;
const base = environment.apiBaseUrl;
const asset: StorefrontAsset = { itemId: 7, revisionId: 'revision', state: 'ready', qualityReview: 'approved', licenseStatus: 'open', width: 64, height: 64 };
const item = { id: 7, categoryId: 3, status: 'Complete', storefront: { description: 'Assumed sample' } } as unknown as Item;
beforeEach(() => { injector = createEnvironmentInjector([provideZonelessChangeDetection(), { provide: ɵINJECTOR_SCOPE, useValue: 'root' }, StorefrontAdminService, provideHttpClient(), provideHttpClientTesting()], null as unknown as EnvironmentInjector); http = injector.get(HttpTestingController); service = injector.get(StorefrontAdminService); });
afterEach(() => { http.verify(); injector.destroy(); });
it('uploads actual bytes using multipart rather than a mock filename', () => {
  const file = new File(['sample'], 'sample.png', { type: 'image/png' }); service.upload(7, file).subscribe({ error: () => undefined });
  const request = http.expectOne(`${base}/api/items/7/image`);
  expect(request.request.method).toBe('POST'); expect(request.request.body.get('file')).toBe(file); request.flush(asset);
});
it('names the reviewed revision and explicitly confirms publication', () => {
  service.review(7, 'revision').subscribe({ error: () => undefined });
  const review = http.expectOne(`${base}/api/items/7/image/review`);
  expect(review.request.body).toEqual({ revisionId: 'revision', approve: true, confirm: true }); review.flush(asset);
  service.publish(7, 'revision').subscribe({ error: () => undefined });
  const publish = http.expectOne(`${base}/api/ai/knowledge/products/7/publish`);
  expect(publish.request.body).toEqual({ assetRevisionId: 'revision', confirm: true }); publish.flush(null);
});
it('treats missing assets as empty but keeps permission failures visible', () => {
  let value: unknown = 'pending'; service.asset(7).subscribe({ next: result => value = result, error: () => undefined });
  http.expectOne(`${base}/api/items/7/image`).flush(null, { status: 404, statusText: 'Not found' }); expect(value).toBeNull();
  let failed = false; service.asset(7).subscribe({ error: () => failed = true });
  http.expectOne(`${base}/api/items/7/image`).flush(null, { status: 403, statusText: 'Forbidden' }); expect(failed).toBe(true);
});
it('requires complete supported item, description, matching item and reviewed ready asset', () => {
  expect(publicationReady(item, asset)).toBe(true); expect(publicationReady(item, null)).toBe(false);
  expect(publicationReady({ ...item, status: 'Draft' }, asset)).toBe(false);
  expect(publicationReady({ ...item, categoryId: 2 }, asset)).toBe(false);
  expect(publicationReady(item, { ...asset, state: 'processing' })).toBe(false);
  expect(publicationReady(item, { ...asset, qualityReview: 'pending' })).toBe(false);
  expect(publicationReady(item, { ...asset, itemId: 8 })).toBe(false);
});

it('loads the exact preview revision the operator is reviewing', () => {
  service.preview(7, 'reviewed-revision').subscribe({ error: () => undefined });
  const request = http.expectOne(`${base}/api/items/7/image/preview?revisionId=reviewed-revision`);
  expect(request.request.responseType).toBe('blob'); request.flush(new Blob(['image']));
});
