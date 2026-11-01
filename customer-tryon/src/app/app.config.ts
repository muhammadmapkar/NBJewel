// Role: App wide providers (router with scroll restore, HTTP client) and the three routes:
// catalog (home), product page, and the camera try-on.

import { provideHttpClient } from '@angular/common/http';
import { ApplicationConfig } from '@angular/core';
import { Routes, provideRouter, withInMemoryScrolling } from '@angular/router';

const routes: Routes = [
  { path: '', title: 'NB Jewel Try-On', loadComponent: () => import('./pages/home.page').then(m => m.HomePage) },
  { path: 'p/:id', title: 'NB Jewel Try-On', loadComponent: () => import('./pages/product.page').then(m => m.ProductPage) },
  { path: 'try/:id', title: 'Try on | NB Jewel', loadComponent: () => import('./pages/tryon.page').then(m => m.TryonPage) },
  { path: '**', redirectTo: '' }
];

export const appConfig: ApplicationConfig = {
  providers: [
    provideRouter(routes, withInMemoryScrolling({ scrollPositionRestoration: 'enabled' })),
    provideHttpClient()
  ]
};
