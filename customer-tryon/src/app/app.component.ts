// Role: Hosts routed pages, one customer authentication dialog and the shared mobile navigation.
import { Component, inject } from '@angular/core';
import { RouterOutlet } from '@angular/router';
import { AuthDialogComponent } from './customer/auth-dialog.component';
import { CustomerSession } from './customer/customer-session';
import { MobileNavComponent } from './ui/mobile-nav.component';
@Component({ selector: 'ct-root', standalone: true, imports: [RouterOutlet, AuthDialogComponent, MobileNavComponent], template: '<router-outlet /><ct-mobile-nav /><ct-auth-dialog />' })
export class AppComponent { constructor() { void inject(CustomerSession).refresh(); } }
