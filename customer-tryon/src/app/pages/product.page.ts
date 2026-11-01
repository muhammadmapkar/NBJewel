// Role: One design: a large picture, what is in the set, and try-on buttons (the full set, or
// one piece of it on its own).

import { Component, computed, inject } from '@angular/core';
import { toSignal } from '@angular/core/rxjs-interop';
import { ActivatedRoute, RouterLink } from '@angular/router';
import { map } from 'rxjs';
import { KIND_LABELS, PieceKind } from '../catalog/catalog.models';
import { CatalogService } from '../catalog/catalog.service';

@Component({
  selector: 'ct-product', standalone: true, imports: [RouterLink],
  template: `
    <header class="bar">
      <a class="back" routerLink="/" aria-label="Back to all designs">‹</a>
      <span class="brand">NB Jewel</span>
      <span class="spacer"></span>
    </header>

    @switch (catalog.state().status) {
      @case ('loading') { <div class="stage skeleton"></div> }
      @case ('error') { <p class="empty">This design could not load. <button type="button" class="link" (click)="catalog.load(true)">Try again</button></p> }
      @default {
        @if (product(); as p) {
          <div class="stage"><img [src]="catalog.url(p.card)" [alt]="p.name" /></div>
          <section class="sheet">
            <h1 class="serif">{{ p.name }}</h1>
            <p class="muted">{{ p.note }}</p>
            <ul class="pieces">
              @for (k of p.kinds; track k) { <li>{{ label(k) }}</li> }
            </ul>
            <div class="actions">
              <a class="btn" [routerLink]="['/try', p.id]">{{ p.kinds.length > 1 ? 'Try the full set' : 'Try it on' }}</a>
              @if (p.kinds.length > 1) {
                <div class="singles">
                  @for (k of p.kinds; track k) {
                    <a class="btn ghost" [routerLink]="['/try', p.id]" [queryParams]="{ parts: k }">{{ label(k) }} only</a>
                  }
                </div>
              }
            </div>
            <p class="note">Your camera opens on this phone only. Nothing is recorded or uploaded.</p>
          </section>
        } @else {
          <p class="empty">This design is not available. <a class="link" routerLink="/">See all designs</a></p>
        }
      }
    }
  `,
  styles: [`
    :host { display: block; min-height: 100vh; background: var(--ivory); }
    .bar { position: sticky; top: 0; z-index: 2; display: flex; align-items: center; gap: 12px; height: calc(56px + env(safe-area-inset-top)); padding: env(safe-area-inset-top) 12px 0; background: rgba(250, 246, 239, 0.92); backdrop-filter: blur(8px); }
    .back { width: 40px; height: 40px; display: grid; place-items: center; border-radius: 50%; color: var(--ink); text-decoration: none; font-size: 30px; line-height: 1; }
    .brand { font-size: 14px; color: var(--maroon); }
    .spacer { flex: 1; }
    .stage { display: grid; place-items: center; height: min(52vh, 460px); padding: 20px; background: radial-gradient(circle at 50% 42%, #fff 0%, var(--cream) 72%); }
    .stage img { max-height: 100%; object-fit: contain; filter: drop-shadow(0 12px 18px rgba(80, 50, 10, 0.2)); }
    .sheet { max-width: 640px; margin: -18px auto 0; position: relative; padding: 22px 20px calc(28px + env(safe-area-inset-bottom)); border-radius: 22px 22px 0 0; background: var(--ivory); }
    h1 { margin: 0 0 4px; font-size: 32px; line-height: 1.1; }
    .muted { margin: 0; }
    .pieces { display: flex; flex-wrap: wrap; gap: 8px; list-style: none; padding: 0; margin: 16px 0 22px; }
    .pieces li { padding: 6px 12px; border-radius: 999px; background: var(--cream); color: var(--maroon); font-size: 13px; font-weight: 500; }
    .actions { display: grid; gap: 10px; }
    .actions > .btn { width: 100%; }
    .singles { display: grid; grid-template-columns: repeat(auto-fit, minmax(130px, 1fr)); gap: 10px; }
    .singles .btn { min-height: 44px; padding: 0 12px; font-size: 14px; }
    .note { margin: 18px 0 0; color: var(--muted); font-size: 12px; text-align: center; }
    .empty { padding: 60px 20px; text-align: center; color: var(--muted); }
    .link { border: 0; background: none; color: var(--maroon); font-weight: 600; text-decoration: underline; padding: 0; }
    .skeleton { background: linear-gradient(90deg, #efe6d6 25%, #f7f1e6 50%, #efe6d6 75%); background-size: 200% 100%; animation: shimmer 1.2s infinite; }
    @keyframes shimmer { from { background-position: 200% 0; } to { background-position: -200% 0; } }
  `]
})
export class ProductPage {
  readonly catalog = inject(CatalogService);
  private readonly id = toSignal(inject(ActivatedRoute).paramMap.pipe(map(p => p.get('id') ?? '')), { initialValue: '' });
  readonly product = computed(() => this.catalog.products().find(p => p.id === this.id()));

  constructor() { this.catalog.load(); }

  label(kind: PieceKind): string { return KIND_LABELS[kind]; }
}
