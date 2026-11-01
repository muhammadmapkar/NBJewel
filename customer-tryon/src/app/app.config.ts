// Role: App wide providers (router with scroll restore, HTTP client) and the routes: home,
// catalogue, product page, camera try-on, bag, wishlist and the reservation confirmation.

import { provideHttpClient } from '@angular/common/http';
import { ApplicationConfig } from '@angular/core';
import { Routes, provideRouter, withInMemoryScrolling } from '@angular/router';

const routes: Routes = [
  { path: '', title: 'NB Jewel | Try on gold jewellery', loadComponent: () => import('./pages/home.page').then(m => m.HomePage) },
  { path: 'shop', title: 'Shop | NB Jewel', loadComponent: () => import('./pages/shop.page').then(m => m.ShopPage) },
  { path: 'p/:id', title: 'NB Jewel', loadComponent: () => import('./pages/product.page').then(m => m.ProductPage) },
  { path: 'bag', title: 'Bag | NB Jewel', loadComponent: () => import('./pages/bag.page').then(m => m.BagPage) },
  { path: 'wishlist', title: 'Wishlist | NB Jewel', loadComponent: () => import('./pages/wishlist.page').then(m => m.WishlistPage) },
  { path: 'reserved', title: 'Reserved | NB Jewel', loadComponent: () => import('./pages/reserved.page').then(m => m.ReservedPage) },
  { path: 'try/:id', title: 'Try on | NB Jewel', loadComponent: () => import('./pages/tryon.page').then(m => m.TryonPage) },
  { path: '**', redirectTo: '' }
];

export const appConfig: ApplicationConfig = {
  providers: [
    provideRouter(routes, withInMemoryScrolling({ scrollPositionRestoration: 'enabled' })),
    provideHttpClient()
  ]
};
