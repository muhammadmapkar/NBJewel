// Role: Lazy-loaded routes for the catalog feature (list now, detail page in part B).

import { Routes } from '@angular/router';

export const CATALOG_ROUTES: Routes = [
  { path: '', loadComponent: () => import('./catalog-list.component').then(m => m.CatalogListComponent) },
  { path: ':id', loadComponent: () => import('./item-detail-placeholder.component').then(m => m.ItemDetailPlaceholderComponent) }
];
