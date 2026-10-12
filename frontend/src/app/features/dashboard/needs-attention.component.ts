// Role: Dashboard widget that lists items still With Smith and stock rows with
// fewer than 3 units available (the threshold of 3 is assumed). It only reads
// data through ItemDataService, and each row opens the item page. This is live
// data, unlike the mock dashboard widgets.

import { Component, inject, signal } from '@angular/core';
import { RouterLink } from '@angular/router';
import { forkJoin } from 'rxjs';
import { WidgetCardComponent } from '../../shared/ui/widget-card.component';
import { ItemDataService } from '../../core/data/item-data.service';
import { Item, StockLevel, Store } from '../../core/models';

const LOW_STOCK_THRESHOLD = 3;
const SHOWN = 6;

@Component({
  selector: 'nb-needs-attention', standalone: true, imports: [RouterLink, WidgetCardComponent],
  template: `
    <nb-widget-card title="Needs attention">
      @if (error()) { <p class="note">{{ error() }}</p> }
      @else if (loading()) { <p class="note">Loading...</p> }
      @else {
        <div class="cols">
          <section>
            <h3>With Smith <span>{{ withSmith().length }}</span></h3>
            @for (item of withSmithShown(); track item.id) { <a [routerLink]="['/catalog', item.id]"><b>{{ item.code }}</b> {{ item.name }}</a> }
            @empty { <p class="note">No items are with Smith.</p> }
          </section>
          <section>
            <h3>Low stock, under {{ threshold }} available <span>{{ low().length }}</span></h3>
            @for (row of lowShown(); track $index) { <a [routerLink]="['/catalog', row.itemId]"><b>{{ nameOf(row.itemId) }}</b> {{ storeOf(row.storeId) }}<em>{{ row.onHand - row.reserved }} left</em></a> }
            @empty { <p class="note">No low stock.</p> }
          </section>
        </div>
      }
    </nb-widget-card>
  `,
  styles: [`
    .cols { display:grid; grid-template-columns:repeat(2, minmax(0, 1fr)); gap:18px; } h3 { margin:0 0 8px; font-size:14px; color:var(--gold); } h3 span { margin-left:6px; padding:1px 8px; border-radius:10px; background:var(--pale); font-size:12px; }
    a { display:flex; gap:8px; align-items:baseline; padding:7px 4px; border-bottom:1px solid var(--line); color:var(--ink); text-decoration:none; font-size:13px; } a:hover { background:var(--pale); } a b { white-space:nowrap; } a em { margin-left:auto; font-style:normal; color:var(--muted); white-space:nowrap; }
    .note { margin:0; color:var(--muted); font-size:13px; }
    @media (max-width: 900px) { .cols { grid-template-columns:1fr; } }
  `]
})
export class NeedsAttentionComponent {
  private readonly data = inject(ItemDataService);
  readonly threshold = LOW_STOCK_THRESHOLD;
  readonly loading = signal(true);
  readonly error = signal('');
  readonly withSmith = signal<Item[]>([]);
  readonly low = signal<StockLevel[]>([]);
  private items: Item[] = [];
  private stores: Store[] = [];

  constructor() {
    forkJoin({
      withSmith: this.data.searchItems({ status: 'With Smith', pageSize: 100 }),
      all: this.data.searchItems({ pageSize: 100 }),
      low: this.data.getLowStock(LOW_STOCK_THRESHOLD),
      stores: this.data.getStores()
    }).subscribe({
      next: r => {
        this.items = r.all.items; this.stores = r.stores;
        this.withSmith.set(r.withSmith.items); this.low.set(r.low); this.loading.set(false);
      },
      error: () => { this.error.set('Could not load stock and status data.'); this.loading.set(false); }
    });
  }

  withSmithShown(): Item[] { return this.withSmith().slice(0, SHOWN); }
  lowShown(): StockLevel[] { return this.low().slice(0, SHOWN); }
  nameOf(itemId: number): string { return this.items.find(x => x.id === itemId)?.code ?? `Item ${itemId}`; }
  storeOf(storeId: number): string { return this.stores.find(x => x.id === storeId)?.name ?? `Store ${storeId}`; }
}
