// Role: Lazy-loaded customer routes for the virtual try-on: the jewelry grid and the camera page.
// They are mounted outside the ERP shell in app.config.ts. The owner page lives inside the shell.

import { Routes } from '@angular/router';

export const TRYON_ROUTES: Routes = [
  { path: '', title: 'NB Jewel Try-On', loadComponent: () => import('./tryon-grid.component').then(m => m.TryonGridComponent) },
  { path: ':id', title: 'NB Jewel Try-On', loadComponent: () => import('./tryon-camera.component').then(m => m.TryonCameraComponent) }
];
