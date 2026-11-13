// Role: The shop home page: a hero with "Shop" and "Try on" calls to action, collection tiles,
// bestsellers, new arrivals, and how the camera try-on works.

import { Component, computed, inject } from '@angular/core';
import { RouterLink } from '@angular/router';
import { CatalogService } from '../catalog/catalog.service';
import { ProductCardComponent } from '../ui/product-card.component';
import { SiteHeaderComponent } from '../ui/site-header.component';

@Component({
  selector: 'ct-home', standalone: true, imports: [RouterLink, ProductCardComponent, SiteHeaderComponent],
  template: `
    <ct-site-header [dark]="true" />
    <section class="hero">
      <p class="eyebrow">Virtual try-on</p>
      <h1 class="serif">See it on you<br />before you visit</h1>
      <p class="lead">Browse our gold collections and wear any design live with your phone camera. It moves with you, the video stays on your phone.</p>
      <div class="cta">
        <a class="btn gold" routerLink="/shop">Shop the collection</a>
        @if (featured(); as f) { <a class="btn ghost light" [routerLink]="['/try', f.id]">Try on now</a> }
      </div>
    </section>

    <main class="page">
      @switch (catalog.state().status) {
        @case ('loading') { <div class="grid">@for (n of [1, 2]; track n) { <div class="skeleton"></div> }</div> }
        @case ('error') { <p class="empty">The collection could not load. <button type="button" class="link" (click)="catalog.load(true)">Try again</button></p> }
        @default {
          <section>
            <h2 class="serif">Collections</h2>
            <div class="collections">
              @for (c of collections(); track c.name) {
                <a class="tile" routerLink="/shop" [queryParams]="{ collection: c.name }">
                  <img [src]="catalog.url(c.image)" alt="" loading="lazy" />
                  <span class="serif">{{ c.name }}</span>
                </a>
              }
            </div>
          </section>

          <section>
            <div class="head"><h2 class="serif">Bestsellers</h2><a class="more" routerLink="/shop" [queryParams]="{ sort: 'popular' }">See all</a></div>
            <div class="row">@for (p of bestsellers(); track p.id) { <ct-product-card [product]="p" /> }</div>
          </section>

          <section>
            <div class="head"><h2 class="serif">New arrivals</h2><a class="more" routerLink="/shop" [queryParams]="{ sort: 'newest' }">See all</a></div>
            <div class="row">@for (p of newest(); track p.id) { <ct-product-card [product]="p" /> }</div>
          </section>

          <section class="how">
            <h2 class="serif">How try-on works</h2>
            <ol>
              <li><b>Pick a design</b><span>Any necklace, earrings or full set.</span></li>
              <li><b>Allow the camera</b><span>Face the camera with your ears and neck in view.</span></li>
              <li><b>Move and compare</b><span>Pieces turn and swing with you. Switch designs live, then save a photo.</span></li>
            </ol>
          </section>
        }
      }
      <footer class="foot">Demo shop. Designs, prices, specifications and gold rates are sample data.</footer>
    </main>
  `,
  styles: [`
    :host { display: block; min-height: 100vh; }
    .hero { padding: 26px 20px 30px; background: radial-gradient(120% 90% at 80% 0%, #7d2a36 0%, var(--maroon-deep) 55%, #2a0d12 100%); color: var(--ivory); }
    .eyebrow { margin: 0; color: var(--gold-soft); font-size: 12px; letter-spacing: 0.16em; text-transform: uppercase; font-weight: 600; }
    h1 { margin: 12px 0 10px; font-size: 40px; line-height: 1.04; font-weight: 600; }
    .lead { margin: 0; max-width: 420px; color: #e9dccb; font-size: 14px; line-height: 1.55; }
    .cta { display: flex; flex-wrap: wrap; gap: 10px; margin-top: 20px; }
    .btn.light { color: var(--ivory); border-color: rgba(250, 246, 239, 0.7); }
    .page { max-width: 1080px; margin: 0 auto; padding: 8px 16px 32px; }
    section { margin-top: 26px; }
    h2 { margin: 0 0 12px; font-size: 26px; }
    .head { display: flex; align-items: baseline; justify-content: space-between; }
    .more { color: var(--maroon); font-weight: 600; font-size: 14px; text-decoration: none; }
    .collections { display: grid; grid-template-columns: repeat(auto-fill, minmax(150px, 1fr)); gap: 12px; }
    .tile { position: relative; display: block; aspect-ratio: 4 / 3; border-radius: var(--radius); overflow: hidden; background: radial-gradient(circle at 50% 35%, #fff 0%, var(--cream) 70%); text-decoration: none; color: var(--ink); box-shadow: var(--shadow); }
    .tile img { position: absolute; inset: 8px 8px 30px; width: calc(100% - 16px); height: calc(100% - 38px); object-fit: contain; }
    .tile span { position: absolute; left: 0; right: 0; bottom: 0; padding: 6px 12px 8px; font-size: 19px; font-weight: 600; background: linear-gradient(transparent, rgba(255, 253, 249, 0.95) 40%); }
    .row { display: grid; grid-auto-flow: column; grid-auto-columns: minmax(170px, 46%); gap: 12px; overflow-x: auto; padding-bottom: 8px; scroll-snap-type: x mandatory; scrollbar-width: none; }
    .row::-webkit-scrollbar { display: none; }
    .row > * { scroll-snap-align: start; }
    .how ol { list-style: none; counter-reset: step; display: grid; gap: 10px; padding: 0; margin: 0; }
    .how li { counter-increment: step; display: grid; grid-template-columns: 34px 1fr; gap: 2px 12px; padding: 14px; border-radius: var(--radius); background: var(--card); box-shadow: var(--shadow); }
    .how li::before { content: counter(step); grid-row: span 2; width: 30px; height: 30px; border-radius: 50%; display: grid; place-items: center; background: var(--maroon); color: #fff; font-weight: 600; }
    .how span { color: var(--muted); font-size: 13px; }
    .grid { display: grid; grid-template-columns: 1fr 1fr; gap: 12px; margin-top: 24px; }
    .skeleton { aspect-ratio: 0.7; border-radius: var(--radius); background: linear-gradient(90deg, #efe6d6 25%, #f7f1e6 50%, #efe6d6 75%); background-size: 200% 100%; animation: shimmer 1.2s infinite; }
    @keyframes shimmer { from { background-position: 200% 0; } to { background-position: -200% 0; } }
    .empty { padding: 40px 0; text-align: center; color: var(--muted); }
    .link { border: 0; background: none; color: var(--maroon); font-weight: 600; text-decoration: underline; padding: 0; }
    .foot { margin-top: 30px; text-align: center; color: var(--muted); font-size: 12px; }
    @media (min-width: 760px) { .hero { padding: 56px 48px 52px; } h1 { font-size: 58px; } .row { grid-auto-columns: minmax(220px, 24%); } }
  `]
})
export class HomePage {
  readonly catalog = inject(CatalogService);
  readonly bestsellers = computed(() => [...this.catalog.products()].sort((a, b) => a.popularity - b.popularity).slice(0, 6));
  readonly newest = computed(() => [...this.catalog.products()].sort((a, b) => b.addedOn.localeCompare(a.addedOn)).slice(0, 6));
  readonly featured = computed(() => this.bestsellers()[0]);
  // One tile per collection, pictured by its most popular design.
  readonly collections = computed(() => {
    const byName = new Map<string, string>();
    for (const p of [...this.catalog.products()].sort((a, b) => a.popularity - b.popularity)) if (!byName.has(p.collection)) byName.set(p.collection, p.card);
    return [...byName].map(([name, image]) => ({ name, image }));
  });

  constructor() { this.catalog.load(); }
}
