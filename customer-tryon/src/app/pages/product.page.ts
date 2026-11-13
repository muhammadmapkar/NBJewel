// Role: One design (/p/:id): swipeable pictures (the set and each piece) with tap to zoom, name,
// item code and price, Try on (full set or one piece), Add to bag, wishlist, Enquire on WhatsApp,
// specifications, the price breakdown from the sample gold rate, the description, store
// information, and similar designs. A sticky bar keeps the price and Try on in reach on phones.

import { Component, computed, inject, signal } from '@angular/core';
import { toSignal } from '@angular/core/rxjs-interop';
import { ActivatedRoute, RouterLink } from '@angular/router';
import { map } from 'rxjs';
import { KIND_LABELS, PieceKind, Product, similarProducts } from '../catalog/catalog.models';
import { CatalogService } from '../catalog/catalog.service';
import { StoreService } from '../catalog/store.service';
import { IconComponent } from '../ui/icon.component';
import { ProductCardComponent } from '../ui/product-card.component';
import { SiteHeaderComponent } from '../ui/site-header.component';

@Component({
  selector: 'ct-product', standalone: true, imports: [RouterLink, IconComponent, ProductCardComponent, SiteHeaderComponent],
  template: `
    <ct-site-header [back]="true" />
    @switch (catalog.state().status) {
      @case ('loading') { <div class="gallery skeleton"></div> }
      @case ('error') { <p class="empty">This design could not load. <button type="button" class="link" (click)="catalog.load(true)">Try again</button></p> }
      @default {
        @if (product(); as p) {
          <div class="layout">
            <section class="gallery-wrap">
              <div class="gallery" (scroll)="onScroll($event)">
                @for (img of images(); track img) {
                  <button type="button" class="slide" (click)="zoom.set(img)" aria-label="Zoom picture"><img [src]="catalog.url(img)" [alt]="p.name" /></button>
                }
              </div>
              @if (images().length > 1) {
                <div class="dots">@for (img of images(); track img; let i = $index) { <span [class.on]="i === slide()"></span> }</div>
              }
              @if (p.badge) { <span class="badge" [class.new]="p.badge === 'new'">{{ p.badge === 'new' ? 'New' : 'Bestseller' }}</span> }
              <button type="button" class="heart" [class.on]="store.inWishlist(p.id)" (click)="store.toggleWishlist(p.id)" [attr.aria-label]="store.inWishlist(p.id) ? 'Remove from wishlist' : 'Add to wishlist'">
                <ct-icon [name]="store.inWishlist(p.id) ? 'heartFull' : 'heart'" />
              </button>
            </section>

            <section class="details">
              <p class="collection">{{ p.collection }} collection</p>
              <h1 class="serif">{{ p.name }}</h1>
              <p class="code">Item code {{ p.code }}</p>
              <p class="price">{{ catalog.money(price().total) }} <span>incl. VAT</span></p>
              <p class="rate">Price based on the gold rate of {{ catalog.money(price().rate) }} per gram ({{ p.purity }}, sample)</p>

              <div class="actions">
                <a class="btn try" [routerLink]="['/try', p.id]"><ct-icon name="camera" /> {{ p.kinds.length > 1 ? 'Try the full set' : 'Try it on' }}</a>
                <button type="button" class="btn ghost" (click)="addToBag(p)">{{ store.inBag(p.id) ? 'In your bag' : 'Add to bag' }}</button>
              </div>
              @if (added()) { <p class="toast" role="status">Added to your bag. <a routerLink="/bag">View bag</a></p> }

              @if (p.kinds.length > 1) {
                <div class="pieces">
                  <b>In this set</b>
                  <div class="piece-row">
                    @for (k of p.kinds; track k) {
                      <a class="piece" [routerLink]="['/try', p.id]" [queryParams]="{ parts: k }">{{ label(k) }} <span>Try alone</span></a>
                    }
                  </div>
                </div>
              }

              <a class="enquire" [href]="whatsapp(p)" target="_blank" rel="noopener"><ct-icon name="chat" /> Enquire on WhatsApp</a>

              <h2 class="serif">Specifications</h2>
              <dl class="specs">
                <div><dt>Metal</dt><dd>{{ p.metal }}</dd></div>
                <div><dt>Purity</dt><dd>{{ p.purity }}</dd></div>
                <div><dt>Gross weight</dt><dd>{{ p.grossWeight }} g</dd></div>
                <div><dt>Net gold weight</dt><dd>{{ p.netWeight }} g</dd></div>
                <div><dt>Stones</dt><dd>{{ p.stones.length ? p.stones.join(', ') : 'None (plain gold)' }}</dd></div>
                <div><dt>Size</dt><dd>{{ p.size }}</dd></div>
                <div><dt>Hallmark</dt><dd>{{ p.hallmark }}</dd></div>
                <div><dt>Making charge</dt><dd>{{ p.makingPercent }}% of gold value</dd></div>
                <div><dt>Occasion</dt><dd>{{ p.occasions.join(', ') }}</dd></div>
              </dl>

              <h2 class="serif">Price breakdown</h2>
              <dl class="specs money">
                <div><dt>Gold ({{ p.netWeight }} g x {{ catalog.money(price().rate) }})</dt><dd>{{ catalog.money(price().gold) }}</dd></div>
                <div><dt>Making charge</dt><dd>{{ catalog.money(price().making) }}</dd></div>
                <div><dt>Stones</dt><dd>{{ catalog.money(price().stones) }}</dd></div>
                <div><dt>VAT ({{ catalog.catalog()?.vatPercent }}%)</dt><dd>{{ catalog.money(price().vat) }}</dd></div>
                <div class="total"><dt>Total</dt><dd>{{ catalog.money(price().total) }}</dd></div>
              </dl>

              <h2 class="serif">About this design</h2>
              <p class="desc">{{ p.description }}</p>
              <ul class="perks">
                <li>Reserve online, see it in store before you buy</li>
                <li>Certified purity with every piece</li>
                <li>Exchange on the gold value at our stores (sample policy)</li>
              </ul>
            </section>
          </div>

          @if (similar().length) {
            <section class="similar">
              <h2 class="serif">You may also like</h2>
              <div class="row">@for (s of similar(); track s.id) { <ct-product-card [product]="s" /> }</div>
            </section>
          }
          <p class="foot">Sample data: designs, prices, specifications and gold rate are for the demo.</p>

          <div class="sticky">
            <div class="sp"><b>{{ catalog.money(price().total) }}</b><span>incl. VAT</span></div>
            <a class="btn" [routerLink]="['/try', p.id]"><ct-icon name="camera" /> Try on</a>
            <button type="button" class="btn ghost" (click)="addToBag(p)" [attr.aria-label]="'Add ' + p.name + ' to bag'"><ct-icon name="bag" /></button>
          </div>

          @if (zoom(); as z) {
            <div class="zoom" role="dialog" aria-modal="true" aria-label="Zoomed picture">
              <button type="button" class="x" (click)="zoom.set(null)" aria-label="Close"><ct-icon name="close" /></button>
              <div class="zoom-area"><img [src]="catalog.url(z)" [alt]="p.name" /></div>
              <p>Pinch to zoom</p>
            </div>
          }
        } @else {
          <p class="empty">This design is not available. <a class="link" routerLink="/shop">See all designs</a></p>
        }
      }
    }
  `,
  styles: [`
    :host { display: block; min-height: 100vh; padding-bottom: calc(84px + env(safe-area-inset-bottom)); }
    .gallery-wrap { position: relative; }
    .gallery { display: flex; overflow-x: auto; scroll-snap-type: x mandatory; scrollbar-width: none; height: min(56vh, 480px); background: radial-gradient(circle at 50% 42%, #fff 0%, var(--cream) 72%); }
    .gallery::-webkit-scrollbar { display: none; }
    .slide { flex: 0 0 100%; scroll-snap-align: center; display: grid; place-items: center; padding: 26px; border: 0; background: transparent; }
    .slide img { max-height: 100%; object-fit: contain; filter: drop-shadow(0 12px 18px rgba(80, 50, 10, 0.2)); }
    .dots { position: absolute; bottom: 12px; left: 0; right: 0; display: flex; justify-content: center; gap: 6px; }
    .dots span { width: 7px; height: 7px; border-radius: 50%; background: rgba(43, 26, 20, 0.2); }
    .dots span.on { background: var(--maroon); }
    .badge { position: absolute; top: 14px; left: 14px; padding: 4px 10px; border-radius: 999px; background: var(--ink); color: var(--gold-soft); font-size: 11px; font-weight: 600; letter-spacing: 0.06em; text-transform: uppercase; }
    .badge.new { background: var(--gold); color: #fff; }
    .heart { position: absolute; top: 10px; right: 12px; width: 44px; height: 44px; display: grid; place-items: center; border: 0; border-radius: 50%; background: rgba(255,255,255,0.9); color: var(--maroon); box-shadow: var(--shadow); }
    .details { padding: 20px 18px 0; max-width: 640px; }
    .collection { margin: 0; color: var(--gold); font-size: 12px; font-weight: 600; letter-spacing: 0.12em; text-transform: uppercase; }
    h1 { margin: 6px 0 2px; font-size: 32px; line-height: 1.1; }
    .code { margin: 0; color: var(--muted); font-size: 12px; }
    .price { margin: 14px 0 2px; font-size: 26px; font-weight: 600; color: var(--maroon); }
    .price span { font-size: 13px; font-weight: 500; color: var(--muted); }
    .rate { margin: 0; color: var(--muted); font-size: 12px; }
    .actions { display: grid; grid-template-columns: 1.5fr 1fr; gap: 10px; margin: 18px 0 10px; }
    .actions ct-icon, .sticky ct-icon { width: 19px; height: 19px; }
    .toast { margin: 0 0 8px; padding: 10px 14px; border-radius: 12px; background: #e6f2e6; color: #245b2a; font-size: 13px; }
    .toast a { color: inherit; font-weight: 600; }
    .pieces { margin: 10px 0; padding: 14px; border-radius: var(--radius); background: var(--card); box-shadow: var(--shadow); }
    .pieces b { font-size: 14px; }
    .piece-row { display: flex; flex-wrap: wrap; gap: 8px; margin-top: 10px; }
    .piece { display: inline-flex; flex-direction: column; padding: 8px 14px; border-radius: 12px; background: var(--cream); color: var(--ink); text-decoration: none; font-weight: 600; font-size: 14px; }
    .piece span { color: var(--maroon); font-size: 12px; font-weight: 500; }
    .enquire { display: inline-flex; align-items: center; gap: 8px; margin: 6px 0 4px; color: #1f7a45; font-weight: 600; text-decoration: none; font-size: 14px; }
    .enquire ct-icon { width: 20px; height: 20px; }
    h2 { margin: 24px 0 10px; font-size: 24px; }
    .specs { margin: 0; border-radius: var(--radius); background: var(--card); box-shadow: var(--shadow); overflow: hidden; }
    .specs div { display: flex; justify-content: space-between; gap: 14px; padding: 11px 14px; border-bottom: 1px solid var(--line); font-size: 14px; }
    .specs div:last-child { border-bottom: 0; }
    .specs dt { color: var(--muted); }
    .specs dd { margin: 0; text-align: right; font-weight: 500; }
    .money .total { background: var(--cream); }
    .money .total dt, .money .total dd { color: var(--ink); font-weight: 700; }
    .desc { margin: 0; line-height: 1.6; }
    .perks { margin: 14px 0 0; padding-left: 18px; color: var(--muted); font-size: 13px; line-height: 1.8; }
    .similar { padding: 6px 18px 0; }
    .row { display: grid; grid-auto-flow: column; grid-auto-columns: minmax(170px, 46%); gap: 12px; overflow-x: auto; padding-bottom: 8px; scrollbar-width: none; }
    .row::-webkit-scrollbar { display: none; }
    .foot { margin: 22px 18px 0; text-align: center; color: var(--muted); font-size: 12px; }
    .sticky { position: fixed; left: 0; right: 0; bottom: 0; z-index: 15; display: grid; grid-template-columns: 1fr auto auto; gap: 8px; align-items: center; padding: 10px 14px calc(10px + env(safe-area-inset-bottom)); background: rgba(250, 246, 239, 0.96); backdrop-filter: blur(10px); border-top: 1px solid var(--line); }
    .sp { display: flex; flex-direction: column; }
    .sp b { color: var(--maroon); font-size: 17px; }
    .sp span { color: var(--muted); font-size: 11px; }
    .sticky .btn { min-height: 46px; }
    .sticky .ghost { width: 50px; padding: 0; }
    .zoom { position: fixed; inset: 0; z-index: 50; display: flex; flex-direction: column; background: #fff; }
    .zoom .x { position: absolute; top: calc(10px + env(safe-area-inset-top)); right: 12px; z-index: 2; width: 44px; height: 44px; display: grid; place-items: center; border: 0; border-radius: 50%; background: var(--cream); }
    .zoom-area { flex: 1; overflow: auto; display: grid; place-items: center; touch-action: pinch-zoom pan-x pan-y; }
    .zoom-area img { max-width: 100%; max-height: 100%; object-fit: contain; }
    .zoom p { margin: 0; padding: 10px 0 calc(14px + env(safe-area-inset-bottom)); text-align: center; color: var(--muted); font-size: 12px; }
    .empty { padding: 60px 20px; text-align: center; color: var(--muted); }
    .link { border: 0; background: none; color: var(--maroon); font-weight: 600; text-decoration: underline; padding: 0; }
    .skeleton { height: 50vh; background: linear-gradient(90deg, #efe6d6 25%, #f7f1e6 50%, #efe6d6 75%); background-size: 200% 100%; animation: shimmer 1.2s infinite; }
    @keyframes shimmer { from { background-position: 200% 0; } to { background-position: -200% 0; } }
    @media (min-width: 900px) {
      :host { padding-bottom: 0; }
      .layout { display: grid; grid-template-columns: 1.1fr 1fr; gap: 28px; max-width: 1160px; margin: 0 auto; padding: 24px; }
      .gallery { height: 560px; border-radius: 22px; }
      .details { padding: 0; }
      .sticky { display: none; }
      .similar { max-width: 1160px; margin: 0 auto; }
      .row { grid-auto-columns: minmax(220px, 24%); }
    }
  `]
})
export class ProductPage {
  readonly catalog = inject(CatalogService);
  readonly store = inject(StoreService);
  private readonly id = toSignal(inject(ActivatedRoute).paramMap.pipe(map(p => p.get('id') ?? '')), { initialValue: '' });
  readonly product = computed(() => this.catalog.find(this.id()));
  readonly price = computed(() => { const p = this.product(); return p ? this.catalog.price(p) : { gold: 0, making: 0, stones: 0, vat: 0, total: 0, rate: 0 }; });
  readonly similar = computed(() => { const p = this.product(); return p ? similarProducts(p, this.catalog.products()) : []; });
  // The whole design first, then each piece on its own.
  readonly images = computed(() => {
    const p = this.product();
    if (!p) return [];
    const parts = [p.parts.necklace?.image, p.parts.earringLeft?.image, p.parts.tikka?.image].filter((x): x is string => !!x);
    return [p.card, ...(p.kinds.length > 1 ? parts : [])];
  });
  readonly slide = signal(0);
  readonly zoom = signal<string | null>(null);
  readonly added = signal(false);

  constructor() { this.catalog.load(); }

  label(kind: PieceKind): string { return KIND_LABELS[kind]; }

  onScroll(event: Event): void {
    const el = event.target as HTMLElement;
    this.slide.set(Math.round(el.scrollLeft / Math.max(el.clientWidth, 1)));
  }

  addToBag(p: Product): void {
    this.store.addToBag(p.id);
    this.added.set(true);
  }

  // Opens WhatsApp with the design already written in; the customer picks the chat (assumed:
  // the shop's WhatsApp number is not known yet).
  whatsapp(p: Product): string {
    const text = `Hello NB Jewel, I would like to know more about ${p.name} (${p.code}), ${this.catalog.money(this.price().total)}.`;
    return `https://wa.me/?text=${encodeURIComponent(text)}`;
  }
}
