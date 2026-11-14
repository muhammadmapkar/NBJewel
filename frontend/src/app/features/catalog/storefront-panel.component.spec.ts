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
