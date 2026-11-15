// Role: Verifies assumed shop-specific wishlist keys, safe ERP-only migration and stale-state isolation.
import '@angular/compiler';
import { createEnvironmentInjector, EnvironmentInjector, ɵINJECTOR_SCOPE, provideZonelessChangeDetection, signal } from '@angular/core';
import { beforeEach, afterEach, expect, it, vi } from 'vitest';
import { StoreService, CustomerState } from './store.service';
import { CustomerSession } from '../customer/customer-session';
import { CatalogService } from './catalog.service';
let injector: EnvironmentInjector;
beforeEach(() => { const values = new Map<string, string>(); vi.stubGlobal('localStorage', {
  getItem: (key: string) => values.get(key) ?? null, setItem: (key: string, value: string) => values.set(key, value), clear: () => values.clear()
}); });
function setup(session: object = { identity: signal(null) }) {
  injector = createEnvironmentInjector([provideZonelessChangeDetection(), { provide: ɵINJECTOR_SCOPE, useValue: 'root' }, StoreService,
    { provide: CustomerSession, useValue: session }, { provide: CatalogService, useValue: { source: signal('erp') } }], null as unknown as EnvironmentInjector);
  return injector.get(StoreService);
}
afterEach(() => { injector?.destroy(); vi.unstubAllGlobals(); });
it('copies only ERP wishlist IDs to the demo v3 key and keeps other shops independent', () => {
  localStorage.setItem('nbjewel.wishlist.v2.erp.guest', '["7"]'); localStorage.setItem('nbjewel.wishlist.v2.demo.guest', '["old-demo"]');
  const store = setup(); store.selectShop('nb-jewel-demo'); expect(store.wishlist()).toEqual(['7']);
  expect(localStorage.getItem('nbjewel.wishlist.v3.nb-jewel-demo.guest')).toBe('["7"]');
  expect(localStorage.getItem('nbjewel.wishlist.v2.erp.guest')).toBe('["7"]');
  store.selectShop('sample-wholesale'); expect(store.wishlist()).toEqual([]); store.toggleWishlist('9');
  expect(localStorage.getItem('nbjewel.wishlist.v3.sample-wholesale.guest')).toBe('["9"]');
  store.selectShop('nb-jewel-demo'); expect(store.wishlist()).toEqual(['7']); expect(store.wishlist()).not.toContain('old-demo');
});
it('does not repeat v2 migration when a v3 wishlist was deliberately cleared', () => {
  localStorage.setItem('nbjewel.wishlist.v2.erp.guest', '["7"]'); localStorage.setItem('nbjewel.wishlist.v3.nb-jewel-demo.guest', '[]');
  const store = setup(); store.selectShop('nb-jewel-demo'); expect(store.wishlist()).toEqual([]);
});

it('rejects a late response from the previous shop after clearing its state', async () => {
  let finish!: (value: CustomerState) => void;
  const old = new Promise<CustomerState>(resolve => { finish = resolve; });
  const store = setup({ identity: signal({ id: 'customer-1' }), read: (path: string) => path.includes('/nb-jewel-demo/') ? old : Promise.resolve({ name: 'Wholesale customer', phone: '', bag: [{ id: '9', source: 'erp' }], reservations: [] }) });
  store.selectShop('nb-jewel-demo'); store.selectShop('sample-wholesale'); await Promise.resolve();
  expect(store.bag()).toEqual(['9']);
  finish({ name: 'Old shop customer', phone: '', bag: [{ id: '7', source: 'erp' }], reservations: [] }); await Promise.resolve();
  expect(store.bag()).toEqual(['9']); expect(store.profile().name).toBe('Wholesale customer');
});
it('does not resume an add intent into another shop after sign-in', async () => {
  let finish!: (value: boolean) => void;
  const intent = new Promise<boolean>(resolve => { finish = resolve; });
  const store = setup({ identity: signal(null), require: () => intent, mutate: async () => ({ name: '', phone: '', bag: [{ id: '7', source: 'erp' }], reservations: [] }) });
  store.selectShop('nb-jewel-demo'); const pending = store.addToBag('7'); store.selectShop('sample-wholesale'); finish(true);
  expect(await pending).toBe(false); expect(store.bag()).toEqual([]);
});
