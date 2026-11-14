// Role: Proves auth intent completion, cancellation and verification-required handling through real HTTP contracts.
import '@angular/compiler';
import { createEnvironmentInjector, EnvironmentInjector, ɵINJECTOR_SCOPE, provideZonelessChangeDetection } from '@angular/core';
import { provideHttpClient } from '@angular/common/http';
import { HttpTestingController, provideHttpClientTesting } from '@angular/common/http/testing';
import { afterEach, expect, it } from 'vitest';
import { CustomerSession } from './customer-session';
import { STOREFRONT_CONFIG } from '../config/storefront-config';
import { distanceKm } from './store-directory.service';
let injector: EnvironmentInjector; let http: HttpTestingController; let session: CustomerSession;
function setup() { injector = createEnvironmentInjector([provideZonelessChangeDetection(), { provide: ɵINJECTOR_SCOPE, useValue: 'root' }, { provide: STOREFRONT_CONFIG, useValue: { apiBaseUrl: null, portalApiBaseUrl: 'http://localhost:5155' } }, CustomerSession, provideHttpClient(), provideHttpClientTesting()], null as unknown as EnvironmentInjector); http = injector.get(HttpTestingController); session = injector.get(CustomerSession); }
async function guest() { const intent = session.require('Try this design'); http.expectOne('http://localhost:5155/api/auth/me').flush({}, { status: 401, statusText: 'Unauthorized' }); await Promise.resolve(); await Promise.resolve(); return { intent }; }
afterEach(() => { http?.verify(); injector?.destroy(); });
it('resumes the pending intent only after a customer login with CSRF and cookies', async () => {
  setup(); const { intent } = await guest(); expect(session.prompt()).toBe(true);
  const login = session.authenticate('login', 'sample@example.test', 'synthetic');
  const token = http.expectOne('http://localhost:5155/api/auth/csrf'); expect(token.request.withCredentials).toBe(true); token.flush({ token: 'test-token' }); await Promise.resolve();
  const request = http.expectOne('http://localhost:5155/api/auth/login'); expect(request.request.headers.get('X-CSRF-TOKEN')).toBe('test-token'); expect(request.request.withCredentials).toBe(true);
  request.flush({ id: 'customer-1', email: 'sample@example.test', accountType: 'customer' }); await login;
  expect(await intent).toBe(true); expect(session.prompt()).toBe(false);
});
it('does not authenticate a verification-required signup and cancellation does not resume', async () => {
  setup(); const { intent } = await guest(); const registration = session.authenticate('register', 'sample@example.test', 'synthetic');
  http.expectOne('http://localhost:5155/api/auth/csrf').flush({ token: 'test' }); await Promise.resolve();
  http.expectOne('http://localhost:5155/api/auth/register').flush({ verificationRequired: true }, { status: 202, statusText: 'Accepted' }); await registration;
  expect(session.identity()).toBeNull(); expect(session.prompt()).toBe(true); session.cancel(); expect(await intent).toBe(false);
});
it('rejects a staff session for customer intents', async () => {
  setup(); const intent = session.require(); http.expectOne('http://localhost:5155/api/auth/me').flush({ id: 'staff', email: 'staff@example.test', accountType: 'erp' }); await Promise.resolve(); await Promise.resolve();
  expect(session.identity()).toBeNull(); session.cancel(); expect(await intent).toBe(false);
});
it('computes nearest distances without sending coordinates to a provider', () => { setup(); expect(distanceKm(25,55,25,55)).toBe(0); expect(distanceKm(0,0,0,1)).toBeCloseTo(111.195,2); });

it('does not clear a newer login when an older mutation returns unauthorized', async () => {
  setup(); session.identity.set({ id: 'old', email: 'old@example.test', accountType: 'customer' });
  const old = session.mutate('POST', '/api/customer/bag', {}).catch(() => undefined);
  http.expectOne('http://localhost:5155/api/auth/csrf').flush({ token: 'old-token' }); await Promise.resolve();
  const pending = http.expectOne('http://localhost:5155/api/customer/bag');
  const login = session.authenticate('login', 'new@example.test', 'synthetic');
  http.expectOne('http://localhost:5155/api/auth/csrf').flush({ token: 'new-token' }); await Promise.resolve();
  http.expectOne('http://localhost:5155/api/auth/login').flush({ id: 'new', email: 'new@example.test', accountType: 'customer' }); await login;
  pending.flush({}, { status: 401, statusText: 'Unauthorized' }); await old;
  expect(session.identity()?.id).toBe('new');
});
