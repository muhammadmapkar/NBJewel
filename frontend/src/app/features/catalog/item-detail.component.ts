// Role: The item detail screen at /catalog/:id. It shows every item field, the
// price breakdown, stock by store, and the Edit and Change Status actions.

import { DecimalPipe } from '@angular/common';
import { Component, computed, inject, signal } from '@angular/core';
import { takeUntilDestroyed } from '@angular/core/rxjs-interop';
import { ActivatedRoute, RouterLink } from '@angular/router';
import { catchError, of } from 'rxjs';
import { ItemDataService } from '../../core/data/item-data.service';
import { ApiError } from '../../core/http/error.interceptor';
import { Category, Item, PriceBreakdown, StockLevel, Store } from '../../core/models';
import { ToastComponent } from '../../shared/ui/toast.component';
import { errorMessage, makingTypeLabel } from './item-options';
import { Loadable } from './loadable';
import { StatusDialogComponent } from './status-dialog.component';
import { StatusPillComponent } from './status-pill.component';

type ItemState = 'loading' | 'ready' | 'notfound' | 'error';

@Component({
  selector: 'nb-item-detail', standalone: true,
  imports: [DecimalPipe, RouterLink, StatusPillComponent, StatusDialogComponent, ToastComponent],
  template: `
    <nb-toast [message]="toast()" (dismissed)="toast.set(null)" />

    @switch (state()) {
      @case ('loading') {
        <div class="card" aria-busy="true" aria-label="Loading item">
          @for (n of skeletonRows; track n) { <div class="skeleton line"></div> }
        </div>
      }
      @case ('notfound') {
        <div class="card message">Item not found. <a routerLink="/catalog">Back to Item Catalog</a></div>
      }
      @case ('error') {
        <div class="card message">{{ loadError() }} <button type="button" class="link" (click)="load()">Retry</button></div>
      }
      @default {
        @if (item(); as it) {
          <div class="page-head">
            <div>
              <h1>{{ it.name }}</h1>
              <p>Inventory　›　Item Catalog　›　{{ it.code }}</p>
            </div>
            <div class="actions">
              <a routerLink="/catalog">☷ List</a>
              <a [routerLink]="['/catalog', it.id, 'edit']">✎ Edit</a>
              <button type="button" (click)="openStatus()">⇄ Change Status</button>
            </div>
          </div>

          <div class="columns">
            <section class="card">
              <h2>Item details</h2>
              <dl>
                <div><dt>Code</dt><dd>{{ it.code }}</dd></div>
                <div><dt>Name</dt><dd>{{ it.name }}</dd></div>
                <div><dt>Category</dt><dd>{{ categoryName(it.categoryId) }}</dd></div>
                <div><dt>Design code</dt><dd>{{ it.designCode }}</dd></div>
                <div><dt>Metal</dt><dd>{{ it.metalType }}</dd></div>
                <div><dt>Purity</dt><dd>{{ it.purity }}</dd></div>
                <div><dt>Gross weight</dt><dd>{{ it.grossWt | number:'1.3-3' }} g</dd></div>
                <div><dt>Net weight</dt><dd>{{ it.netWt | number:'1.3-3' }} g</dd></div>
                <div><dt>Stone weight</dt><dd>{{ it.stoneWt | number:'1.3-3' }} g</dd></div>
                <div><dt>Making</dt><dd>{{ it.makingValue | number:'1.2-2' }} ({{ makingLabel(it.makingType) }})</dd></div>
                <div><dt>HUID</dt><dd>{{ it.huid || 'None' }}</dd></div>
                <div><dt>Status</dt><dd><nb-status-pill [status]="it.status" /></dd></div>
                <div><dt>Image</dt><dd>{{ it.imageUrl || 'No image' }}</dd></div>
              </dl>
            </section>

            <div class="side">
              <section class="card">
                <h2>Price breakdown</h2>
                @switch (price().status) {
                  @case ('loading') { <div class="skeleton line"></div><div class="skeleton line"></div><div class="skeleton line"></div> }
                  @case ('error') { <div class="message small">{{ priceError() }} <button type="button" class="link" (click)="loadPrice(it.id)">Retry</button></div> }
                  @default {
                    @if (priceValue(); as p) {
                      <dl class="rows">
                        <div><dt>Net weight</dt><dd>{{ p.netWeight | number:'1.3-3' }} g</dd></div>
                        <div><dt>Rate per gram</dt><dd>{{ p.ratePerGram | number:'1.2-2' }} {{ p.currency }}</dd></div>
                        <div><dt>Metal value</dt><dd>{{ p.metalValue | number:'1.2-2' }} {{ p.currency }}</dd></div>
                        <div><dt>Making charge</dt><dd>{{ p.makingCharge | number:'1.2-2' }} {{ p.currency }}</dd></div>
                        <div class="total"><dt>Total</dt><dd>{{ p.totalPrice | number:'1.2-2' }} {{ p.currency }}</dd></div>
                      </dl>
                    }
                  }
                }
              </section>

              <section class="card">
                <h2>Stock by store</h2>
                @switch (stock().status) {
                  @case ('loading') { <div class="skeleton line"></div><div class="skeleton line"></div> }
                  @case ('error') { <div class="message small">{{ stockError() }} <button type="button" class="link" (click)="loadStock(it.id)">Retry</button></div> }
                  @default {
                    @if (stockRows().length === 0) {
                      <div class="message small">No stock records for this item.</div>
                    } @else {
                      <div class="table-scroll">
                        <table>
                          <thead><tr><th>Store</th><th class="num">On hand</th><th class="num">Reserved</th><th class="num">Available</th></tr></thead>
                          <tbody>
                            @for (r of stockRows(); track r.storeId) {
                              <tr>
                                <td>{{ storeName(r.storeId) }}</td>
                                <td class="num">{{ r.onHand }}</td>
                                <td class="num">{{ r.reserved }}</td>
                                <td class="num">{{ r.onHand - r.reserved }}</td>
                              </tr>
                            }
                            <tr class="total">
                              <td>Total</td><td class="num">{{ totalOnHand() }}</td><td class="num">{{ totalReserved() }}</td><td class="num">{{ totalOnHand() - totalReserved() }}</td>
                            </tr>
                          </tbody>
                        </table>
                      </div>
                    }
                  }
                }
              </section>
            </div>
          </div>

          @if (dialogOpen()) {
            <nb-status-dialog [current]="it.status" [saving]="statusSaving()" [error]="statusError()"
              (confirm)="changeStatus(it.id, $event)" (cancel)="dialogOpen.set(false)" />
          }
        }
      }
    }
  `,
  styles: [`
    .page-head { display:flex; justify-content:space-between; align-items:flex-start; gap:12px; flex-wrap:wrap; margin-bottom:12px; }
    h1 { margin:0 0 5px; font-size:25px; }
    p { margin:0; color:var(--muted); font-size:12px; }
    .actions { display:flex; gap:25px; }
    .actions a, .actions button { border:0; background:transparent; font-weight:700; color:#333; text-decoration:none; padding:0; }
    .columns { display:grid; grid-template-columns:1fr 1fr; gap:14px; align-items:start; }
    .side { display:grid; gap:14px; }
    .card { background:white; padding:16px; border-radius:8px; }
    h2 { margin:-16px -16px 14px; padding:11px 16px; font-size:14px; color:var(--gold); background:var(--widget); border-radius:8px 8px 0 0; }
    dl { margin:0; display:grid; gap:10px; }
    dl > div { display:flex; justify-content:space-between; align-items:center; gap:12px; }
    dt { color:#6f6d68; font-size:12px; font-weight:700; }
    dd { margin:0; text-align:right; word-break:break-word; }
    .rows .total, tr.total { font-weight:700; border-top:1px solid var(--line); }
    .rows .total { padding-top:10px; }
    .table-scroll { overflow-x:auto; }
    table { width:100%; border-collapse:collapse; }
    th { padding:8px 10px; text-align:left; font-size:12px; color:#6f6d68; border-bottom:1px solid var(--line); white-space:nowrap; }
    td { padding:8px 10px; border-top:1px solid var(--line); white-space:nowrap; }
    .num { text-align:right; } th.num { text-align:right; }
    .message { text-align:center; color:var(--muted); padding:30px 12px; }
    .message.small { padding:16px 0; }
    .message a, .link { color:var(--gold); font-weight:700; }
    .link { border:0; background:transparent; padding:0; text-decoration:underline; }
    .skeleton { display:block; border-radius:4px; background:linear-gradient(90deg, #efe9d6 25%, #f8f3e4 50%, #efe9d6 75%); background-size:200% 100%; animation:shimmer 1.2s infinite; }
    .skeleton.line { height:20px; margin-bottom:12px; }
    @keyframes shimmer { from { background-position:200% 0; } to { background-position:-200% 0; } }
    @media (max-width:900px) { .columns { grid-template-columns:1fr; } .actions { gap:18px; flex-wrap:wrap; } }
  `]
})
export class ItemDetailComponent {
  private readonly data = inject(ItemDataService);
  private readonly route = inject(ActivatedRoute);
  private id = 0;

  readonly skeletonRows = [1, 2, 3, 4, 5, 6];
  readonly state = signal<ItemState>('loading');
  readonly loadError = signal('');
  readonly item = signal<Item | null>(null);
  readonly price = signal<Loadable<PriceBreakdown>>({ status: 'loading' });
  readonly stock = signal<Loadable<StockLevel[]>>({ status: 'loading' });
  readonly categories = signal<Category[]>([]);
  readonly stores = signal<Store[]>([]);
  readonly toast = signal<string | null>(null);
  readonly dialogOpen = signal(false);
  readonly statusSaving = signal(false);
  readonly statusError = signal<string | null>(null);

  readonly priceValue = computed(() => { const p = this.price(); return p.status === 'ready' ? p.value : null; });
  readonly priceError = computed(() => { const p = this.price(); return p.status === 'error' ? p.message : ''; });
  readonly stockRows = computed(() => { const s = this.stock(); return s.status === 'ready' ? s.value : []; });
  readonly stockError = computed(() => { const s = this.stock(); return s.status === 'error' ? s.message : ''; });
  readonly totalOnHand = computed(() => this.stockRows().reduce((sum, r) => sum + r.onHand, 0));
  readonly totalReserved = computed(() => this.stockRows().reduce((sum, r) => sum + r.reserved, 0));

  constructor() {
    this.showToastFromNavigation();
    this.data.getCategories().pipe(catchError(() => of<Category[]>([]))).subscribe(c => this.categories.set(c));
    this.data.getStores().pipe(catchError(() => of<Store[]>([]))).subscribe(s => this.stores.set(s));
    this.route.paramMap.pipe(takeUntilDestroyed()).subscribe(params => {
      const id = Number(params.get('id'));
      this.id = id;
      this.load();
    });
  }

  load(): void {
    if (!Number.isInteger(this.id) || this.id < 1) { this.state.set('notfound'); return; }
    const id = this.id;
    this.state.set('loading');
    this.data.getItem(id).subscribe({
      next: item => {
        if (id !== this.id) return;
        this.item.set(item);
        this.state.set('ready');
        this.loadPrice(id);
        this.loadStock(id);
      },
      error: (error: unknown) => {
        if (id !== this.id) return;
        if (error instanceof ApiError && error.status === 404) { this.state.set('notfound'); return; }
        this.loadError.set(`Could not load this item. ${errorMessage(error)}`);
        this.state.set('error');
      }
    });
  }

  loadPrice(id: number): void {
    this.price.set({ status: 'loading' });
    this.data.getPrice(id).subscribe({
      next: value => this.price.set({ status: 'ready', value }),
      error: (error: unknown) => this.price.set({ status: 'error', message: errorMessage(error) })
    });
  }

  loadStock(id: number): void {
    this.stock.set({ status: 'loading' });
    this.data.getStock(id).subscribe({
      next: value => this.stock.set({ status: 'ready', value }),
      error: (error: unknown) => this.stock.set({ status: 'error', message: errorMessage(error) })
    });
  }

  openStatus(): void {
    this.statusError.set(null);
    this.dialogOpen.set(true);
  }

  changeStatus(id: number, status: string): void {
    this.statusSaving.set(true);
    this.statusError.set(null);
    this.data.updateItemStatus(id, status).subscribe({
      next: item => {
        this.item.set(item);
        this.statusSaving.set(false);
        this.dialogOpen.set(false);
        this.toast.set(`Status changed to ${item.status}.`);
      },
      error: (error: unknown) => {
        this.statusSaving.set(false);
        this.statusError.set(errorMessage(error));
      }
    });
  }

  categoryName(id: number): string { return this.categories().find(c => c.id === id)?.name ?? `Category ${id}`; }
  storeName(id: number): string { return this.stores().find(s => s.id === id)?.name ?? `Store ${id}`; }
  makingLabel(type: string): string { return makingTypeLabel(type); }

  // The form screen passes a success message through the navigation state.
  private showToastFromNavigation(): void {
    const state = history.state as { toast?: string } | null;
    if (state?.toast) {
      this.toast.set(state.toast);
      history.replaceState({ ...state, toast: undefined }, '');
    }
  }
}
