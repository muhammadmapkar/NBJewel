// Role: Exercises saved-copy events through panel teardown/recreation and parent item state.
import '@angular/compiler';
import { createEnvironmentInjector, EnvironmentInjector, ɵINJECTOR_SCOPE, provideZonelessChangeDetection, runInInjectionContext, signal } from '@angular/core';
import { ActivatedRoute, convertToParamMap } from '@angular/router';
import { of } from 'rxjs';
import { expect, it, vi } from 'vitest';
import { AuthService } from '../../core/auth/auth.service';
import { ItemDataService } from '../../core/data/item-data.service';
import { StorefrontAdminService } from '../../core/data/storefront-admin.service';
import { Item } from '../../core/models';
import { ItemDetailComponent } from './item-detail.component';
import { StorefrontPanelComponent } from './storefront-panel.component';

it('keeps saved copy in the parent when Images is destroyed and recreated', () => {
  const original = { id: 7, storefront: { description: 'Original sample', collection: 'Old' } } as Item;
  const saveCopy = vi.fn((item: Item, storefront: Item['storefront']) => of({ ...item, storefront }));
  vi.stubGlobal('history', { state: null });
  const injector = createEnvironmentInjector([
    provideZonelessChangeDetection(), { provide: ɵINJECTOR_SCOPE, useValue: 'root' },
    { provide: AuthService, useValue: { identity: signal(null) } },
    { provide: StorefrontAdminService, useValue: { saveCopy, asset: () => of(null), state: () => of({ published: false }) } },
    { provide: ItemDataService, useValue: { getCategories: () => of([]), getItem: () => of(original), getPrice: () => of({}) } },
    { provide: ActivatedRoute, useValue: { paramMap: of(convertToParamMap({ id: '7' })) } }
  ], null as unknown as EnvironmentInjector);
  try {
    const parent = runInInjectionContext(injector, () => new ItemDetailComponent());
    const openImages = () => {
      parent.tab.set('images');
      const child = runInInjectionContext(injector, () => new StorefrontPanelComponent());
      Object.defineProperty(child, 'item', { value: parent.item });
      child.itemSaved.subscribe(value => parent.storefrontSaved(value));
      child.load(); return child;
    };
    const first = openImages(); first.description = 'Saved sample copy'; first.collection = 'Updated'; first.saveCopy();
    first.ngOnDestroy(); parent.tab.set('overview');
    expect(parent.item()?.storefront?.description).toBe('Saved sample copy');
    const reopened = openImages(); expect(reopened.description).toBe('Saved sample copy'); expect(reopened.collection).toBe('Updated');
    reopened.saveCopy(); expect(saveCopy.mock.calls[1][1]?.description).toBe('Saved sample copy'); reopened.ngOnDestroy();
  } finally { injector.destroy(); vi.unstubAllGlobals(); }
});

it('preserves manual edits while AI responds and rejects stale image replies', async () => {
  const { Subject } = await import('rxjs');
  const response = new Subject<{ revisionId: string; description: string; collection: string; cameraTrimPercent?: number }>();
  const injector = createEnvironmentInjector([
    provideZonelessChangeDetection(), { provide: ɵINJECTOR_SCOPE, useValue: 'root' },
    { provide: AuthService, useValue: { identity: signal({ accountType: 'erp', allStores: true, permissions: ['items.read','items.write','prices.read','status.read'] }) } },
    { provide: StorefrontAdminService, useValue: { analyze: () => response, asset: () => of(null), state: () => of({ published: false }) } }
  ], null as unknown as EnvironmentInjector);
  try {
    const panel = runInInjectionContext(injector, () => new StorefrontPanelComponent());
    Object.defineProperty(panel, 'item', { value: signal({ id: 7 } as Item) });
    Object.defineProperty(panel, 'categoryName', { value: signal('Necklaces') });
    panel.asset.set({ itemId: 7, revisionId: 'one', state: 'ready', qualityReview: 'pending', licenseStatus: 'open', width: 1, height: 1 });
    panel.preview.set('blob:test'); panel.analyze(); panel.description = 'Retailer edit';
    panel.updateCameraTrim(18);
    response.next({ revisionId: 'one', description: 'AI copy', collection: 'Temple', cameraTrimPercent: 25 });
    expect(panel.description).toBe('Retailer edit'); expect(panel.collection).toBe('Temple');
    expect(panel.cameraTrim()).toBe(18);
    expect(panel.reviewablePreview()).toBe(false);
    panel.analyze(); panel.asset.update(x => ({ ...x!, revisionId: 'two' }));
    response.next({ revisionId: 'one', description: 'Old copy', collection: 'Bridal', cameraTrimPercent: 31 });
    expect(panel.description).toBe('Retailer edit'); expect(panel.collection).toBe('Temple');
    expect(panel.cameraTrim()).toBe(18);
    panel.ngOnDestroy();
  } finally { injector.destroy(); }
});
