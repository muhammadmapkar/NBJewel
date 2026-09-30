// Role: Declares the application-wide router, HTTP client, error handling,
// and the data service used by the screens.

import { provideHttpClient, withInterceptors } from '@angular/common/http';
import { ApplicationConfig } from '@angular/core';
import { provideRouter, Routes } from '@angular/router';
import { HttpItemDataService } from './core/data/http-item-data.service';
import { ItemDataService } from './core/data/item-data.service';
import { errorInterceptor } from './core/http/error.interceptor';
import { ShellComponent } from './core/layout/shell.component';

const routes: Routes = [{
  path: '', component: ShellComponent, children: [
    { path: '', pathMatch: 'full', redirectTo: 'home' },
    { path: 'home', loadComponent: () => import('./features/dashboard/dashboard.component').then(m => m.DashboardComponent) },
    { path: 'scheme-master', loadComponent: () => import('./features/gold-scheme/scheme-master.component').then(m => m.SchemeMasterComponent) },
    { path: 'chit-master', loadComponent: () => import('./features/gold-scheme/chit-master.component').then(m => m.ChitMasterComponent) },
    { path: 'change-financial-year', loadComponent: () => import('./features/settings/financial-year.component').then(m => m.FinancialYearComponent) },
    { path: 'change-password', loadComponent: () => import('./features/settings/change-password.component').then(m => m.ChangePasswordComponent) }
  ]
}];

export const appConfig: ApplicationConfig = {
  providers: [
    provideRouter(routes),
    provideHttpClient(withInterceptors([errorInterceptor])),
    { provide: ItemDataService, useClass: HttpItemDataService }
  ]
};
