// Role: The Stock tab. Shows on hand, reserved and available stock for one
// item in each store, using the real stock and store endpoints.

import { DatePipe } from '@angular/common';
import { Component, computed, effect, inject, input, signal, untracked } from '@angular/core';
import { catchError, of } from 'rxjs';
import { ItemDataService } from '../../core/data/item-data.service';
import { StockLevel, Store } from '../../core/models';
import { errorMessage } from './item-options';
import { Loadable } from './loadable';

@Component({
  selector: 'nb-stock-panel', standalone: true, imports: [DatePipe],
  template: `
    <section class="card">
      @switch (stock().status) {
        @case ('loading') { <div aria-busy="true" aria-label="Loading stock"><div class="skeleton line"></div><div class="skeleton line"></div><div class="skeleton line"></div></div> }
        @case ('error') { <div class="message">{{ errorText() }} <button type="button" class="link" (click)="load()">Retry</button></div> }
        @default {
          @if (rows().length === 0) {
            <div class="message">No stock in any store.</div>
          } @else {
            <div class="table-scroll">
              <table>
                <thead><tr><th>Store</th><th class="num">On hand</th><th class="num">Reserved</th><th class="num">Available</th><th>Last updated</th></tr></thead>
                <tbody>
                  @for (r of rows(); track r.storeId) {
                    <tr>
                      <td>{{ storeName(r.storeId) }}</td>
                      <td class="num">{{ r.onHand }}</td>
                      <td class="num">{{ r.reserved }}</td>
                      <td class="num">{{ r.onHand - r.reserved }}</td>
                      <td>@if (r.lastUpdated) { {{ r.lastUpdated | date:'mediumDate' }} } @else { <span class="muted">Not available</span> }</td>
                    </tr>
                  }
                  <tr class="total">
                    <td>Total</td><td class="num">{{ totalOnHand() }}</td><td class="num">{{ totalReserved() }}</td><td class="num">{{ totalOnHand() - totalReserved() }}</td><td></td>
                  </tr>
                </tbody>
              </table>
            </div>
          }
        }
      }
    </section>
  `,
  styles: [`
    .card { background:white; padding:16px; border-radius:8px; }
    .table-scroll { overflow-x:auto; }
    table { width:100%; min-width:520px; border-collapse:collapse; }
    th { padding:10px 12px; text-align:left; font-size:12px; color:var(--gold); background:var(--widget); white-space:nowrap; }
    td { padding:10px 12px; border-top:1px solid var(--line); white-space:nowrap; }
    .num { text-align:right; } th.num { text-align:right; }
    tr.total { font-weight:700; }
    .muted { color:var(--muted); }
    .message { text-align:center; color:var(--muted); padding:30px 12px; }
    .link { border:0; background:transparent; padding:0; color:var(--gold); font-weight:700; text-decoration:underline; }
    .skeleton { display:block; height:20px; margin-bottom:12px; border-radius:4px; background:linear-gradient(90deg, #efe9d6 25%, #f8f3e4 50%, #efe9d6 75%); background-size:200% 100%; animation:shimmer 1.2s infinite; }
    @keyframes shimmer { from { background-position:200% 0; } to { background-position:-200% 0; } }
  `]
})
export class StockPanelComponent {
  private readonly data = inject(ItemDataService);

  readonly itemId = input.required<number>();
  readonly stock = signal<Loadable<StockLevel[]>>({ status: 'loading' });
  readonly stores = signal<Store[]>([]);

  readonly rows = computed(() => { const s = this.stock(); return s.status === 'ready' ? s.value : []; });
  readonly errorText = computed(() => { const s = this.stock(); return s.status === 'error' ? s.message : ''; });
  readonly totalOnHand = computed(() => this.rows().reduce((sum, r) => sum + r.onHand, 0));
  readonly totalReserved = computed(() => this.rows().reduce((sum, r) => sum + r.reserved, 0));

  constructor() {
    this.data.getStores().pipe(catchError(() => of<Store[]>([]))).subscribe(s => this.stores.set(s));
    effect(() => { this.itemId(); untracked(() => this.load()); });
  }

  load(): void {
    const id = this.itemId();
    this.stock.set({ status: 'loading' });
    this.data.getStock(id).subscribe({
      next: value => { if (id === this.itemId()) this.stock.set({ status: 'ready', value }); },
      error: (error: unknown) => { if (id === this.itemId()) this.stock.set({ status: 'error', message: errorMessage(error) }); }
    });
  }

  storeName(id: number): string { return this.stores().find(s => s.id === id)?.name ?? `Store ${id}`; }
}
