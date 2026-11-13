// Role: Verifies that ERP route entry uses server identity and never admits customer or anonymous sessions.
import '@angular/compiler';
import { createEnvironmentInjector, EnvironmentInjector, runInInjectionContext } from '@angular/core';
import { ActivatedRouteSnapshot, Router, RouterStateSnapshot } from '@angular/router';
import { firstValueFrom, Observable, of, throwError } from 'rxjs';
import { describe, expect, it } from 'vitest';
import { AuthService, SessionIdentity } from './auth.service';
import { erpGuard } from './erp.guard';

describe('ERP route identity checks', () => {
  const identity: SessionIdentity = { id: '1', email: 'sample@example.test', accountType: 'erp', permissions: [], storeIds: [], allStores: false };
  async function check(session: Observable<SessionIdentity | null>, url = '/catalog/42'): Promise<unknown> {
    const injector = createEnvironmentInjector([
      { provide: AuthService, useValue: { loadSession: () => session } },
      { provide: Router, useValue: { createUrlTree: (paths: string[], options?: unknown) => ({ paths, options }) } }
    ], null as unknown as EnvironmentInjector);
    try {
      return await firstValueFrom(runInInjectionContext(injector, () => erpGuard({} as ActivatedRouteSnapshot, { url } as RouterStateSnapshot)) as Observable<unknown>);
    } finally { injector.destroy(); }
  }
  it('admits ERP identities returned by the server', async () => {
    expect(await check(of(identity))).toBe(true);
  });
  it('denies customer identities even when they carry permission strings', async () => {
    expect(await check(of({ ...identity, accountType: 'customer', permissions: ['erp.all'] }))).toEqual({ paths: ['/access-denied'], options: undefined });
  });
  it('sends anonymous users to sign-in with a local return path', async () => {
    expect(await check(of(null))).toEqual({ paths: ['/sign-in'], options: { queryParams: { returnUrl: '/catalog/42' } } });
    expect(await check(of(null), '//evil.example')).toEqual({ paths: ['/sign-in'], options: { queryParams: { returnUrl: '/home' } } });
  });
  it('fails closed when session lookup is unavailable', async () => {
    expect(await check(throwError(() => new Error('offline')))).toEqual({ paths: ['/sign-in'], options: { queryParams: { sessionError: '1' } } });
  });
});
