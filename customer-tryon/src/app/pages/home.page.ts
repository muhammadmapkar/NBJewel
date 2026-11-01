// Role: The customer catalog (home). A short hero, filter chips (All, Sets, Necklaces, Earrings)
// and a grid of designs. Tapping a design opens its page; "Try on" goes straight to the camera.

import { Component, computed, inject, signal } from '@angular/core';
import { RouterLink } from '@angular/router';
import { Category, FILTERS, KIND_LABELS, Product, matchesFilter } from '../catalog/catalog.models';
import { CatalogService } from '../catalog/catalog.service';

@Component({
  selector: 'ct-home', standalone: true, imports: [RouterLink],
  template: `
    <header class="hero">
      <span class="brand logo">NB Jewel</span>
      <h1 class="serif">See it on you<br />before you visit</h1>
      <p>Pick a design, allow the camera, and wear it live. The video stays on your phone.</p>
    </header>

    <main class="page">
      <nav class="filters" aria-label="Filter designs">
        @for (f of filters; track f.value) {
          <button type="button" class="chip" [class.on]="filter() === f.value" [attr.aria-pressed]="filter() === f.value" (click)="filter.set(f.value)">{{ f.label }}</button>
        }
      </nav>

      @switch (catalog.state().status) {
        @case ('loading') {
          <div class="grid" aria-busy="true">@for (n of [1, 2, 3, 4]; track n) { <div class="card skeleton"></div> }</div>
        }
        @case ('error') {
          <p class="empty">The designs could not load. <button type="button" class="link" (click)="catalog.load(true)">Try again</button></p>
        }
        @default {
          @if (!shown().length) { <p class="empty">No designs here yet.</p> }
          <div class="grid">
            @for (p of shown(); track p.id) {
              <article class="card">
                <a class="pic" [routerLink]="['/p', p.id]" [attr.aria-label]="p.name">
                  <img [src]="catalog.url(p.card)" [alt]="p.name" loading="lazy" />
                  @if (p.category === 'set') { <span class="tag">Set</span> }
                </a>
                <div class="info">
                  <a class="name serif" [routerLink]="['/p', p.id]">{{ p.name }}</a>
                  <span class="kinds">{{ kindsText(p) }}</span>
                  <a class="btn try" [routerLink]="['/try', p.id]">Try on</a>
                </div>
              </article>
            }
          </div>
        }
      }
      <footer class="foot">Demo designs for a try-on preview. Sizes on screen are approximate.</footer>
    </main>
  `,
  styles: [`
    :host { display: block; min-height: 100vh; }
    .hero { padding: calc(28px + env(safe-area-inset-top)) 20px 26px; background: radial-gradient(120% 90% at 80% 0%, #7d2a36 0%, var(--maroon-deep) 55%, #2a0d12 100%); color: var(--ivory); }
    .logo { font-size: 15px; color: var(--gold-soft); }
    h1 { margin: 18px 0 10px; font-size: 38px; line-height: 1.05; font-weight: 600; }
    .hero p { margin: 0; max-width: 360px; color: #e9dccb; font-size: 14px; line-height: 1.5; }
    .page { max-width: 1040px; margin: 0 auto; padding: 18px 16px 32px; }
    .filters { display: flex; gap: 8px; overflow-x: auto; padding-bottom: 4px; margin-bottom: 16px; scrollbar-width: none; }
    .filters::-webkit-scrollbar { display: none; }
    .grid { display: grid; grid-template-columns: repeat(auto-fill, minmax(160px, 1fr)); gap: 14px; }
    .card { display: flex; flex-direction: column; border-radius: var(--radius); background: var(--card); box-shadow: var(--shadow); overflow: hidden; }
    .pic { position: relative; aspect-ratio: 1; display: grid; place-items: center; padding: 14px; background: radial-gradient(circle at 50% 40%, #fff 0%, var(--cream) 75%); }
    .pic img { max-height: 100%; object-fit: contain; filter: drop-shadow(0 6px 10px rgba(80, 50, 10, 0.18)); }
    .tag { position: absolute; top: 10px; left: 10px; padding: 3px 9px; border-radius: 999px; background: var(--ink); color: var(--gold-soft); font-size: 11px; font-weight: 600; letter-spacing: 0.06em; text-transform: uppercase; }
    .info { display: flex; flex-direction: column; gap: 4px; padding: 12px 12px 14px; flex: 1; }
    .name { color: var(--ink); text-decoration: none; font-size: 19px; font-weight: 600; line-height: 1.15; }
    .kinds { color: var(--muted); font-size: 12px; }
    .try { margin-top: auto; min-height: 40px; padding: 0 16px; font-size: 14px; align-self: stretch; margin-top: 10px; }
    .skeleton { aspect-ratio: 0.72; background: linear-gradient(90deg, #efe6d6 25%, #f7f1e6 50%, #efe6d6 75%); background-size: 200% 100%; animation: shimmer 1.2s infinite; box-shadow: none; }
    @keyframes shimmer { from { background-position: 200% 0; } to { background-position: -200% 0; } }
    .empty { padding: 40px 0; text-align: center; color: var(--muted); }
    .link { border: 0; background: none; color: var(--maroon); font-weight: 600; text-decoration: underline; padding: 0; }
    .foot { margin-top: 28px; text-align: center; color: var(--muted); font-size: 12px; }
    @media (min-width: 720px) { h1 { font-size: 52px; } .hero { padding: 48px 40px 40px; } .grid { grid-template-columns: repeat(auto-fill, minmax(220px, 1fr)); gap: 20px; } }
  `]
})
export class HomePage {
  readonly catalog = inject(CatalogService);
  readonly filters = FILTERS;
  readonly filter = signal<'all' | Category>('all');
  readonly shown = computed(() => this.catalog.products().filter(p => matchesFilter(p, this.filter())));

  constructor() { this.catalog.load(); }

  kindsText(p: Product): string { return p.kinds.map(k => KIND_LABELS[k]).join(' · '); }
}
