// Role: The bag (/bag): chosen designs with prices and the total, then two ways to buy without
// online payment: reserve in a store (name, phone, branch, date) or enquire on WhatsApp.
// Demo only: the reservation stays on this phone and is not sent to the shop (assumed).

import { Component, computed, inject, signal } from '@angular/core';
import { FormsModule } from '@angular/forms';
import { Router, RouterLink } from '@angular/router';
import { StoreDirectoryService } from '../customer/store-directory.service';
import { Product } from '../catalog/catalog.models';
import { CatalogService } from '../catalog/catalog.service';
import { StoreService } from '../catalog/store.service';
import { IconComponent } from '../ui/icon.component';
import { SiteHeaderComponent } from '../ui/site-header.component';

@Component({
  selector: 'ct-bag', standalone: true, imports: [FormsModule, RouterLink, IconComponent, SiteHeaderComponent],
  template: `
    <ct-site-header [back]="true" />
    <main class="page">
      <h1 class="serif">Your bag</h1>
      @if (store.error()) { <p role="alert">{{ store.error() }}</p> }
      @if (!store.session.identity()) { <button class="btn ghost" (click)="store.session.require('Sign in to view your bag')">Sign in to your bag</button> }
      @if (unavailable().length && catalog.state().status === 'ready') {
        <p>Some saved designs are no longer available:</p>
        @for (id of unavailable(); track id) { <p>{{ id }} <button class="chip" [disabled]="!catalog.actionEnabled('bag')" (click)="store.removeFromBag(id)">Remove</button></p> }
      }
      @if (!items().length) {
        <div class="empty">
          <p>Your bag is empty.</p>
          <a class="btn" [routerLink]="catalog.link('shop')" [queryParams]="catalog.linkParams()">Browse the collection</a>
        </div>
      } @else {
        <ul class="items">
          @for (p of items(); track p.id) {
            <li>
              <a class="thumb" [routerLink]="catalog.link('p', p.id)" [queryParams]="catalog.linkParams()"><img [src]="catalog.url(p.card)" [alt]="p.name" /></a>
              <div class="info">
                <a class="name serif" [routerLink]="catalog.link('p', p.id)" [queryParams]="catalog.linkParams()">{{ p.name }}</a>
                <span class="meta">{{ p.code }} · {{ p.purity }} · {{ p.grossWeight }} g</span>
                @if (catalog.showPrices()) { <b>{{ catalog.money(catalog.price(p).total) }}</b> }
                <div class="links">
                  @if (catalog.actionEnabled('tryOn')) { <a [routerLink]="catalog.link('try', p.id)" [queryParams]="catalog.linkParams()">Try on</a> }
                  <button type="button" [disabled]="!catalog.actionEnabled('bag')" (click)="moveToWishlist(p)">Move to wishlist</button>
                  <button type="button" [disabled]="!catalog.actionEnabled('bag')" (click)="store.removeFromBag(p.id)">Remove</button>
                </div>
              </div>
            </li>
          }
        </ul>

        @if (catalog.showPrices()) { <div class="summary">
          <div><span>{{ items().length }} {{ items().length === 1 ? 'design' : 'designs' }}</span><span>{{ catalog.money(total()) }}</span></div>
          <div class="muted"><span>Includes VAT</span><span>{{ catalog.money(vat()) }}</span></div>
          <div class="grand"><span>Total</span><span>{{ catalog.money(total()) }}</span></div>
          <p class="muted small">Final price is set on the day of purchase by that day's gold rate. Prices here use a sample rate.</p>
        </div> }

        @if (catalog.actionEnabled('reservations')) { <form class="reserve" (ngSubmit)="reserve()" #f="ngForm">
          <h2 class="serif">Reserve in store</h2>
          <p class="muted">Request a store visit for these designs. The store must confirm availability; no payment or stock hold is made.</p>
          <label>Full name<input name="name" [(ngModel)]="name" required autocomplete="name" /></label>
          <label>Mobile number<input name="phone" [(ngModel)]="phone" required type="tel" inputmode="tel" autocomplete="tel" /></label>
          <label>Store
            <select name="branch" [(ngModel)]="branch" required>
              <option value="" disabled>Choose a store</option>
              @for (b of branches(); track b.location.id) { <option [value]="b.location.id">{{ b.location.name }}</option> }
            </select>
          </label>
          <label>Visit date<input name="date" [(ngModel)]="date" required type="date" [min]="today" /></label>
          @if (error()) { <p class="error">{{ error() }}</p> }
          <button type="submit" class="btn" [disabled]="store.working()">Reserve {{ items().length }} {{ items().length === 1 ? 'design' : 'designs' }}</button>
          <p class="muted small">Demo: the reservation is not sent to a real store.</p>
        </form> }
        @if (catalog.actionEnabled('enquiries')) { <a class="btn wa" [href]="whatsapp()" target="_blank" rel="noopener"><ct-icon name="chat" /> Enquire on WhatsApp</a> }
      }
    </main>
  `,
  styles: [`
    :host { display: block; min-height: 100vh; }
    .page { max-width: 720px; margin: 0 auto; padding: 16px 16px 40px; }
    h1 { margin: 4px 0 14px; font-size: 32px; }
    .empty { padding: 40px 0; text-align: center; color: var(--muted); display: grid; gap: 14px; justify-items: center; }
    .items { list-style: none; padding: 0; margin: 0; display: grid; gap: 12px; }
    .items li { display: grid; grid-template-columns: 96px 1fr; gap: 12px; padding: 10px; border-radius: var(--radius); background: var(--card); box-shadow: var(--shadow); }
    .thumb { display: grid; place-items: center; aspect-ratio: 1; border-radius: 12px; background: radial-gradient(circle, #fff 0%, var(--cream) 75%); padding: 8px; }
    .thumb img { max-height: 100%; object-fit: contain; }
    .info { display: flex; flex-direction: column; gap: 3px; min-width: 0; }
    .name { color: var(--ink); text-decoration: none; font-size: 18px; font-weight: 600; }
    .meta { color: var(--muted); font-size: 12px; }
    .info b { color: var(--maroon); }
    .links { display: flex; flex-wrap: wrap; gap: 12px; margin-top: 4px; }
    .links a, .links button { border: 0; background: none; padding: 0; color: var(--maroon); font-size: 13px; font-weight: 600; text-decoration: none; }
    .summary { margin-top: 16px; padding: 14px; border-radius: var(--radius); background: var(--card); box-shadow: var(--shadow); display: grid; gap: 6px; }
    .summary div { display: flex; justify-content: space-between; }
    .grand { padding-top: 8px; border-top: 1px solid var(--line); font-weight: 700; font-size: 18px; color: var(--maroon); }
    .muted { color: var(--muted); }
    .small { font-size: 12px; margin: 4px 0 0; }
    .reserve { margin-top: 18px; padding: 16px; border-radius: var(--radius); background: var(--card); box-shadow: var(--shadow); display: grid; gap: 12px; }
    .reserve h2 { margin: 0; font-size: 24px; }
    .reserve p { margin: 0; }
    label { display: grid; gap: 6px; font-size: 13px; font-weight: 600; }
    input, select { min-height: 46px; padding: 0 12px; border: 1px solid var(--line); border-radius: 12px; background: #fff; font: inherit; font-weight: 400; color: var(--ink); }
    input:focus, select:focus { outline: 2px solid var(--gold); outline-offset: 1px; }
    .error { color: #a3261b; font-size: 13px; }
    .wa { background: #1f7a45; }
    .wa ct-icon { width: 20px; height: 20px; }
  `]
})
export class BagPage {
  readonly catalog = inject(CatalogService);
  readonly store = inject(StoreService);
  private readonly router = inject(Router);
  readonly items = computed(() => this.store.bag().map(id => this.catalog.find(id)).filter((p): p is Product => !!p));
  readonly unavailable = computed(() => this.store.bag().filter(id => !this.catalog.find(id)));
  readonly total = computed(() => this.items().reduce((sum, p) => sum + this.catalog.price(p).total, 0));
  readonly vat = computed(() => this.items().reduce((sum, p) => sum + this.catalog.price(p).vat, 0));
  readonly directory = inject(StoreDirectoryService);
  readonly branches = computed(() => this.directory.stores().filter(x => x.location.source === this.store.source));
  private requestId = crypto.randomUUID();
  readonly error = signal<string | null>(null);
  readonly today = new Date().toISOString().slice(0, 10);

  name = '';
  phone = '';
  branch = '';
  date = '';

  constructor() { this.catalog.load(); void this.directory.load(); }

  moveToWishlist(p: Product): void {
    if (!this.store.inWishlist(p.id)) this.store.toggleWishlist(p.id);
    this.store.removeFromBag(p.id);
  }

  async reserve(): Promise<void> {
    const name = this.name.trim();
    const phone = this.phone.replace(/[^\d+]/g, '');
    const branch = this.branch;
    if (!name) { this.error.set('Please enter your name.'); return; }
    if (phone.replace(/\D/g, '').length < 7) { this.error.set('Please enter a valid mobile number.'); return; }
    if (!branch) { this.error.set('Please choose a store.'); return; }
    if (!this.date || this.date < this.today) { this.error.set('Please choose a visit date from today on.'); return; }
    this.error.set(null);
    if (await this.store.reserve({ name, phone, storeId: branch, date: this.date, requestId: this.requestId, items: this.items().map(p => p.id) })) { this.requestId = crypto.randomUUID(); await this.router.navigate(this.catalog.link('reserved'), { queryParams: this.catalog.linkParams() }); }
  }

  whatsapp(): string { return this.catalog.enquiry(this.items()) ?? ''; }
}
