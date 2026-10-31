// Role: Declares the application-wide router, HTTP client, error handling,
// and the data service used by the screens. The customer try-on (/tryon) sits outside the shell;
// the owner try-on page (/owner/jewelry) sits inside it.

import { provideHttpClient, withInterceptors } from '@angular/common/http';
import { ApplicationConfig } from '@angular/core';
import { provideRouter, Routes } from '@angular/router';
import { HttpItemDataService } from './core/data/http-item-data.service';
import { InMemorySampleDataService } from './core/data/in-memory-sample-data.service';
import { ItemDataService } from './core/data/item-data.service';
import { errorInterceptor } from './core/http/error.interceptor';
import { ShellComponent } from './core/layout/shell.component';

const routes: Routes = [
  // Customer try-on pages, outside the ERP shell (no ERP menu, no chatbot).
  { path: 'tryon', loadChildren: () => import('./features/tryon/tryon.routes').then(m => m.TRYON_ROUTES) },
  {
  path: '', component: ShellComponent, children: [
    { path: '', pathMatch: 'full', redirectTo: 'home' },
    { path: 'home', loadComponent: () => import('./features/dashboard/dashboard.component').then(m => m.DashboardComponent) },
    { path: 'scheme-master', loadComponent: () => import('./features/gold-scheme/scheme-master.component').then(m => m.SchemeMasterComponent) },
    { path: 'chit-master', loadComponent: () => import('./features/gold-scheme/chit-master.component').then(m => m.ChitMasterComponent) },
    { path: 'catalog', loadChildren: () => import('./features/catalog/catalog.routes').then(m => m.CATALOG_ROUTES) },
    { path: 'change-financial-year', loadComponent: () => import('./features/settings/financial-year.component').then(m => m.FinancialYearComponent) },
    { path: 'change-password', loadComponent: () => import('./features/settings/change-password.component').then(m => m.ChangePasswordComponent) },
    { path: 'owner/jewelry', loadComponent: () => import('./features/tryon/owner-jewelry.component').then(m => m.OwnerJewelryComponent) }
  ]
}];

export const appConfig: ApplicationConfig = {
  providers: [
    provideRouter(routes),
    provideHttpClient(withInterceptors([errorInterceptor])),
    // To swap the data source, change useClass here (for example to a SQL or real ERP service).
    InMemorySampleDataService,
    { provide: ItemDataService, useClass: HttpItemDataService }
  ]
};
