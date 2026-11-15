// Role: Hosts the shared storefront shell, business-unavailable state, customer dialog and navigation without retaining another shop's data.
import { Component, inject } from '@angular/core';
import { RouterOutlet } from '@angular/router';
import { AuthDialogComponent } from './customer/auth-dialog.component';
import { CustomerSession } from './customer/customer-session';
import { MobileNavComponent } from './ui/mobile-nav.component';
import { SiteHeaderComponent } from './ui/site-header.component';
import { CatalogService } from './catalog/catalog.service';
@Component({ selector: 'ct-root', standalone: true, imports: [RouterOutlet, AuthDialogComponent, MobileNavComponent, SiteHeaderComponent],
  host: { '[style.--gold]': "catalog.portal()?.colorPreset === 'silver' ? 'var(--muted)' : catalog.portal()?.colorPreset === 'rose' ? 'var(--maroon)' : null" },
  template: `@if (catalog.shopSlug() && catalog.state().status === 'error') { <ct-site-header /><main class="page"><h1 class="serif">Shop unavailable</h1><p>This shop is unavailable right now. Please try again later.</p><button type="button" class="btn" (click)="catalog.load(true)">Try again</button></main> } @else { @if (catalog.shopSlug() && catalog.state().status === 'loading') { <p role="status">Loading shop...</p> }<div [hidden]="catalog.shopSlug() && catalog.state().status === 'loading'"><router-outlet /></div> }<ct-mobile-nav /><ct-auth-dialog />` })
export class AppComponent { readonly catalog = inject(CatalogService); constructor() { void inject(CustomerSession).refresh(); } }
