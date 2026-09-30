// Role: Renders the catalog filter panel. It keeps a draft copy of the filters
// and only tells the list when the user presses Apply or Reset.

import { Component, effect, input, output, signal } from '@angular/core';
import { Category } from '../../core/models';
import { CatalogFilters, DEFAULT_CATALOG_FILTERS, ITEM_STATUSES } from './catalog-filters.model';

@Component({
  selector: 'nb-catalog-filters', standalone: true,
  template: `
    <div class="filters">
      <label>Category
        <select (change)="setText('categoryId', $event)">
          <option value="" [selected]="draft().categoryId === null">All categories</option>
          @for (c of categories(); track c.id) {
            <option [value]="c.id" [selected]="draft().categoryId === c.id">{{ c.name }}</option>
          }
        </select>
      </label>
      <label>Metal
        <select (change)="setText('metal', $event)">
          <option value="" [selected]="draft().metal === ''">All metals</option>
          @for (m of metals(); track m) { <option [value]="m" [selected]="draft().metal === m">{{ m }}</option> }
        </select>
      </label>
      <label>Purity
        <select (change)="setText('purity', $event)">
          <option value="" [selected]="draft().purity === ''">All purities</option>
          @for (p of purities(); track p) { <option [value]="p" [selected]="draft().purity === p">{{ p }}</option> }
        </select>
      </label>
      <label>Status
        <select (change)="setText('status', $event)">
          <option value="" [selected]="draft().status === ''">All statuses</option>
          @for (s of statuses; track s) { <option [value]="s" [selected]="draft().status === s">{{ s }}</option> }
        </select>
      </label>
      <label>Net weight from (g)
        <input type="number" min="0" step="0.001" [value]="draft().minWeight ?? ''" (input)="setNumber('minWeight', $event)">
      </label>
      <label>Net weight to (g)
        <input type="number" min="0" step="0.001" [value]="draft().maxWeight ?? ''" (input)="setNumber('maxWeight', $event)">
      </label>
      <label class="check">
        <input type="checkbox" [checked]="draft().inStockOnly" (change)="setStock($event)"> In stock only
      </label>
      <div class="buttons">
        <button class="apply" type="button" (click)="onApply()">Apply</button>
        <button class="reset" type="button" (click)="reset.emit()">Reset</button>
      </div>
    </div>
  `,
  styles: [`
    .filters { display:grid; grid-template-columns:repeat(auto-fit, minmax(160px, 1fr)); gap:12px 16px; align-items:end; }
    label { display:flex; flex-direction:column; gap:6px; color:#6f6d68; font-size:12px; font-weight:700; }
    input, select { width:100%; min-height:38px; padding:8px 10px; border:1px solid #cfd1d5; border-radius:6px; background:white; color:#555; outline:none; }
    input:focus, select:focus { border-color:var(--button); }
    label.check { flex-direction:row; align-items:center; gap:8px; min-height:38px; }
    label.check input { width:auto; min-height:0; accent-color:var(--gold); }
    .buttons { display:flex; gap:10px; }
    .apply { min-height:38px; padding:0 20px; border:0; border-radius:6px; background:var(--button); color:#333; font-weight:700; }
    .reset { min-height:38px; padding:0 20px; border:1px solid var(--gold); border-radius:6px; background:white; color:var(--gold); font-weight:700; }
  `]
})
export class CatalogFiltersComponent {
  readonly applied = input.required<CatalogFilters>();
  readonly categories = input<Category[]>([]);
  readonly metals = input<string[]>([]);
  readonly purities = input<string[]>([]);
  readonly apply = output<CatalogFilters>();
  readonly reset = output<void>();

  readonly statuses = ITEM_STATUSES;
  readonly draft = signal<CatalogFilters>({ ...DEFAULT_CATALOG_FILTERS });

  constructor() {
    // Whenever the list changes the applied filters, copy them into the draft.
    effect(() => this.draft.set({ ...this.applied() }));
  }

  setText(field: 'categoryId' | 'metal' | 'purity' | 'status', event: Event): void {
    const value = (event.target as HTMLSelectElement).value;
    this.draft.update(d => ({ ...d, [field]: field === 'categoryId' ? (value === '' ? null : Number(value)) : value }));
  }

  setNumber(field: 'minWeight' | 'maxWeight', event: Event): void {
    const value = (event.target as HTMLInputElement).value;
    this.draft.update(d => ({ ...d, [field]: value === '' ? null : Number(value) }));
  }

  setStock(event: Event): void {
    this.draft.update(d => ({ ...d, inStockOnly: (event.target as HTMLInputElement).checked }));
  }

  onApply(): void {
    const d = this.draft();
    const swap = d.minWeight !== null && d.maxWeight !== null && d.minWeight > d.maxWeight;
    this.apply.emit(swap ? { ...d, minWeight: d.maxWeight, maxWeight: d.minWeight } : d);
  }
}
