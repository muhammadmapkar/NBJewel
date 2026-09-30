// Role: The Item Catalog screen. It loads items through ItemDataService, then
// filters, sorts and pages them in the browser. Price and stock are fetched
// only for the ten rows currently on screen.
//
// Why in the browser: the API can filter by search, category, metal, purity and
// status, but it cannot sort, filter by weight or stock, or return totals.
// That is fine for the sample data (30 items). A real ERP would move this to the server.

import { DecimalPipe } from '@angular/common';
import { Component, computed, effect, inject, OnInit, signal, untracked } from '@angular/core';
import { Router } from '@angular/router';
import { catchError, EMPTY, expand, forkJoin, map, of, reduce } from 'rxjs';
import { ItemDataService } from '../../core/data/item-data.service';
import { Category, Item, MetalRate, PriceBreakdown, StockLevel } from '../../core/models';
import { CatalogFilters, DEFAULT_CATALOG_FILTERS } from './catalog-filters.model';
import { CatalogFiltersComponent } from './filters.component';
import { StatusPillComponent } from './status-pill.component';

type LoadState = 'loading' | 'ready' | 'error';
type Loadable<T> = { status: 'loading' } | { status: 'ready'; value: T } | { status: 'error' };
type SortKey = 'code' | 'name' | 'category' | 'metal' | 'purity' | 'netWt' | 'status';
interface SortState { key: SortKey; dir: 'asc' | 'desc'; }
interface Column { id: string; label: string; sortKey?: SortKey; }

const PAGE_SIZE = 10;
const FETCH_PAGE_SIZE = 100;

@Component({
  selector: 'nb-catalog-list', standalone: true,
  imports: [DecimalPipe, CatalogFiltersComponent, StatusPillComponent],
  template: `
    <div class="page-head">
      <div>
        <h1>Item Catalog</h1>
        <p>Inventory　›　Item Catalog</p>
      </div>
    </div>

    <div class="card">
      <nb-catalog-filters
        [applied]="filters()" [categories]="filterCategories()" [metals]="metals()" [purities]="purities()"
        (apply)="applyFilters($event)" (reset)="resetFilters()" />
    </div>

    <div class="card table-card">
      <div class="toolbar">
        <input class="search" type="search" placeholder="Search by code or name" aria-label="Search items"
          [value]="search()" (input)="onSearch($event)">
        @if (state() === 'ready') { <span class="count">{{ filteredItems().length }} items</span> }
      </div>

      @switch (state()) {
        @case ('loading') {
          <div class="skeleton-list" aria-busy="true" aria-label="Loading items">
            @for (n of skeletonRows; track n) { <div class="skeleton line"></div> }
          </div>
        }
        @case ('error') {
          <div class="message">Could not load items. <button type="button" class="link" (click)="load()">Retry</button></div>
        }
        @default {
          @if (filteredItems().length === 0) {
            <div class="message">No items match. <button type="button" class="link" (click)="clearAll()">Clear filters.</button></div>
          } @else {
            <div class="table-scroll">
              <table>
                <thead>
                  <tr>
                    @for (col of columns; track col.id) {
                      <th [attr.aria-sort]="ariaSort(col)">
                        @if (col.sortKey) {
                          <button type="button" class="sort" (click)="toggleSort(col.sortKey)">{{ col.label }}<span class="arrow">{{ arrow(col) }}</span></button>
                        } @else { {{ col.label }} }
                      </th>
                    }
                  </tr>
                </thead>
                <tbody>
                  @for (item of pageRows(); track item.id) {
                    <tr tabindex="0" (click)="open(item)" (keydown.enter)="open(item)">
                      <td><div class="thumb" [title]="item.imageUrl ?? 'No image'">◇</div></td>
                      <td class="code">{{ item.code }}</td>
                      <td>{{ item.name }}</td>
                      <td>{{ categoryName(item.categoryId) }}</td>
                      <td>{{ item.metalType }}</td>
                      <td>{{ item.purity }}</td>
                      <td class="num">{{ item.netWt | number:'1.3-3' }} g</td>
                      <td class="num">
                        @if (price(item.id); as p) {
                          @switch (p.status) {
                            @case ('ready') { {{ priceValue(p)?.totalPrice | number:'1.2-2' }} {{ priceValue(p)?.currency }} }
                            @case ('error') { <span class="muted">n/a</span> }
                            @default { <span class="skeleton cell"></span> }
                          }
                        } @else { <span class="skeleton cell"></span> }
                      </td>
                      <td><nb-status-pill [status]="item.status" /></td>
                      <td class="num">
                        @if (stock(item.id); as s) {
                          @switch (s.status) {
                            @case ('ready') { {{ stockTotal(s) }} }
                            @case ('error') { <span class="muted">n/a</span> }
                            @default { <span class="skeleton cell short"></span> }
                          }
                        } @else { <span class="skeleton cell short"></span> }
                      </td>
                    </tr>
                  }
                </tbody>
              </table>
            </div>
            <div class="pager">
              <span>Showing {{ firstShown() }} to {{ lastShown() }} of {{ filteredItems().length }}</span>
              <div class="pager-buttons">
                <button type="button" [disabled]="page() === 1" (click)="goTo(page() - 1)">Previous</button>
                <span>Page {{ page() }} of {{ totalPages() }}</span>
                <button type="button" [disabled]="page() === totalPages()" (click)="goTo(page() + 1)">Next</button>
              </div>
            </div>
          }
        }
      }
    </div>
  `,
  styles: [`
    .page-head { margin-bottom:12px; }
    h1 { margin:0 0 5px; font-size:25px; }
    p { margin:0; color:var(--muted); font-size:12px; }
    .card { background:white; padding:16px; border-radius:8px; margin-bottom:14px; }
    .table-card { padding:0; overflow:hidden; }
    .toolbar { display:flex; align-items:center; gap:14px; padding:14px 16px; border-bottom:1px solid var(--line); }
    .search { flex:1; max-width:340px; min-height:38px; padding:8px 10px; border:1px solid #cfd1d5; border-radius:6px; outline:none; }
    .search:focus { border-color:var(--button); }
    .count { color:var(--muted); font-size:12px; }
    .table-scroll { overflow-x:auto; }
    table { width:100%; min-width:1000px; border-collapse:collapse; }
    th { padding:10px 12px; text-align:left; font-size:12px; color:var(--gold); background:var(--widget); white-space:nowrap; }
    td { padding:10px 12px; border-top:1px solid var(--line); white-space:nowrap; }
    tbody tr { cursor:pointer; }
    tbody tr:hover, tbody tr:focus { background:var(--pale); outline:none; }
    .sort { border:0; background:transparent; padding:0; color:inherit; font-weight:700; }
    .arrow { display:inline-block; width:14px; margin-left:4px; }
    .code { font-weight:700; }
    .num { text-align:right; } th:nth-child(7), th:nth-child(8), th:nth-child(10) { text-align:right; }
    .thumb { width:38px; height:38px; display:grid; place-items:center; border-radius:6px; background:var(--page); border:1px solid var(--line); color:var(--gold); font-size:18px; }
    .muted { color:var(--muted); }
    .message { padding:40px 16px; text-align:center; color:var(--muted); }
    .link { border:0; background:transparent; padding:0; color:var(--gold); font-weight:700; text-decoration:underline; }
    .skeleton { display:inline-block; border-radius:4px; background:linear-gradient(90deg, #efe9d6 25%, #f8f3e4 50%, #efe9d6 75%); background-size:200% 100%; animation:shimmer 1.2s infinite; }
    .skeleton.cell { width:70px; height:14px; } .skeleton.cell.short { width:30px; }
    .skeleton-list { padding:16px; display:grid; gap:12px; } .skeleton.line { display:block; height:22px; }
    @keyframes shimmer { from { background-position:200% 0; } to { background-position:-200% 0; } }
    .pager { display:flex; justify-content:space-between; align-items:center; gap:12px; flex-wrap:wrap; padding:12px 16px; border-top:1px solid var(--line); color:var(--muted); font-size:12px; }
    .pager-buttons { display:flex; align-items:center; gap:12px; }
    .pager-buttons button { min-height:34px; padding:0 14px; border:1px solid var(--gold); border-radius:6px; background:white; color:var(--gold); font-weight:700; }
    .pager-buttons button:disabled { opacity:.45; cursor:default; }
  `]
})
export class CatalogListComponent implements OnInit {
  private readonly data = inject(ItemDataService);
  private readonly router = inject(Router);
  private loadToken = 0;
  private searchTimer: ReturnType<typeof setTimeout> | undefined;

  readonly columns: Column[] = [
    { id: 'thumb', label: 'Image' },
    { id: 'code', label: 'Code', sortKey: 'code' },
    { id: 'name', label: 'Name', sortKey: 'name' },
    { id: 'category', label: 'Category', sortKey: 'category' },
    { id: 'metal', label: 'Metal', sortKey: 'metal' },
    { id: 'purity', label: 'Purity', sortKey: 'purity' },
    { id: 'netWt', label: 'Net weight', sortKey: 'netWt' },
    { id: 'price', label: 'Price' },
    { id: 'status', label: 'Status', sortKey: 'status' },
    { id: 'stock', label: 'Stock' }
  ];
  readonly skeletonRows = [1, 2, 3, 4, 5, 6];

  readonly state = signal<LoadState>('loading');
  readonly items = signal<Item[]>([]);
  readonly categories = signal<Category[]>([]);
  readonly rates = signal<MetalRate[]>([]);
  readonly filters = signal<CatalogFilters>({ ...DEFAULT_CATALOG_FILTERS });
  readonly search = signal('');
  readonly sort = signal<SortState | null>(null);
  readonly page = signal(1);
  private readonly prices = signal<Record<number, Loadable<PriceBreakdown>>>({});
  private readonly stocks = signal<Record<number, Loadable<StockLevel[]>>>({});

  // Only leaf categories are offered, because items belong to the child categories.
  readonly filterCategories = computed(() => this.categories().filter(c => !this.categories().some(o => o.parentId === c.id)));
  readonly metals = computed(() => [...new Set(this.rates().map(r => r.metalType))]);
  readonly purities = computed(() => [...new Set(this.rates().map(r => r.purity))]);

  readonly filteredItems = computed(() => {
    const f = this.filters();
    const stocks = this.stocks();
    let rows = this.items().filter(i =>
      (f.minWeight === null || i.netWt >= f.minWeight) &&
      (f.maxWeight === null || i.netWt <= f.maxWeight) &&
      (!f.inStockOnly || this.totalOnHand(stocks[i.id]) > 0));
    const sort = this.sort();
    if (sort) {
      const sign = sort.dir === 'asc' ? 1 : -1;
      rows = [...rows].sort((a, b) => {
        const x = this.sortValue(a, sort.key);
        const y = this.sortValue(b, sort.key);
        return (typeof x === 'number' && typeof y === 'number' ? x - y : String(x).localeCompare(String(y))) * sign;
      });
    }
    return rows;
  });
  readonly totalPages = computed(() => Math.max(1, Math.ceil(this.filteredItems().length / PAGE_SIZE)));
  readonly pageRows = computed(() => this.filteredItems().slice((this.page() - 1) * PAGE_SIZE, this.page() * PAGE_SIZE));
  readonly firstShown = computed(() => (this.filteredItems().length === 0 ? 0 : (this.page() - 1) * PAGE_SIZE + 1));
  readonly lastShown = computed(() => Math.min(this.page() * PAGE_SIZE, this.filteredItems().length));

  constructor() {
    // Fetch price and stock for the rows that are visible, and only those.
    effect(() => {
      const rows = this.pageRows();
      untracked(() => this.fetchRowData(rows));
    });
  }

  ngOnInit(): void {
    this.data.getCategories().pipe(catchError(() => of<Category[]>([]))).subscribe(c => this.categories.set(c));
    this.data.getLatestMetalRates().pipe(catchError(() => of<MetalRate[]>([]))).subscribe(r => this.rates.set(r));
    this.load();
  }

  load(): void {
    const token = ++this.loadToken;
    const f = this.filters();
    const base = {
      search: this.search().trim() || undefined,
      category: f.categoryId ?? undefined,
      metal: f.metal || undefined,
      purity: f.purity || undefined,
      status: f.status || undefined,
      pageSize: FETCH_PAGE_SIZE
    };
    this.state.set('loading');
    this.items.set([]);
    this.prices.set({});
    this.stocks.set({});

    // Read every page the API has for these filters (the API caps a page at 100).
    this.data.searchItems({ ...base, page: 1 }).pipe(
      expand(res => (res.page * res.pageSize < res.totalCount ? this.data.searchItems({ ...base, page: res.page + 1 }) : EMPTY)),
      reduce((all, res) => all.concat(res.items), [] as Item[])
    ).subscribe({
      next: items => {
        if (token !== this.loadToken) return;
        if (f.inStockOnly) { this.loadStockForAll(items, token); } else { this.finishLoad(items); }
      },
      error: () => { if (token === this.loadToken) this.state.set('error'); }
    });
  }

  applyFilters(next: CatalogFilters): void {
    this.filters.set(next);
    this.load();
  }

  resetFilters(): void { this.applyFilters({ ...DEFAULT_CATALOG_FILTERS }); }

  clearAll(): void {
    this.search.set('');
    this.resetFilters();
  }

  onSearch(event: Event): void {
    this.search.set((event.target as HTMLInputElement).value);
    clearTimeout(this.searchTimer);
    this.searchTimer = setTimeout(() => this.load(), 300);
  }

  toggleSort(key: SortKey): void {
    const current = this.sort();
    this.sort.set(current?.key === key ? { key, dir: current.dir === 'asc' ? 'desc' : 'asc' } : { key, dir: 'asc' });
    this.page.set(1);
  }

  ariaSort(col: Column): string | null {
    const s = this.sort();
    return s && col.sortKey === s.key ? (s.dir === 'asc' ? 'ascending' : 'descending') : null;
  }

  arrow(col: Column): string {
    const s = this.sort();
    return s && col.sortKey === s.key ? (s.dir === 'asc' ? '▲' : '▼') : '';
  }

  goTo(page: number): void { this.page.set(Math.min(Math.max(1, page), this.totalPages())); }

  open(item: Item): void { void this.router.navigate(['/catalog', item.id]); }

  categoryName(id: number): string { return this.categories().find(c => c.id === id)?.name ?? String(id); }

  price(id: number): Loadable<PriceBreakdown> | undefined { return this.prices()[id]; }
  stock(id: number): Loadable<StockLevel[]> | undefined { return this.stocks()[id]; }
  priceValue(p: Loadable<PriceBreakdown>): PriceBreakdown | null { return p.status === 'ready' ? p.value : null; }
  stockTotal(s: Loadable<StockLevel[]>): number { return this.totalOnHand(s); }

  private totalOnHand(s: Loadable<StockLevel[]> | undefined): number {
    return s?.status === 'ready' ? s.value.reduce((sum, r) => sum + r.onHand, 0) : 0;
  }

  private sortValue(item: Item, key: SortKey): string | number {
    switch (key) {
      case 'code': return item.code;
      case 'name': return item.name;
      case 'category': return this.categoryName(item.categoryId);
      case 'metal': return item.metalType;
      case 'purity': return item.purity;
      case 'netWt': return item.netWt;
      case 'status': return item.status;
    }
  }

  private finishLoad(items: Item[]): void {
    this.items.set(items);
    this.page.set(1);
    this.state.set('ready');
  }

  // "In stock only" needs stock for every candidate item, since the API has no bulk stock call.
  private loadStockForAll(items: Item[], token: number): void {
    if (items.length === 0) { this.finishLoad(items); return; }
    forkJoin(items.map(i => this.data.getStock(i.id).pipe(
      map((value): [number, Loadable<StockLevel[]>] => [i.id, { status: 'ready', value }]),
      catchError(() => of<[number, Loadable<StockLevel[]>]>([i.id, { status: 'error' }]))
    ))).subscribe(entries => {
      if (token !== this.loadToken) return;
      this.stocks.set(Object.fromEntries(entries));
      this.finishLoad(items);
    });
  }

  private fetchRowData(rows: Item[]): void {
    for (const item of rows) {
      if (!this.prices()[item.id]) {
        this.prices.update(p => ({ ...p, [item.id]: { status: 'loading' } }));
        this.data.getPrice(item.id).subscribe({
          next: value => this.prices.update(p => ({ ...p, [item.id]: { status: 'ready', value } })),
          error: () => this.prices.update(p => ({ ...p, [item.id]: { status: 'error' } }))
        });
      }
      if (!this.stocks()[item.id]) {
        this.stocks.update(s => ({ ...s, [item.id]: { status: 'loading' } }));
        this.data.getStock(item.id).subscribe({
          next: value => this.stocks.update(s => ({ ...s, [item.id]: { status: 'ready', value } })),
          error: () => this.stocks.update(s => ({ ...s, [item.id]: { status: 'error' } }))
        });
      }
    }
  }
}
