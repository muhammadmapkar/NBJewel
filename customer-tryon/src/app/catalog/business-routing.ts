// Role: Restores assumed shop context before rendering and redirects legacy URLs without losing valid product/store/try-on selections.
import { CanActivateFn, DefaultUrlSerializer, Router, UrlTree } from '@angular/router';
import { inject } from '@angular/core';
import { CatalogService } from './catalog.service';
import { StoreService } from './store.service';
import { StoreDirectoryService } from '../customer/store-directory.service';
export function legacyShopUrl(url: string): UrlTree {
  const serializer = new DefaultUrlSerializer(); const original = serializer.parse(url);
  const serialized = serializer.serialize(original); const path = serialized.split(/[?#]/, 1)[0];
  const target = serializer.parse(`/shops/nb-jewel-demo${path === '/' ? '' : path}`);
  target.queryParams = { ...original.queryParams }; delete target.queryParams['source'];
  if (original.queryParams['source'] === 'demo') delete target.queryParams['store'];
  target.fragment = original.fragment; return target;
}
export const legacyPortalRedirect: CanActivateFn = (_route, state) => legacyShopUrl(state.url);
export const shopContextGuard: CanActivateFn = async route => {
  const catalog = inject(CatalogService); const store = inject(StoreService); const directory = inject(StoreDirectoryService); const router = inject(Router);
  const slug = route.paramMap.get('slug') ?? route.pathFromRoot.map(parent => parent.paramMap.get('slug')).find(value => !!value);
  if (!slug) return false;
  catalog.selectShop(slug, route.queryParamMap.get('source') === 'demo' ? null : route.queryParamMap.get('store'));
  store.selectShop(slug); directory.selectShop(slug); await catalog.whenReady();
  if (slug !== catalog.shopSlug()) return false;
  if (route.routeConfig?.path?.startsWith('try/') && !catalog.actionEnabled('tryOn')) return router.createUrlTree(catalog.link('shop'), { queryParams: catalog.linkParams() });
  return true;
};
