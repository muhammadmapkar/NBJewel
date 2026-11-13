// Role: The customer catalogue (/shop): quick type chips, a Filter panel (type, purity, price,
// weight, stone, occasion), sorting, the result count and the product grid. A collection chosen on
// the home page arrives as ?collection=; ?sort= picks the sort.

import { Component, computed, inject, signal } from '@angular/core';
import { takeUntilDestroyed } from '@angular/core/rxjs-interop';
import { ActivatedRoute, RouterLink } from '@angular/router';
import { Category, EMPTY_FILTERS, Filters, PRICE_RANGES, SORT_OPTIONS, SortKey, TYPE_OPTIONS, WEIGHT_RANGES, activeFilterCount, filterValues, matchesFilters, sortProducts } from '../catalog/catalog.models';
import { CatalogService } from '../catalog/catalog.service';
import { IconComponent } from '../ui/icon.component';
import { ProductCardComponent } from '../ui/product-card.component';
import { SiteHeaderComponent } from '../ui/site-header.component';

type Group = Exclude<keyof Filters, 'collections'>;

@Component({
  selector: 'ct-shop', standalone: true, imports: [RouterLink, IconComponent, ProductCardComponent, SiteHeaderComponent],
  template: `
    <ct-site-header />
    <main class="page">
      <h1 class="serif">{{ filters().collections.length ? filters().collections[0] + ' collection' : 'All jewellery' }}</h1>

      <nav class="quick" aria-label="Type">
        <button type="button" class="chip" [class.on]="!filters().types.length" (click)="setTypes([])">All</button>
        @for (t of types; track t.value) {
          <button type="button" class="chip" [class.on]="filters().types.length === 1 && filters().types[0] === t.value" (click)="setTypes([t.value])">{{ t.label }}</button>
        }
      </nav>

      <div class="toolbar">
        <button type="button" class="tool" (click)="panelOpen.set(true)">
          <ct-icon name="filter" /> Filter @if (count()) { <span class="n">{{ count() }}</span> }
        </button>
        <label class="tool sort">Sort
          <select [value]="sort()" (change)="sort.set($any($event.target).value)">
            @for (s of sorts; track s.value) { <option [value]="s.value">{{ s.label }}</option> }
          </select>
        </label>
      </div>

      @if (filters().collections.length) {
        <div class="active"><button type="button" class="chip on" (click)="clearCollection()">{{ filters().collections[0] }} ✕</button></div>
      }

      @switch (catalog.state().status) {
        @case ('loading') { <p class="count-line">Loading...</p> }
        @case ('error') { <p class="empty">The collection could not load. <button type="button" class="link" (click)="catalog.load(true)">Try again</button></p> }
        @default {
          <p class="count-line">{{ shown().length }} {{ shown().length === 1 ? 'design' : 'designs' }}</p>
          @if (!shown().length) {
            <p class="empty">No designs match these filters. <button type="button" class="link" (click)="clearAll()">Clear filters</button></p>
          }
          <div class="grid">@for (p of shown(); track p.id) { <ct-product-card [product]="p" /> }</div>
        }
      }
      <footer class="foot">Prices use a sample gold rate and include VAT. All data is sample data.</footer>
    </main>

    @if (panelOpen()) {
      <div class="scrim" (click)="panelOpen.set(false)"></div>
      <aside class="panel" role="dialog" aria-modal="true" aria-label="Filters">
        <header><b class="serif">Filters</b><button type="button" class="x" (click)="panelOpen.set(false)" aria-label="Close filters"><ct-icon name="close" /></button></header>
        <div class="groups">
          @for (g of groups(); track g.key) {
            <fieldset>
              <legend>{{ g.label }}</legend>
              <div class="options">
                @for (o of g.options; track o.value) {
                  <button type="button" class="chip" [class.on]="isOn(g.key, o.value)" [attr.aria-pressed]="isOn(g.key, o.value)" (click)="toggle(g.key, o.value)">{{ o.label }}</button>
                }
              </div>
            </fieldset>
          }
        </div>
        <footer>
          <button type="button" class="btn ghost" (click)="clearAll()">Clear all</button>
          <button type="button" class="btn" (click)="panelOpen.set(false)">Show {{ shown().length }}</button>
        </footer>
      </aside>
    }
  `,
  styles: [`
    :host { display: block; min-height: 100vh; }
    .page { max-width: 1080px; margin: 0 auto; padding: 16px 16px 32px; }
    h1 { margin: 4px 0 12px; font-size: 32px; }
    .quick { display: flex; gap: 8px; overflow-x: auto; scrollbar-width: none; padding-bottom: 2px; }
    .quick::-webkit-scrollbar { display: none; }
    .toolbar { display: flex; gap: 10px; margin: 14px 0 6px; }
    .tool { flex: 1; display: inline-flex; align-items: center; justify-content: center; gap: 8px; min-height: 44px; border: 1px solid var(--line); border-radius: 12px; background: var(--card); color: var(--ink); font-weight: 500; }
    .tool ct-icon { width: 18px; height: 18px; }
    .n { min-width: 20px; height: 20px; border-radius: 10px; background: var(--maroon); color: #fff; font-size: 12px; display: grid; place-items: center; }
    .sort select { border: 0; background: transparent; font: inherit; color: var(--maroon); font-weight: 600; max-width: 150px; }
    .active { margin: 6px 0; }
    .count-line { margin: 8px 0 12px; color: var(--muted); font-size: 13px; }
    .grid { display: grid; grid-template-columns: repeat(auto-fill, minmax(160px, 1fr)); gap: 14px; }
    .empty { padding: 30px 0; text-align: center; color: var(--muted); }
    .link { border: 0; background: none; color: var(--maroon); font-weight: 600; text-decoration: underline; padding: 0; }
    .foot { margin-top: 28px; text-align: center; color: var(--muted); font-size: 12px; }
    .scrim { position: fixed; inset: 0; z-index: 40; background: rgba(20, 10, 8, 0.45); }
    .panel { position: fixed; z-index: 41; left: 0; right: 0; bottom: 0; max-height: 86vh; display: flex; flex-direction: column; border-radius: 22px 22px 0 0; background: var(--ivory); box-shadow: 0 -10px 40px rgba(0,0,0,0.2); animation: up 0.2s ease-out; }
    @keyframes up { from { transform: translateY(30px); opacity: 0; } to { transform: none; opacity: 1; } }
    .panel > header { display: flex; align-items: center; justify-content: space-between; padding: 16px 18px 8px; font-size: 24px; }
    .x { width: 40px; height: 40px; display: grid; place-items: center; border: 0; border-radius: 50%; background: var(--cream); color: var(--ink); }
    .groups { overflow-y: auto; padding: 0 18px 8px; }
    fieldset { border: 0; padding: 10px 0; margin: 0; border-bottom: 1px solid var(--line); }
    legend { padding: 0 0 8px; font-weight: 600; font-size: 14px; }
    .options { display: flex; flex-wrap: wrap; gap: 8px; }
    .panel > footer { display: grid; grid-template-columns: 1fr 1.4fr; gap: 10px; padding: 12px 18px calc(14px + env(safe-area-inset-bottom)); border-top: 1px solid var(--line); }
    @media (min-width: 760px) { .panel { left: auto; top: 0; width: 400px; max-height: none; border-radius: 0; } }
  `]
})
export class ShopPage {
  readonly catalog = inject(CatalogService);
  private readonly route = inject(ActivatedRoute);
  readonly types = TYPE_OPTIONS;
  readonly sorts = SORT_OPTIONS;
  readonly filters = signal<Filters>(EMPTY_FILTERS);
  readonly sort = signal<SortKey>('popular');
  readonly panelOpen = signal(false);
  readonly count = computed(() => activeFilterCount({ ...this.filters(), collections: [] }));

  readonly shown = computed(() => {
    const f = this.filters();
    const total = (id: string) => { const p = this.catalog.find(id); return p ? this.catalog.price(p).total : 0; };
    const list = this.catalog.products().filter(p => matchesFilters(p, f, total(p.id)));
    return sortProducts(list, this.sort(), p => total(p.id));
  });

  readonly groups = computed(() => {
    const v = filterValues(this.catalog.products());
    const opts = (xs: readonly string[]) => xs.map(x => ({ value: x, label: x }));
    return [
      { key: 'types' as Group, label: 'Type', options: this.types.map(t => ({ value: t.value as string, label: t.label })) },
      { key: 'purities' as Group, label: 'Gold purity', options: opts(v.purities) },
      { key: 'prices' as Group, label: `Price (${this.catalog.catalog()?.currency ?? 'AED'})`, options: opts(PRICE_RANGES.map(r => r.label)) },
      { key: 'weights' as Group, label: 'Weight', options: opts(WEIGHT_RANGES.map(r => r.label)) },
      { key: 'stones' as Group, label: 'Stone', options: opts(v.stones) },
      { key: 'occasions' as Group, label: 'Occasion', options: opts(v.occasions) }
    ];
  });

  constructor() {
    this.catalog.load();
    this.route.queryParamMap.pipe(takeUntilDestroyed()).subscribe(q => {
      const collection = q.get('collection');
      this.filters.update(f => ({ ...f, collections: collection ? [collection] : [] }));
      const sort = q.get('sort') as SortKey | null;
      if (sort && this.sorts.some(s => s.value === sort)) this.sort.set(sort);
    });
  }

  isOn(group: Group, value: string): boolean { return (this.filters()[group] as string[]).includes(value); }

  toggle(group: Group, value: string): void {
    this.filters.update(f => {
      const list = f[group] as string[];
      return { ...f, [group]: list.includes(value) ? list.filter(x => x !== value) : [...list, value] };
    });
  }

  setTypes(types: Category[]): void { this.filters.update(f => ({ ...f, types })); }
  clearCollection(): void { this.filters.update(f => ({ ...f, collections: [] })); }
  clearAll(): void { this.filters.set(EMPTY_FILTERS); }
}
