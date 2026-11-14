// Role: Displays and edits the signed-in customer's own details, saved reservation requests, wishlist and bag summary.
import { Component, effect, inject } from '@angular/core';
import { FormsModule } from '@angular/forms';
import { Router, RouterLink } from '@angular/router';
import { CustomerSession, customerError } from '../customer/customer-session';
import { CatalogService } from '../catalog/catalog.service';
import { StoreService } from '../catalog/store.service';
import { SiteHeaderComponent } from '../ui/site-header.component';
@Component({ selector: 'ct-profile', standalone: true, imports: [FormsModule, RouterLink, SiteHeaderComponent], template: `
<ct-site-header [back]="true" /><main class="portal-page"><p class="eyebrow">Your NB JEWEL</p><h1 class="serif">Customer profile</h1>
@if (session.identity(); as user) {
<p>{{ user.email }}</p><form class="portal-card" (ngSubmit)="save()"><h2 class="serif">Your details</h2>
<label>Name<input name="name" [(ngModel)]="name" required maxlength="100" autocomplete="name" /></label>
<label>Phone<input name="phone" [(ngModel)]="phone" maxlength="30" type="tel" autocomplete="tel" /></label>
<button class="btn" [disabled]="store.working()">Save details</button></form>
<div class="summary-links"><a class="portal-card" routerLink="/bag" [queryParams]="catalog.linkParams()"><b>{{ store.bagCount() }}</b> designs in your bag</a><a class="portal-card" routerLink="/wishlist" [queryParams]="catalog.linkParams()"><b>{{ store.wishlist().length }}</b> saved favourites on this device</a></div>
<section class="portal-card"><h2 class="serif">Store reservations</h2><p class="muted">Requests await store confirmation. No stock hold or payment has been made.</p>
@for (r of store.reservations(); track r.reference) { <article><b>{{ r.reference }}</b><p>{{ r.branch }} · {{ r.date }}</p><p>{{ r.items.length }} designs · {{ r.status }}</p></article> } @empty { <p>No reservation requests yet.</p> }</section>
<section class="portal-card"><h2 class="serif">Saved designs</h2>
@for (id of store.wishlist(); track id) { <p><a [routerLink]="['/p', id]" [queryParams]="catalog.linkParams()">{{ catalog.find(id)?.name ?? id }}</a></p> } @empty { <p>No saved designs in this catalog yet.</p> }</section>
<section class="portal-card"><h2 class="serif">Order summary</h2><p>There are no paid orders. Your bag and reservation requests help you plan an in-store visit.</p></section>
<button class="btn ghost" (click)="logout()">Sign out</button>
} @else { <p>Sign in to view your saved details.</p><button class="btn" (click)="session.require('Sign in to your profile')">Sign in</button> }
@if (notice || store.error()) { <p role="status">{{ notice || store.error() }}</p> }</main>`, styles: [`label { display:grid; gap:6px; margin:14px 0; } input { min-height:48px; padding:12px; border:1px solid var(--line); border-radius:10px; font:inherit; } .summary-links { display:flex; flex-wrap:wrap; gap:16px; } .summary-links a { flex:1; color:var(--maroon); text-decoration:none; min-width:200px; } article { border-top:1px solid var(--line); padding:16px 0; }`] })
export class ProfilePage {
  readonly catalog = inject(CatalogService); readonly store = inject(StoreService); readonly session = inject(CustomerSession); private readonly router = inject(Router);
  name = ''; phone = ''; notice = '';
  constructor() { this.catalog.load(); effect(() => { const data = this.store.profile(); this.name = data.name; this.phone = data.phone; }); }
  async save(): Promise<void> { this.notice = await this.store.saveProfile(this.name, this.phone) ? 'Your details are saved.' : ''; }
  async logout(): Promise<void> { try { await this.session.logout(); await this.router.navigate(['/']); } catch (error) { this.notice = customerError(error); } }
}
