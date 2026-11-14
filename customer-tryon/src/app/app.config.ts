// Role: App wide providers (router with scroll restore, HTTP client) and the routes: home,
// catalogue, product page, camera try-on, bag, wishlist and the reservation confirmation.

import { DOCUMENT } from '@angular/common';
import { HttpClient, provideHttpClient } from '@angular/common/http';
import { ApplicationConfig, inject, provideAppInitializer } from '@angular/core';
import { Routes, provideRouter, withInMemoryScrolling } from '@angular/router';

import { catalogContextGuard } from './catalog/catalog.service';
import { customerGuard } from './customer/customer-session';
import { STOREFRONT_CONFIG, loadStorefrontConfig } from './config/storefront-config';

const routes: Routes = [
  { path: '', title: 'NB Jewel | Try on gold jewellery', loadComponent: () => import('./pages/home.page').then(m => m.HomePage) },
  { path: 'shop', title: 'Shop | NB Jewel', loadComponent: () => import('./pages/shop.page').then(m => m.ShopPage) },
  { path: 'p/:id', canActivate: [catalogContextGuard], runGuardsAndResolvers: 'paramsOrQueryParamsChange', title: 'NB Jewel', loadComponent: () => import('./pages/product.page').then(m => m.ProductPage) },
  { path: 'bag', canActivate: [catalogContextGuard], runGuardsAndResolvers: 'paramsOrQueryParamsChange', title: 'Bag | NB Jewel', loadComponent: () => import('./pages/bag.page').then(m => m.BagPage) },
  { path: 'wishlist', canActivate: [catalogContextGuard], runGuardsAndResolvers: 'paramsOrQueryParamsChange', title: 'Wishlist | NB Jewel', loadComponent: () => import('./pages/wishlist.page').then(m => m.WishlistPage) },
  { path: 'reserved', canActivate: [catalogContextGuard], runGuardsAndResolvers: 'paramsOrQueryParamsChange', title: 'Reserved | NB Jewel', loadComponent: () => import('./pages/reserved.page').then(m => m.ReservedPage) },
  { path: 'try/:id', canActivate: [catalogContextGuard, customerGuard], runGuardsAndResolvers: 'paramsOrQueryParamsChange', title: 'Try on | NB Jewel', loadComponent: () => import('./pages/tryon.page').then(m => m.TryonPage) },
  { path: 'stores', title: 'Stores | NB Jewel', loadComponent: () => import('./pages/stores.page').then(m => m.StoresPage) },
  { path: 'profile', canActivate: [catalogContextGuard], runGuardsAndResolvers: 'paramsOrQueryParamsChange', title: 'Your profile | NB Jewel', loadComponent: () => import('./pages/profile.page').then(m => m.ProfilePage) },
  { path: 'verify-email', loadComponent: () => import('./pages/verify-email.page').then(m => m.VerifyEmailPage) },
  { path: '**', redirectTo: '' }
];

export const appConfig: ApplicationConfig = {
  providers: [
    provideRouter(routes, withInMemoryScrolling({ scrollPositionRestoration: 'enabled' })),
    provideHttpClient(),
    provideAppInitializer(() => loadStorefrontConfig(inject(HttpClient), inject(STOREFRONT_CONFIG), inject(DOCUMENT).baseURI))
  ]
};
