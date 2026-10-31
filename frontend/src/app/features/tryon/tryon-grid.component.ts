// Role: The customer page (/tryon). Shows the active try-on jewelry as a grid, filtered by
// type. Tapping an item opens the camera try-on (/tryon/:id). It sits outside the ERP shell
// so customers never see the ERP menu or the chatbot (assumed), but it uses the same theme
// colors, card and button styles. Images load with crossorigin so the browser cache holds the
// CORS copy the camera canvas needs (a plain cached copy would block the snapshot and overlay).

import { Component, computed, inject, signal } from '@angular/core';
import { RouterLink } from '@angular/router';
import { ApiError } from '../../core/http/error.interceptor';
import { JEWELRY_TYPES, JewelryItem, JewelryType } from './jewelry.models';
import { JewelryService } from './jewelry.service';

@Component({
  selector: 'nb-tryon-grid', standalone: true, imports: [RouterLink],
  template: `
    <header class="brand-bar"><span class="brand-name">NB <small>JEWEL</small></span><span class="title">Virtual Try-On</span></header>
    <main class="page">
      <p class="intro">Pick a piece, allow the camera, and see it on you. The video stays on your phone.</p>
      <div class="chips" role="tablist" aria-label="Filter by type">
        <button type="button" role="tab" [class.on]="filter() === 'all'" [attr.aria-selected]="filter() === 'all'" (click)="filter.set('all')">All</button>
        @for (t of types; track t.value) {
          <button type="button" role="tab" [class.on]="filter() === t.value" [attr.aria-selected]="filter() === t.value" (click)="filter.set(t.value)">{{ t.plural }}</button>
        }
      </div>
      @if (error()) {
        <div class="card message">{{ error() }} <button type="button" class="link" (click)="load()">Retry</button></div>
      } @else if (loading()) {
        <div class="grid" aria-busy="true">@for (n of [1, 2, 3, 4]; track n) { <div class="tile skeleton"></div> }</div>
      } @else if (!shown().length) {
        <div class="card message">No jewelry to try on yet{{ filter() === 'all' ? '' : ' in this type' }}.</div>
      } @else {
        <div class="grid">
          @for (item of shown(); track item.id) {
            <a class="tile" [routerLink]="['/tryon', item.id]" [attr.aria-label]="'Try on ' + item.name">
              <div class="pic"><img [src]="jewelry.imageSrc(item)" [alt]="item.name" loading="lazy" crossorigin="anonymous" /></div>
              <b>{{ item.name }}</b>
              <span class="type">{{ typeLabel(item.type) }}</span>
              <span class="try">Try on ›</span>
            </a>
          }
        </div>
      }
    </main>
  `,
  styles: [`
    :host { display:block; min-height:100vh; background:var(--page); }
    .brand-bar { height:58px; display:flex; align-items:center; gap:14px; padding:0 16px; color:white; background:var(--sidebar); }
    .brand-name { font-size:24px; font-weight:800; line-height:.7; } .brand-name small { display:block; font-size:7px; letter-spacing:3px; margin-left:3px; }
    .title { font-weight:700; }
    .page { max-width:960px; margin:0 auto; padding:16px; }
    .intro { margin:0 0 12px; color:var(--muted); font-size:13px; }
    .chips { display:flex; gap:8px; flex-wrap:wrap; margin-bottom:14px; }
    .chips button { min-height:36px; padding:0 16px; border:1px solid var(--gold); border-radius:18px; background:white; color:var(--gold); font-weight:700; }
    .chips button.on { background:var(--button); border-color:var(--button); color:#333; }
    .card { background:white; padding:16px; border-radius:8px; }
    .message { padding:40px 16px; text-align:center; color:var(--muted); }
    .link { border:0; background:transparent; padding:0; color:var(--gold); font-weight:700; text-decoration:underline; }
    .grid { display:grid; grid-template-columns:repeat(auto-fill, minmax(150px, 1fr)); gap:12px; }
    .tile { display:flex; flex-direction:column; gap:4px; padding:10px; border-radius:8px; background:white; border:1px solid var(--line); color:var(--ink); text-decoration:none; min-height:220px; }
    .tile:hover, .tile:focus-visible { border-color:var(--gold); outline:none; }
    .pic { height:140px; display:grid; place-items:center; border-radius:6px; background:var(--page); margin-bottom:6px; }
    .pic img { max-width:90%; max-height:120px; }
    .type { color:var(--muted); font-size:12px; }
    .try { margin-top:auto; color:var(--gold); font-weight:700; font-size:13px; }
    .skeleton { border:0; background:linear-gradient(90deg, #efe9d6 25%, #f8f3e4 50%, #efe9d6 75%); background-size:200% 100%; animation:shimmer 1.2s infinite; }
    @keyframes shimmer { from { background-position:200% 0; } to { background-position:-200% 0; } }
    @media (max-width:400px) { .grid { grid-template-columns:1fr 1fr; gap:8px; } .pic { height:110px; } }
  `]
})
export class TryonGridComponent {
  readonly jewelry = inject(JewelryService);
  readonly types = JEWELRY_TYPES;
  readonly items = signal<JewelryItem[]>([]);
  readonly loading = signal(true);
  readonly error = signal<string | null>(null);
  readonly filter = signal<JewelryType | 'all'>('all');
  readonly shown = computed(() => this.filter() === 'all' ? this.items() : this.items().filter(x => x.type === this.filter()));

  constructor() { this.load(); }

  typeLabel(type: JewelryType): string { return this.types.find(t => t.value === type)?.label ?? type; }

  load(): void {
    this.loading.set(true);
    this.error.set(null);
    this.jewelry.listActive().subscribe({
      next: items => { this.items.set(items); this.loading.set(false); },
      error: (error: unknown) => { this.loading.set(false); this.error.set(error instanceof ApiError ? error.message : 'Could not load the jewelry.'); }
    });
  }
}
