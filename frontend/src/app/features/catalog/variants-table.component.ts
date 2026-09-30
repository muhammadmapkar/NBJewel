// Role: The Variants tab. Lists the sizes and colors of one item.
// Assumed, no backend endpoint yet: variants come from sample data.

import { DecimalPipe } from '@angular/common';
import { Component, computed, effect, inject, input, signal, untracked } from '@angular/core';
import { ItemDataService } from '../../core/data/item-data.service';
import { ItemVariant, VARIANT_CURRENCY } from '../../core/models';
import { errorMessage } from './item-options';
import { Loadable } from './loadable';

@Component({
  selector: 'nb-variants-table', standalone: true, imports: [DecimalPipe],
  template: `
    <section class="card">
      @switch (variants().status) {
        @case ('loading') { <div aria-busy="true" aria-label="Loading variants"><div class="skeleton line"></div><div class="skeleton line"></div><div class="skeleton line"></div></div> }
        @case ('error') { <div class="message">{{ errorText() }} <button type="button" class="link" (click)="load()">Retry</button></div> }
        @default {
          @if (rows().length === 0) {
            <div class="message">No variants. This item is single.</div>
          } @else {
            <div class="table-scroll">
              <table>
                <thead><tr><th>Size</th><th>Color</th><th>Variant code</th><th class="num">Price delta ({{ currency }})</th><th class="num">Stock</th></tr></thead>
                <tbody>
                  @for (v of rows(); track v.id) {
                    <tr>
                      <td>{{ v.size }}</td>
                      <td>{{ v.color }}</td>
                      <td class="code">{{ v.variantCode }}</td>
                      <td class="num">{{ v.priceDelta > 0 ? '+' : '' }}{{ v.priceDelta | number:'1.2-2' }}</td>
                      <td class="num">{{ v.stock }}</td>
                    </tr>
                  }
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
    .code { font-weight:700; }
    .message { text-align:center; color:var(--muted); padding:30px 12px; }
    .link { border:0; background:transparent; padding:0; color:var(--gold); font-weight:700; text-decoration:underline; }
    .skeleton { display:block; height:20px; margin-bottom:12px; border-radius:4px; background:linear-gradient(90deg, #efe9d6 25%, #f8f3e4 50%, #efe9d6 75%); background-size:200% 100%; animation:shimmer 1.2s infinite; }
    @keyframes shimmer { from { background-position:200% 0; } to { background-position:-200% 0; } }
  `]
})
export class VariantsTableComponent {
  private readonly data = inject(ItemDataService);

  readonly itemId = input.required<number>();
  readonly currency = VARIANT_CURRENCY;
  readonly variants = signal<Loadable<ItemVariant[]>>({ status: 'loading' });

  readonly rows = computed(() => { const v = this.variants(); return v.status === 'ready' ? v.value : []; });
  readonly errorText = computed(() => { const v = this.variants(); return v.status === 'error' ? v.message : ''; });

  constructor() {
    effect(() => { this.itemId(); untracked(() => this.load()); });
  }

  load(): void {
    const id = this.itemId();
    this.variants.set({ status: 'loading' });
    this.data.getVariants(id).subscribe({
      next: value => { if (id === this.itemId()) this.variants.set({ status: 'ready', value }); },
      error: (error: unknown) => { if (id === this.itemId()) this.variants.set({ status: 'error', message: errorMessage(error) }); }
    });
  }
}
