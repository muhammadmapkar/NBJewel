// Role: Lazy-loaded routes for the catalog feature (list, detail, new and edit).
// The "new" route must stay above ":id" so it is not read as an item id.

import { Routes } from '@angular/router';

export const CATALOG_ROUTES: Routes = [
  { path: '', loadComponent: () => import('./catalog-list.component').then(m => m.CatalogListComponent) },
  { path: 'new', loadComponent: () => import('./item-form.component').then(m => m.ItemFormComponent) },
  { path: ':id', loadComponent: () => import('./item-detail.component').then(m => m.ItemDetailComponent) },
  { path: ':id/edit', loadComponent: () => import('./item-form.component').then(m => m.ItemFormComponent) }
];
