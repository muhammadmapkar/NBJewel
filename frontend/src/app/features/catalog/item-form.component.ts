// Role: The item form screen, used for /catalog/new and /catalog/:id/edit.
// It is a reactive form with validation, and saves through ItemDataService.
// Design code is a dropdown fed by the design master. Status is not on the form:
// new items start as Draft and status is changed from the item page.

import { Component, computed, inject, signal } from '@angular/core';
import { takeUntilDestroyed } from '@angular/core/rxjs-interop';
import { AbstractControl, FormControl, FormGroup, ReactiveFormsModule, ValidationErrors, Validators } from '@angular/forms';
import { ActivatedRoute, Router, RouterLink } from '@angular/router';
import { forkJoin, of } from 'rxjs';
import { ItemDataService } from '../../core/data/item-data.service';
import { ApiError } from '../../core/http/error.interceptor';
import { Category, Design, Item, ItemWriteRequest, MetalRate } from '../../core/models';
import { errorMessage, MAKING_TYPES } from './item-options';

type FormState = 'loading' | 'ready' | 'notfound' | 'error';
type FieldName = 'code' | 'name' | 'categoryId' | 'designId' | 'metalType' | 'purity' | 'grossWt' | 'netWt' | 'stoneWt' | 'makingType' | 'makingValue' | 'huid';

const LABELS: Record<FieldName, string> = {
  code: 'Code', name: 'Name', categoryId: 'Category', designId: 'Design code', metalType: 'Metal',
  purity: 'Purity', grossWt: 'Gross weight', netWt: 'Net weight', stoneWt: 'Stone weight',
  makingType: 'Making type', makingValue: 'Making value', huid: 'HUID'
};

// Group-level rule: net weight may not be above gross weight.
function netNotAboveGross(group: AbstractControl): ValidationErrors | null {
  const gross = group.get('grossWt')?.value as number | null;
  const net = group.get('netWt')?.value as number | null;
  return typeof gross === 'number' && typeof net === 'number' && net > gross ? { netExceedsGross: true } : null;
}

@Component({
  selector: 'nb-item-form', standalone: true,
  imports: [ReactiveFormsModule, RouterLink],
  template: `
    @switch (state()) {
      @case ('loading') {
        <div class="card" aria-busy="true" aria-label="Loading form">
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
        @if (categories().length === 0) {
          <div class="card message">No categories are available, so an item cannot be saved yet. <a routerLink="/catalog">Back to Item Catalog</a></div>
        } @else {
          <div class="page-head">
            <div>
              <h1>{{ isEdit() ? 'Edit item' : 'New item' }}</h1>
              <p>Inventory　›　Item Catalog　›　{{ isEdit() ? 'Edit' : 'New' }}</p>
            </div>
          </div>

          <form [formGroup]="form" (ngSubmit)="save()" novalidate>
            <div class="form-card">
              <label class="required">Code
                <input type="text" formControlName="code" [class.invalid]="error('code')">
                @if (error('code'); as e) { <span class="err">{{ e }}</span> }
              </label>
              <label class="required">Name
                <input type="text" formControlName="name" [class.invalid]="error('name')">
                @if (error('name'); as e) { <span class="err">{{ e }}</span> }
              </label>
              <label class="required">Category
                <select formControlName="categoryId" [class.invalid]="error('categoryId')">
                  <option [ngValue]="null">Select a category</option>
                  @for (c of categories(); track c.id) { <option [ngValue]="c.id">{{ c.name }}</option> }
                </select>
                @if (error('categoryId'); as e) { <span class="err">{{ e }}</span> }
              </label>
              <label class="required">Design code
                <select formControlName="designId" [class.invalid]="error('designId')">
                  <option [ngValue]="null">Select a design code</option>
                  @for (d of designs(); track d.designId) { <option [ngValue]="d.designId">{{ d.designCode }} - {{ d.name }}</option> }
                </select>
                @if (error('designId'); as e) { <span class="err">{{ e }}</span> }
              </label>
              <label class="required">Metal
                <select formControlName="metalType" (change)="onMetalChange()" [class.invalid]="error('metalType')">
                  <option value="">Select a metal</option>
                  @for (m of metalOptions(); track m) { <option [value]="m">{{ m }}</option> }
                </select>
                @if (error('metalType'); as e) { <span class="err">{{ e }}</span> }
              </label>
              <label class="required">Purity
                <select formControlName="purity" [class.invalid]="error('purity')">
                  <option value="">Select a purity</option>
                  @for (p of purityOptions(); track p) { <option [value]="p">{{ p }}</option> }
                </select>
                @if (error('purity'); as e) { <span class="err">{{ e }}</span> }
              </label>
              <label class="required">Gross weight (g)
                <input type="number" step="0.001" formControlName="grossWt" [class.invalid]="error('grossWt')">
                @if (error('grossWt'); as e) { <span class="err">{{ e }}</span> }
              </label>
              <label class="required">Net weight (g)
                <input type="number" step="0.001" formControlName="netWt" [class.invalid]="error('netWt')">
                @if (error('netWt'); as e) { <span class="err">{{ e }}</span> }
              </label>
              <label class="required">Stone weight (g)
                <input type="number" step="0.001" formControlName="stoneWt" [class.invalid]="error('stoneWt')">
                @if (error('stoneWt'); as e) { <span class="err">{{ e }}</span> }
              </label>
              <label class="required">Making type
                <select formControlName="makingType" [class.invalid]="error('makingType')">
                  <option value="">Select a making type</option>
                  @for (t of makingTypes; track t.value) { <option [value]="t.value">{{ t.label }}</option> }
                </select>
                @if (error('makingType'); as e) { <span class="err">{{ e }}</span> }
              </label>
              <label class="required">Making value
                <input type="number" step="0.01" formControlName="makingValue" [class.invalid]="error('makingValue')">
                @if (error('makingValue'); as e) { <span class="err">{{ e }}</span> }
              </label>
              <label>HUID
                <input type="text" formControlName="huid">
              </label>
            </div>

            @if (saveError()) { <p class="save-error" role="alert">{{ saveError() }}</p> }

            <div class="buttons">
              <button type="submit" class="save" [disabled]="form.invalid || saving()">{{ saving() ? 'Saving...' : 'Save' }}</button>
              <button type="button" class="cancel" [disabled]="saving()" (click)="cancel()">Cancel</button>
            </div>
          </form>
        }
      }
    }
  `,
  styles: [`
    .page-head { margin-bottom:12px; }
    h1 { margin:0 0 5px; font-size:25px; }
    p { margin:0; color:var(--muted); font-size:12px; }
    .card { background:white; padding:16px; border-radius:8px; }
    .message { text-align:center; color:var(--muted); padding:40px 16px; }
    .message a, .link { color:var(--gold); font-weight:700; }
    .link { border:0; background:transparent; padding:0; text-decoration:underline; }
    .form-card { display:grid; grid-template-columns:1fr 1fr; gap:15px 22px; background:white; padding:20px 16px; border-radius:8px; }
    label { display:flex; flex-direction:column; gap:6px; color:#6f6d68; font-size:12px; font-weight:700; }
    label.required:before { content:'* '; color:var(--danger); display:inline; }
    label.required { display:flex; }
    input, select { width:100%; min-height:38px; padding:8px 10px; border:1px solid #cfd1d5; border-radius:6px; background:white; color:#555; outline:none; }
    input:focus, select:focus { border-color:var(--button); }
    input.invalid, select.invalid { border-color:var(--danger); }
    .err { color:var(--danger); font-size:12px; font-weight:600; }
    .save-error { margin:14px 0 0; color:var(--danger); font-weight:600; }
    .buttons { display:flex; gap:10px; margin-top:16px; }
    .save { min-height:38px; padding:0 24px; border:0; border-radius:6px; background:var(--button); color:#333; font-weight:700; }
    .cancel { min-height:38px; padding:0 24px; border:1px solid var(--gold); border-radius:6px; background:white; color:var(--gold); font-weight:700; }
    button:disabled { opacity:.5; cursor:default; }
    .skeleton { display:block; border-radius:4px; background:linear-gradient(90deg, #efe9d6 25%, #f8f3e4 50%, #efe9d6 75%); background-size:200% 100%; animation:shimmer 1.2s infinite; }
    .skeleton.line { height:22px; margin-bottom:12px; }
    @keyframes shimmer { from { background-position:200% 0; } to { background-position:-200% 0; } }
    @media (max-width:650px) { .form-card { grid-template-columns:1fr; } }
  `]
})
export class ItemFormComponent {
  private readonly data = inject(ItemDataService);
  private readonly route = inject(ActivatedRoute);
  private readonly router = inject(Router);
  private editId: number | null = null;
  private imageUrl: string | null = null;

  readonly skeletonRows = [1, 2, 3, 4, 5, 6];
  readonly makingTypes = MAKING_TYPES;

  readonly state = signal<FormState>('loading');
  readonly loadError = signal('');
  readonly saving = signal(false);
  readonly saveError = signal<string | null>(null);
  readonly categories = signal<Category[]>([]);
  readonly designs = signal<Design[]>([]);
  readonly rates = signal<MetalRate[]>([]);
  readonly isEdit = signal(false);
  private readonly selectedMetal = signal('');
  private readonly loadedMetal = signal('');
  private readonly loadedPurity = signal('');

  readonly form = new FormGroup({
    code: new FormControl('', { nonNullable: true, validators: [Validators.required] }),
    name: new FormControl('', { nonNullable: true, validators: [Validators.required] }),
    categoryId: new FormControl<number | null>(null, [Validators.required]),
    designId: new FormControl<number | null>(null, [Validators.required]),
    metalType: new FormControl('', { nonNullable: true, validators: [Validators.required] }),
    purity: new FormControl('', { nonNullable: true, validators: [Validators.required] }),
    grossWt: new FormControl<number | null>(null, [Validators.required, Validators.min(0)]),
    netWt: new FormControl<number | null>(null, [Validators.required, Validators.min(0)]),
    stoneWt: new FormControl<number | null>(0, [Validators.required, Validators.min(0)]),
    makingType: new FormControl('', { nonNullable: true, validators: [Validators.required] }),
    makingValue: new FormControl<number | null>(null, [Validators.required, Validators.min(0)]),
    huid: new FormControl('', { nonNullable: true })
  }, { validators: [netNotAboveGross] });

  // The item's own metal and purity are always offered, even if no rate exists for them.
  readonly metalOptions = computed(() => this.withCurrent([...new Set(this.rates().map(r => r.metalType))], this.loadedMetal()));
  readonly purityOptions = computed(() => this.withCurrent(
    [...new Set(this.rates().filter(r => r.metalType === this.selectedMetal()).map(r => r.purity))],
    this.selectedMetal() === this.loadedMetal() ? this.loadedPurity() : ''));

  constructor() {
    this.form.controls.metalType.valueChanges.pipe(takeUntilDestroyed()).subscribe(v => this.selectedMetal.set(v));
    this.route.paramMap.pipe(takeUntilDestroyed()).subscribe(params => {
      const raw = params.get('id');
      this.isEdit.set(raw !== null);
      this.editId = raw === null ? null : Number(raw);
      this.load();
    });
  }

  load(): void {
    if (this.editId !== null && (!Number.isInteger(this.editId) || this.editId < 1)) { this.state.set('notfound'); return; }
    const editId = this.editId;
    this.state.set('loading');
    forkJoin({
      categories: this.data.getCategories(),
      designs: this.data.getDesigns(),
      rates: this.data.getLatestMetalRates(),
      item: editId === null ? of(null) : this.data.getItem(editId)
    }).subscribe({
      next: ({ categories, designs, rates, item }) => {
        if (editId !== this.editId) return;
        this.categories.set(categories.filter(c => !categories.some(o => o.parentId === c.id)));
        this.designs.set(designs);
        this.rates.set(rates);
        if (item) this.fillForm(item);
        this.state.set('ready');
      },
      error: (error: unknown) => {
        if (error instanceof ApiError && error.status === 404) { this.state.set('notfound'); return; }
        this.loadError.set(`Could not load the form. ${errorMessage(error)}`);
        this.state.set('error');
      }
    });
  }

  onMetalChange(): void {
    const purity = this.form.controls.purity;
    if (!this.purityOptions().includes(purity.value)) purity.setValue('');
  }

  error(name: FieldName): string | null {
    const control = this.form.controls[name];
    const showNet = name === 'netWt' && this.form.hasError('netExceedsGross')
      && (this.form.controls.netWt.touched || this.form.controls.grossWt.touched || this.form.controls.netWt.dirty);
    if (showNet) return 'Net weight cannot exceed gross weight.';
    if (!(control.touched || control.dirty) || !control.errors) return null;
    if (control.hasError('required')) return `${LABELS[name]} is required.`;
    if (control.hasError('min')) return `${LABELS[name]} cannot be negative.`;
    return 'This value is not valid.';
  }

  save(): void {
    this.form.markAllAsTouched();
    if (this.form.invalid || this.saving()) return;
    const v = this.form.getRawValue();
    const request: ItemWriteRequest = {
      code: v.code.trim(),
      name: v.name.trim(),
      categoryId: v.categoryId as number,
      designId: v.designId as number,
      metalType: v.metalType,
      purity: v.purity,
      grossWt: v.grossWt as number,
      netWt: v.netWt as number,
      stoneWt: v.stoneWt as number,
      makingType: v.makingType,
      makingValue: v.makingValue as number,
      huid: v.huid.trim() || null,
      imageUrl: this.imageUrl
    };
    this.saving.set(true);
    this.saveError.set(null);
    const call = this.editId === null ? this.data.createItem(request) : this.data.updateItem(this.editId, request);
    call.subscribe({
      next: item => {
        this.saving.set(false);
        void this.router.navigate(['/catalog', item.id], { state: { toast: this.editId === null ? 'Item created.' : 'Item saved.' } });
      },
      error: (error: unknown) => {
        this.saving.set(false);
        this.saveError.set(errorMessage(error));
      }
    });
  }

  cancel(): void {
    void this.router.navigate(this.editId === null ? ['/catalog'] : ['/catalog', this.editId]);
  }

  private fillForm(item: Item): void {
    this.imageUrl = item.imageUrl;
    this.loadedMetal.set(item.metalType);
    this.loadedPurity.set(item.purity);
    this.form.reset({
      code: item.code, name: item.name, categoryId: item.categoryId, designId: item.designId,
      metalType: item.metalType, purity: item.purity, grossWt: item.grossWt, netWt: item.netWt,
      stoneWt: item.stoneWt, makingType: this.matchMakingType(item.makingType), makingValue: item.makingValue,
      huid: item.huid ?? ''
    });
    this.selectedMetal.set(item.metalType);
  }

  private matchMakingType(value: string): string {
    return MAKING_TYPES.find(t => t.value.toLowerCase() === value.toLowerCase())?.value ?? value;
  }

  private withCurrent(options: string[], current: string): string[] {
    return current && !options.includes(current) ? [...options, current] : options;
  }
}
