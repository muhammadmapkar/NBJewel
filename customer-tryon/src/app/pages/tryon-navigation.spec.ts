// Role: Checks that camera design/piece changes retain the selected store in shareable URLs.
import '@angular/compiler';
import { signal } from '@angular/core';
import { expect, it, vi } from 'vitest';
import { TryonPage } from './tryon.page';
import { CatalogService } from '../catalog/catalog.service';
import { PieceKind, Product } from '../catalog/catalog.models';
it('keeps ERP store context when toggling pieces and changing designs', () => {
  const navigate = vi.fn(); const route = {};
  const page = { parts: signal<PieceKind[]>(['necklace', 'earrings']), productId: signal('7'), route,
    catalog: { shopSlug: () => 'sample-wholesale', link: CatalogService.prototype.link, linkParams: () => ({ source: 'erp', store: '2' }) }, router: { navigate } } as unknown as TryonPage;
  TryonPage.prototype.togglePart.call(page, 'earrings');
  expect(navigate).toHaveBeenLastCalledWith([], { relativeTo: route, queryParams: { source: 'erp', store: '2', parts: 'necklace' }, replaceUrl: true });
  TryonPage.prototype.switchTo.call(page, { id: '8' } as Product);
  expect(navigate).toHaveBeenLastCalledWith(['/shops', 'sample-wholesale', 'try', '8'], { queryParams: { source: 'erp', store: '2' }, replaceUrl: true });
});
