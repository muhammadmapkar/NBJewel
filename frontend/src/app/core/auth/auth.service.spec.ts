// Role: Tests server session identity, CSRF, and API credential isolation.
import '@angular/compiler';
import { createEnvironmentInjector, EnvironmentInjector, ɵINJECTOR_SCOPE, provideZonelessChangeDetection } from '@angular/core';
import { Router } from '@angular/router';
import { provideHttpClient, withInterceptors } from '@angular/common/http';
import { HttpClient } from '@angular/common/http';
import { HttpTestingController, provideHttpClientTesting } from '@angular/common/http/testing';
import { afterEach, beforeEach, describe, expect, it } from 'vitest';
import { firstValueFrom } from 'rxjs';
import { environment } from '../../../environments/environment';
import { AuthService } from './auth.service';
import { authInterceptor } from './auth.interceptor';
import { errorInterceptor } from '../http/error.interceptor';

describe('server authentication', () => {
  let auth: AuthService;
  let http: HttpTestingController;
  let injector: EnvironmentInjector;
  const api = environment.apiBaseUrl;
  const erp = { id: '1', email: 'staff@example.test', accountType: 'erp' as const, permissions: [], storeIds: [], allStores: false };
  beforeEach(() => {
    injector = createEnvironmentInjector([provideZonelessChangeDetection(), { provide: ɵINJECTOR_SCOPE, useValue: 'root' }, AuthService, { provide: Router, useValue: { url: '/home', navigate: () => Promise.resolve(true) } }, provideHttpClient(withInterceptors([errorInterceptor, authInterceptor])), provideHttpClientTesting()], null as unknown as EnvironmentInjector);
    auth = injector.get(AuthService);
    http = injector.get(HttpTestingController);
  });
  afterEach(() => { http.verify(); injector.destroy(); });

  it('increments session revision across login, logout and same-user login', async () => {
    const initial = auth.sessionRevision();
    const login = firstValueFrom(auth.login('staff@example.test', 'password'));
    http.expectOne(`${api}/api/auth/csrf`).flush({ token: 'csrf' });
    http.expectOne(`${api}/api/auth/login`).flush(erp); await login;
    const firstRevision = auth.sessionRevision(); expect(firstRevision).toBeGreaterThan(initial);
    const logout = firstValueFrom(auth.logout());
    http.expectOne(`${api}/api/auth/csrf`).flush({ token: 'csrf' });
    http.expectOne(`${api}/api/auth/logout`).flush(null); await logout;
    expect(auth.sessionRevision()).toBeGreaterThan(firstRevision);
    const loggedOutRevision = auth.sessionRevision();
    const again = firstValueFrom(auth.login('staff@example.test', 'password'));
    http.expectOne(`${api}/api/auth/csrf`).flush({ token: 'csrf' });
    http.expectOne(`${api}/api/auth/login`).flush(erp); await again;
    expect(auth.sessionRevision()).toBeGreaterThan(loggedOutRevision);
  });
  it('does not authenticate a registration pending email verification', async () => {
    const revision = auth.sessionRevision();
    const registration = firstValueFrom(auth.register('customer@example.test', 'password'));
    http.expectOne(`${api}/api/auth/csrf`).flush({ token: 'csrf' });
    http.expectOne(`${api}/api/auth/register`).flush({ verificationRequired: true, message: 'Check your email to verify your account.' }, { status: 202, statusText: 'Accepted' });
    expect(await registration).toMatchObject({ verificationRequired: true });
    expect(auth.identity()).toBeNull(); expect(auth.sessionRevision()).toBe(revision);
  });
  it('loads identity from the server with credentials', async () => {
    const result = firstValueFrom(auth.loadSession());
    const request = http.expectOne(`${api}/api/auth/me`);
    expect(request.request.withCredentials).toBe(true);
    request.flush(erp);
    expect(await result).toEqual(erp);
    expect(auth.identity()).toEqual(erp);
  });
  it('fetches a CSRF token before login and keeps server identity', async () => {
    const result = firstValueFrom(auth.login('staff@example.test', 'password'));
    const csrf = http.expectOne(`${api}/api/auth/csrf`);
    expect(csrf.request.withCredentials).toBe(true);
    csrf.flush({ token: 'csrf-value' });
    const request = http.expectOne(`${api}/api/auth/login`);
    expect(request.request.headers.get('X-CSRF-TOKEN')).toBe('csrf-value');
    expect(request.request.body).toEqual({ email: 'staff@example.test', password: 'password' });
    request.flush(erp);
    await result;
    expect(auth.identity()).toEqual(erp);
  });
  it('treats a 401 session as anonymous', async () => {
    const result = firstValueFrom(auth.loadSession());
    http.expectOne(`${api}/api/auth/me`).flush({}, { status: 401, statusText: 'Unauthorized' });
    expect(await result).toBeNull();
    expect(auth.identity()).toBeNull();
  });
  it('registers a customer with CSRF and never grants a staff identity locally', async () => {
    const result = firstValueFrom(auth.register('customer@example.test', 'password'));
    http.expectOne(`${api}/api/auth/csrf`).flush({ token: 'registration-token' });
    const request = http.expectOne(`${api}/api/auth/register`);
    expect(request.request.headers.get('X-CSRF-TOKEN')).toBe('registration-token');
    expect(request.request.withCredentials).toBe(true);
    request.flush({ ...erp, accountType: 'customer' });
    await result;
    expect(auth.identity()?.accountType).toBe('customer');
  });
  it('clears identity only after a CSRF-protected logout succeeds', async () => {
    const session = firstValueFrom(auth.loadSession());
    http.expectOne(`${api}/api/auth/me`).flush(erp);
    await session;
    const logout = firstValueFrom(auth.logout());
    http.expectOne(`${api}/api/auth/csrf`).flush({ token: 'logout-token' });
    const request = http.expectOne(`${api}/api/auth/logout`);
    expect(request.request.headers.get('X-CSRF-TOKEN')).toBe('logout-token');
    expect(auth.identity()).toEqual(erp);
    request.flush(null);
    await logout;
    expect(auth.identity()).toBeNull();
  });
  it('preserves permission errors for session-aware screens', async () => {
    const result = firstValueFrom(injector.get(HttpClient).get(`${api}/api/items`));
    const rejected = expect(result).rejects.toMatchObject({ status: 403, message: 'Staff permission required.' });
    http.expectOne(`${api}/api/items`).flush({ message: 'Staff permission required.' }, { status: 403, statusText: 'Forbidden' });
    await rejected;
  });
  it('does not send credentials or CSRF tokens to lookalike hosts', async () => {
    const client = injector.get(HttpClient);
    const result = firstValueFrom(client.post(`${api}.evil.example/api/auth/logout`, {}));
    const request = http.expectOne(`${api}.evil.example/api/auth/logout`);
    expect(request.request.withCredentials).toBe(false);
    expect(request.request.headers.has('X-CSRF-TOKEN')).toBe(false);
    http.expectNone(`${api}/api/auth/csrf`);
    request.flush({});
    await result;
  });
});
