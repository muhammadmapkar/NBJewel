// Role: The Stock tab. Shows on hand, reserved and available stock for one
// item by variant and store, using the real stock, variant and store endpoints.
// It also has the stock entry form: pick a variant and store, enter a quantity,
// and the quantity is ADDED to on-hand stock. The table reloads after each save.

import { DatePipe } from '@angular/common';
import { Component, computed, effect, inject, input, signal, untracked } from '@angular/core';
import { FormControl, FormGroup, ReactiveFormsModule, Validators } from '@angular/forms';
import { catchError, of } from 'rxjs';
import { ItemDataService } from '../../core/data/item-data.service';
import { ItemVariant, StockLevel, Store } from '../../core/models';
import { errorMessage } from './item-options';
import { Loadable } from './loadable';

@Component({
  selector: 'nb-stock-panel', standalone: true, imports: [DatePipe, ReactiveFormsModule],
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
                <thead><tr><th>Store</th><th>Variant</th><th class="num">On hand</th><th class="num">Reserved</th><th class="num">Available</th><th>Last updated</th></tr></thead>
                <tbody>
                  @for (r of rows(); track r.storeId + '-' + r.variantId) {
                    <tr>
                      <td>{{ storeName(r.storeId) }}</td>
                      <td>{{ variantName(r.variantId) }}</td>
                      <td class="num">{{ r.onHand }}</td>
                      <td class="num">{{ r.reserved }}</td>
                      <td class="num">{{ r.onHand - r.reserved }}</td>
                      <td>@if (r.lastUpdated) { {{ r.lastUpdated | date:'mediumDate' }} } @else { <span class="muted">Not available</span> }</td>
                    </tr>
                  }
                  <tr class="total">
                    <td colspan="2">Total</td><td class="num">{{ totalOnHand() }}</td><td class="num">{{ totalReserved() }}</td><td class="num">{{ totalOnHand() - totalReserved() }}</td><td></td>
                  </tr>
                </tbody>
              </table>
            </div>
          }

          <form class="entry" [formGroup]="form" (ngSubmit)="add()" novalidate>
            <h2>Add stock</h2>
            <div class="fields">
              @if (variants().length > 0) {
                <label>Variant
                  <select formControlName="variantId" [class.invalid]="showError('variantId')">
                    <option [ngValue]="null">Select a variant</option>
                    @for (v of variants(); track v.id) { <option [ngValue]="v.id">{{ variantName(v.id) }}</option> }
                  </select>
                  @if (showError('variantId')) { <span class="err">Variant is required.</span> }
                </label>
              }
              <label>Store
                <select formControlName="storeId" [class.invalid]="showError('storeId')">
                  <option [ngValue]="null">Select a store</option>
                  @for (s of stores(); track s.id) { <option [ngValue]="s.id">{{ s.name }}</option> }
                </select>
                @if (showError('storeId')) { <span class="err">Store is required.</span> }
              </label>
              <label>Quantity to add
                <input type="number" step="1" min="1" formControlName="quantity" [class.invalid]="showError('quantity')">
                @if (quantityError(); as e) { <span class="err">{{ e }}</span> }
              </label>
            </div>
            @if (saveError()) { <p class="save-error" role="alert">{{ saveError() }}</p> }
            @if (saveNote()) { <p class="save-note" role="status">{{ saveNote() }}</p> }
            <button type="submit" class="save" [disabled]="saving()">{{ saving() ? 'Saving...' : 'Add stock' }}</button>
          </form>
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
    .entry { margin-top:20px; padding-top:16px; border-top:1px solid var(--line); }
    h2 { margin:0 0 12px; font-size:16px; color:var(--gold); }
    .fields { display:grid; grid-template-columns:repeat(auto-fit, minmax(180px, 1fr)); gap:15px 22px; }
    label { display:flex; flex-direction:column; gap:6px; color:#6f6d68; font-size:12px; font-weight:700; }
    input, select { width:100%; min-height:38px; padding:8px 10px; border:1px solid #cfd1d5; border-radius:6px; background:white; color:#555; outline:none; }
    input:focus, select:focus { border-color:var(--button); }
    input.invalid, select.invalid { border-color:var(--danger); }
    .err { color:var(--danger); font-size:12px; font-weight:600; }
    .save-error { margin:12px 0 0; color:var(--danger); font-weight:600; }
    .save-note { margin:12px 0 0; color:#1e6b3a; font-weight:600; }
    .save { margin-top:14px; min-height:38px; padding:0 24px; border:0; border-radius:6px; background:var(--button); color:#333; font-weight:700; }
    button:disabled { opacity:.5; cursor:default; }
    .skeleton { display:block; height:20px; margin-bottom:12px; border-radius:4px; background:linear-gradient(90deg, #efe9d6 25%, #f8f3e4 50%, #efe9d6 75%); background-size:200% 100%; animation:shimmer 1.2s infinite; }
    @keyframes shimmer { from { background-position:200% 0; } to { background-position:-200% 0; } }
  `]
})
export class StockPanelComponent {
  private readonly data = inject(ItemDataService);

  readonly itemId = input.required<number>();
  readonly stock = signal<Loadable<StockLevel[]>>({ status: 'loading' });
  readonly stores = signal<Store[]>([]);
  readonly variants = signal<ItemVariant[]>([]);
  readonly saving = signal(false);
  readonly saveError = signal<string | null>(null);
  readonly saveNote = signal<string | null>(null);

  // Quantity is added to on-hand, so 0 or less is refused here and by the API.
  readonly form = new FormGroup({
    variantId: new FormControl<number | null>(null),
    storeId: new FormControl<number | null>(null, [Validators.required]),
    quantity: new FormControl<number | null>(null, [Validators.required, Validators.min(1), Validators.pattern(/^\d+$/)])
  });

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
    this.data.getVariants(id).pipe(catchError(() => of<ItemVariant[]>([]))).subscribe(v => {
      if (id !== this.itemId()) return;
      this.variants.set(v);
      // A variant must be chosen only when the item has variants.
      const control = this.form.controls.variantId;
      control.setValidators(v.length > 0 ? [Validators.required] : []);
      control.updateValueAndValidity();
    });
    this.fetchStock(id);
  }

  add(): void {
    this.form.markAllAsTouched();
    this.saveNote.set(null);
    if (this.form.invalid || this.saving()) return;
    const id = this.itemId();
    const v = this.form.getRawValue();
    this.saving.set(true);
    this.saveError.set(null);
    this.data.addStock(id, { variantId: this.variants().length > 0 ? v.variantId : null, storeId: v.storeId as number, quantity: v.quantity as number }).subscribe({
      next: () => {
        this.saving.set(false);
        this.saveNote.set(`Added ${v.quantity} to ${this.storeName(v.storeId as number)}.`);
        this.form.controls.quantity.reset(null);
        this.form.controls.quantity.markAsUntouched();
        this.fetchStock(id);
        this.refreshVariants(id);
      },
      error: (error: unknown) => {
        this.saving.set(false);
        this.saveError.set(errorMessage(error));
      }
    });
  }

  showError(name: 'variantId' | 'storeId' | 'quantity'): boolean {
    const control = this.form.controls[name];
    return control.invalid && (control.touched || control.dirty);
  }

  quantityError(): string | null {
    const control = this.form.controls.quantity;
    if (!this.showError('quantity')) return null;
    if (control.hasError('required')) return 'Quantity is required.';
    if (control.hasError('pattern')) return 'Quantity must be a whole number.';
    return 'Quantity must be greater than 0.';
  }

  storeName(id: number): string { return this.stores().find(s => s.id === id)?.name ?? `Store ${id}`; }

  variantName(id: number | null): string {
    if (id === null) return 'No variant';
    const v = this.variants().find(x => x.id === id);
    return v ? `${v.variantCode} (${[v.size, v.color].filter(Boolean).join(', ')})` : `Variant ${id}`;
  }

  private fetchStock(id: number): void {
    this.data.getStock(id).subscribe({
      next: value => { if (id === this.itemId()) this.stock.set({ status: 'ready', value }); },
      error: (error: unknown) => { if (id === this.itemId()) this.stock.set({ status: 'error', message: errorMessage(error) }); }
    });
  }

  private refreshVariants(id: number): void {
    this.data.getVariants(id).pipe(catchError(() => of<ItemVariant[]>([]))).subscribe(v => { if (id === this.itemId()) this.variants.set(v); });
  }
}
