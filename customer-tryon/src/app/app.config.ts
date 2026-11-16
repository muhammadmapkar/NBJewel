// Role: App wide providers (router with scroll restore, HTTP client) and the routes: home,
// catalogue, product page, camera try-on, bag, wishlist and the reservation confirmation.

import { DOCUMENT } from '@angular/common';
import { HttpClient, provideHttpClient } from '@angular/common/http';
import { ApplicationConfig, inject, provideAppInitializer } from '@angular/core';
import { Routes, provideRouter, withInMemoryScrolling } from '@angular/router';

import { legacyPortalRedirect, previewContextGuard, shopContextGuard } from './catalog/business-routing';
import { customerGuard } from './customer/customer-session';
import { STOREFRONT_CONFIG, loadStorefrontConfig } from './config/storefront-config';

const home = () => import('./pages/home.page').then(m => m.HomePage);
export const storefrontRoutes: Routes = [
  ...['', 'shop', 'p/:id', 'bag', 'wishlist', 'reserved', 'try/:id', 'stores', 'profile'].map(path => ({ path, pathMatch: 'full' as const, canActivate: [legacyPortalRedirect], loadComponent: home })),
  { path: 'shops/:slug', runGuardsAndResolvers: 'paramsOrQueryParamsChange', children: [
    { path: '', canActivate: [shopContextGuard], loadComponent: home },
    { path: 'preview', canActivateChild: [previewContextGuard], runGuardsAndResolvers: 'paramsOrQueryParamsChange', children: [
      { path: '', loadComponent: home },
      { path: 'shop', loadComponent: () => import('./pages/shop.page').then(m => m.ShopPage) },
      { path: 'p/:id', loadComponent: () => import('./pages/product.page').then(m => m.ProductPage) },
      { path: 'stores', loadComponent: () => import('./pages/stores.page').then(m => m.StoresPage) },
      { path: '**', redirectTo: '' }
    ] },
    { path: 'shop', canActivate: [shopContextGuard], runGuardsAndResolvers: 'paramsOrQueryParamsChange', loadComponent: () => import('./pages/shop.page').then(m => m.ShopPage) },
    { path: 'p/:id', canActivate: [shopContextGuard], runGuardsAndResolvers: 'paramsOrQueryParamsChange', loadComponent: () => import('./pages/product.page').then(m => m.ProductPage) },
    { path: 'bag', canActivate: [shopContextGuard], runGuardsAndResolvers: 'paramsOrQueryParamsChange', loadComponent: () => import('./pages/bag.page').then(m => m.BagPage) },
    { path: 'wishlist', canActivate: [shopContextGuard], runGuardsAndResolvers: 'paramsOrQueryParamsChange', loadComponent: () => import('./pages/wishlist.page').then(m => m.WishlistPage) },
    { path: 'reserved', canActivate: [shopContextGuard], runGuardsAndResolvers: 'paramsOrQueryParamsChange', loadComponent: () => import('./pages/reserved.page').then(m => m.ReservedPage) },
    { path: 'try/:id', canActivate: [shopContextGuard, customerGuard], runGuardsAndResolvers: 'paramsOrQueryParamsChange', loadComponent: () => import('./pages/tryon.page').then(m => m.TryonPage) },
    { path: 'stores', canActivate: [shopContextGuard], loadComponent: () => import('./pages/stores.page').then(m => m.StoresPage) },
    { path: 'profile', canActivate: [shopContextGuard], loadComponent: () => import('./pages/profile.page').then(m => m.ProfilePage) },
    { path: '**', redirectTo: '' }
  ] },
  { path: 'verify-email', loadComponent: () => import('./pages/verify-email.page').then(m => m.VerifyEmailPage) },
  { path: '**', redirectTo: '/shops/nb-jewel-demo' }
];

export const appConfig: ApplicationConfig = {
  providers: [
    provideRouter(storefrontRoutes, withInMemoryScrolling({ scrollPositionRestoration: 'enabled' })),
    provideHttpClient(),
    provideAppInitializer(() => loadStorefrontConfig(inject(HttpClient), inject(STOREFRONT_CONFIG), inject(DOCUMENT).baseURI))
  ]
};
