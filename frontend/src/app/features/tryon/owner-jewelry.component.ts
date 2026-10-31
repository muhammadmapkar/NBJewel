// Role: The shop owner page (/owner/jewelry) for the customer try-on. After the owner enters
// the PIN (checked by the backend against OWNER_PIN, assumed demo gate), it lists all jewelry
// with an Active toggle and a delete button (with a confirm step), and uploads new images.
// PNG with transparency is preferred; for other images it offers in-browser background
// removal with a before and after preview. Shows the customer link as a QR code to scan with
// a phone. Reuses the catalog page, form and toast styles.

import { Component, ElementRef, OnDestroy, computed, inject, signal, viewChild } from '@angular/core';
import { FormsModule } from '@angular/forms';
import * as QRCode from 'qrcode';
import { RouterLink } from '@angular/router';
import { ApiError } from '../../core/http/error.interceptor';
import { ToastComponent } from '../../shared/ui/toast.component';
import { ToggleComponent } from '../../shared/ui/toggle.component';
import { ConfirmDialogComponent } from './confirm-dialog.component';
import { fileHasTransparency, removeBackground } from './image-tools';
import { JEWELRY_TYPES, JewelryItem, JewelryType, MAX_NAME_LENGTH, MAX_UPLOAD_BYTES } from './jewelry.models';
import { JewelryService } from './jewelry.service';

type CutoutState = 'idle' | 'working' | 'ready' | 'failed';

@Component({
  selector: 'nb-owner-jewelry', standalone: true,
  imports: [FormsModule, RouterLink, ToastComponent, ToggleComponent, ConfirmDialogComponent],
  template: `
    <div class="page-head">
      <div>
        <h1>Try-On Jewelry</h1>
        <p class="crumbs">Try-On　›　Owner</p>
      </div>
      <a class="add" routerLink="/tryon" target="_blank" rel="noopener">Open customer page ›</a>
    </div>

    @if (!unlocked()) {
      <form class="card pin-card" (ngSubmit)="unlock()">
        <label class="required">Owner PIN
          <input type="password" name="pin" inputmode="numeric" autocomplete="off" [(ngModel)]="pinInput" [disabled]="checkingPin()" />
        </label>
        @if (pinError()) { <p class="err">{{ pinError() }}</p> }
        <div class="buttons"><button type="submit" class="save" [disabled]="checkingPin() || !pinInput.trim()">{{ checkingPin() ? 'Checking...' : 'Open' }}</button></div>
        <p class="note">Assumed demo gate. Replace with real sign in later.</p>
      </form>
    } @else {
      <form class="form-card" (ngSubmit)="save()">
        <label class="required">Name
          <input name="name" [maxlength]="maxName" [(ngModel)]="name" [disabled]="saving()" />
        </label>
        <label class="required">Type
          <select name="type" [(ngModel)]="type" [disabled]="saving()">
            @for (t of types; track t.value) { <option [value]="t.value">{{ t.label }}</option> }
          </select>
        </label>
        <label class="required wide">Image (PNG or JPG, max 5 MB, transparent PNG works best)
          <input #fileInput type="file" name="file" accept="image/png,image/jpeg" (change)="onFile(fileInput.files)" [disabled]="saving()" />
        </label>
        @if (fileError()) { <p class="err wide">{{ fileError() }}</p> }

        @if (originalUrl()) {
          <div class="wide previews">
            <figure [class.chosen]="!useCutout()">
              <div class="checker"><img [src]="originalUrl()" alt="Original image" /></div>
              <figcaption>{{ transparent() ? 'Original (has transparency)' : 'Original' }}</figcaption>
            </figure>
            @if (cutoutUrl()) {
              <figure [class.chosen]="useCutout()">
                <div class="checker"><img [src]="cutoutUrl()" alt="Background removed" /></div>
                <figcaption>Background removed</figcaption>
              </figure>
            }
          </div>
          @if (transparent() === false) {
            <div class="wide cutout">
              @switch (cutout()) {
                @case ('idle') {
                  <p>This image has no transparent background, so the try-on will show a box around it.</p>
                  <button type="button" class="cancel" (click)="makeCutout()">Remove background</button>
                }
                @case ('working') { <p>Removing the background in this browser. The first run downloads a model and can take a minute.</p> }
                @case ('failed') {
                  <p class="err">Background removal did not work. You can still save the original.</p>
                  <button type="button" class="cancel" (click)="makeCutout()">Try again</button>
                }
                @case ('ready') {
                  <div class="choice">
                    <label class="inline"><input type="radio" name="pick" [checked]="useCutout()" (change)="useCutout.set(true)" /> Use background removed</label>
                    <label class="inline"><input type="radio" name="pick" [checked]="!useCutout()" (change)="useCutout.set(false)" /> Keep original</label>
                  </div>
                }
              }
            </div>
          }
        }

        @if (saveError()) { <p class="save-error wide">{{ saveError() }}</p> }
        <div class="buttons wide">
          <button type="submit" class="save" [disabled]="!canSave()">{{ saving() ? 'Saving...' : 'Save' }}</button>
          <button type="button" class="cancel" [disabled]="saving()" (click)="resetForm()">Clear</button>
        </div>
      </form>

      <div class="card share">
        @if (qr()) { <img class="qr" [src]="qr()" alt="QR code for the customer try-on link" /> }
        <div><b>Customer link</b><p>Scan with a phone, or share this link. The camera needs https.</p><a [href]="customerUrl" target="_blank" rel="noopener">{{ customerUrl }}</a></div>
      </div>

      <div class="card table-card">
        <div class="toolbar"><b>Items</b>@if (items().length) { <span class="count">{{ items().length }} items, {{ activeCount() }} active</span> }</div>
        @if (listError()) {
          <div class="message">{{ listError() }} <button type="button" class="link" (click)="load()">Retry</button></div>
        } @else if (loading()) {
          <div class="message">Loading...</div>
        } @else if (!items().length) {
          <div class="message">No jewelry yet. Upload the first image above.</div>
        } @else {
          <ul class="list">
            @for (item of items(); track item.id + ':' + version()) {
              <li>
                <div class="thumb checker"><img [src]="jewelry.imageSrc(item)" [alt]="item.name" loading="lazy" /></div>
                <div class="info"><b>{{ item.name }}</b><span class="muted">{{ typeLabel(item.type) }}</span></div>
                <span class="active-toggle" [title]="item.active ? 'Shown to customers' : 'Hidden from customers'" (click)="toggleActive(item)">
                  <nb-toggle [on]="item.active" /><span class="muted">{{ item.active ? 'Active' : 'Hidden' }}</span>
                </span>
                <button type="button" class="link danger" (click)="askDelete(item)">Delete</button>
              </li>
            }
          </ul>
        }
      </div>
    }

    @if (pendingDelete(); as item) {
      <nb-confirm-dialog title="Delete jewelry" [message]="'Delete ' + item.name + '? This cannot be undone.'" confirmLabel="Delete"
        [busy]="deleting()" [error]="deleteError()" (confirm)="confirmDelete(item)" (cancel)="pendingDelete.set(null)" />
    }
    <nb-toast [message]="toast()" (dismissed)="toast.set(null)" />
  `,
  styles: [`
    .page-head { display:flex; justify-content:space-between; align-items:flex-start; gap:12px; margin-bottom:12px; }
    .add { font-weight:700; color:#333; text-decoration:none; }
    h1 { margin:0 0 5px; font-size:25px; }
    p { margin:0; color:var(--muted); font-size:12px; }
    .card { background:white; padding:16px; border-radius:8px; margin-bottom:14px; }
    .pin-card { max-width:360px; display:grid; gap:12px; }
    .note { font-size:11px; }
    .form-card { display:grid; grid-template-columns:1fr 1fr; gap:15px 22px; background:white; padding:20px 16px; border-radius:8px; margin-bottom:14px; }
    .wide { grid-column:1 / -1; }
    label { display:flex; flex-direction:column; gap:6px; color:#6f6d68; font-size:12px; font-weight:700; }
    label.required:before { content:'* '; color:var(--danger); display:inline; }
    label.required { display:flex; }
    label.inline { flex-direction:row; align-items:center; gap:6px; }
    label.inline input { width:auto; min-height:0; accent-color:var(--gold); }
    input, select { width:100%; min-height:38px; padding:8px 10px; border:1px solid #cfd1d5; border-radius:6px; background:white; color:#555; outline:none; }
    input:focus, select:focus { border-color:var(--button); }
    .err { color:var(--danger); font-size:12px; font-weight:600; }
    .save-error { margin:0; color:var(--danger); font-weight:600; }
    .buttons { display:flex; gap:10px; }
    .save { min-height:38px; padding:0 24px; border:0; border-radius:6px; background:var(--button); color:#333; font-weight:700; }
    .cancel { min-height:38px; padding:0 24px; border:1px solid var(--gold); border-radius:6px; background:white; color:var(--gold); font-weight:700; }
    button:disabled { opacity:.5; cursor:default; }
    .previews { display:flex; gap:14px; flex-wrap:wrap; }
    figure { margin:0; padding:6px; border:2px solid transparent; border-radius:8px; }
    figure.chosen { border-color:var(--gold); }
    figcaption { margin-top:6px; font-size:12px; color:var(--muted); text-align:center; }
    .checker { background-color:#fff; background-image:linear-gradient(45deg, #eee 25%, transparent 25%), linear-gradient(-45deg, #eee 25%, transparent 25%), linear-gradient(45deg, transparent 75%, #eee 75%), linear-gradient(-45deg, transparent 75%, #eee 75%); background-size:16px 16px; background-position:0 0, 0 8px, 8px -8px, -8px 0; border:1px solid var(--line); border-radius:6px; }
    .previews .checker { width:160px; height:160px; display:grid; place-items:center; }
    .previews img { max-width:150px; max-height:150px; }
    .cutout { display:grid; gap:10px; justify-items:start; }
    .choice { display:flex; gap:18px; flex-wrap:wrap; }
    .share { display:flex; gap:16px; align-items:center; }
    .share p { margin:4px 0 6px; }
    .share a { color:var(--gold); font-weight:700; word-break:break-all; }
    .qr { width:110px; height:110px; flex:none; }
    .table-card { padding:0; overflow:hidden; }
    .toolbar { display:flex; align-items:center; gap:14px; padding:14px 16px; border-bottom:1px solid var(--line); }
    .count, .muted { color:var(--muted); font-size:12px; }
    .message { padding:40px 16px; text-align:center; color:var(--muted); }
    .link { border:0; background:transparent; padding:0; color:var(--gold); font-weight:700; text-decoration:underline; }
    .link.danger { color:var(--danger); }
    .list { list-style:none; margin:0; padding:0; }
    .list li { display:flex; align-items:center; gap:14px; padding:10px 16px; border-bottom:1px solid var(--line); }
    .list li:last-child { border-bottom:0; }
    .thumb { width:56px; height:56px; flex:none; display:grid; place-items:center; }
    .thumb img { max-width:50px; max-height:50px; }
    .info { flex:1; min-width:0; display:flex; flex-direction:column; gap:3px; }
    .info b { overflow:hidden; text-overflow:ellipsis; white-space:nowrap; }
    .active-toggle { display:flex; align-items:center; gap:8px; cursor:pointer; }
    @media (max-width:650px) { .form-card { grid-template-columns:1fr; } .list li { flex-wrap:wrap; } }
  `]
})
export class OwnerJewelryComponent implements OnDestroy {
  readonly jewelry = inject(JewelryService);
  private readonly fileInput = viewChild<ElementRef<HTMLInputElement>>('fileInput');
  readonly types = JEWELRY_TYPES;
  readonly maxName = MAX_NAME_LENGTH;

  pinInput = '';
  readonly unlocked = computed(() => this.jewelry.pin() !== null);
  readonly checkingPin = signal(false);
  readonly pinError = signal<string | null>(null);

  readonly items = signal<JewelryItem[]>([]);
  readonly activeCount = computed(() => this.items().filter(x => x.active).length);
  readonly loading = signal(false);
  readonly listError = signal<string | null>(null);
  // Bumped after each toggle reply, so every toggle redraws from the saved value.
  readonly version = signal(0);
  readonly toast = signal<string | null>(null);
  readonly customerUrl = new URL('tryon', document.baseURI).href;
  readonly qr = signal('');

  name = '';
  type: JewelryType = 'earring';
  readonly file = signal<File | null>(null);
  readonly fileError = signal<string | null>(null);
  readonly originalUrl = signal<string | null>(null);
  readonly transparent = signal<boolean | null>(null);
  readonly cutout = signal<CutoutState>('idle');
  readonly cutoutBlob = signal<Blob | null>(null);
  readonly cutoutUrl = signal<string | null>(null);
  readonly useCutout = signal(false);
  readonly saving = signal(false);
  readonly saveError = signal<string | null>(null);
  readonly canSave = computed(() => !!this.file() && !this.saving() && this.cutout() !== 'working');

  readonly pendingDelete = signal<JewelryItem | null>(null);
  readonly deleting = signal(false);
  readonly deleteError = signal<string | null>(null);

  constructor() {
    if (this.jewelry.pin() !== null) this.load();
    QRCode.toDataURL(this.customerUrl, { margin: 1, width: 220 }).then(url => this.qr.set(url)).catch(() => this.qr.set(''));
  }

  ngOnDestroy(): void { this.clearPreviews(); }

  typeLabel(type: JewelryType): string { return this.types.find(t => t.value === type)?.label ?? type; }

  unlock(): void {
    const pin = this.pinInput.trim();
    if (!pin) return;
    this.checkingPin.set(true);
    this.pinError.set(null);
    this.jewelry.listAll(pin).subscribe({
      next: items => { this.jewelry.pin.set(pin); this.items.set(items); this.checkingPin.set(false); this.pinInput = ''; },
      error: (error: unknown) => { this.checkingPin.set(false); this.pinError.set(this.pinMessage(error)); }
    });
  }

  load(): void {
    const pin = this.jewelry.pin();
    if (pin === null) return;
    this.loading.set(true);
    this.listError.set(null);
    this.jewelry.listAll(pin).subscribe({
      next: items => { this.items.set(items); this.loading.set(false); },
      error: (error: unknown) => { this.loading.set(false); this.handleAuthError(error) || this.listError.set(this.message(error)); }
    });
  }

  async onFile(files: FileList | null): Promise<void> {
    this.clearPreviews();
    this.fileError.set(null);
    this.saveError.set(null);
    const file = files?.[0] ?? null;
    this.file.set(null);
    if (!file) return;
    if (file.type !== 'image/png' && file.type !== 'image/jpeg') { this.fileError.set('Only PNG or JPG images are allowed.'); return; }
    if (file.size > MAX_UPLOAD_BYTES) { this.fileError.set('The image is larger than 5 MB.'); return; }
    this.file.set(file);
    this.originalUrl.set(URL.createObjectURL(file));
    if (!this.name.trim()) this.name = file.name.replace(/\.[^.]+$/, '').slice(0, MAX_NAME_LENGTH);
    try { this.transparent.set(await fileHasTransparency(file)); }
    catch { this.fileError.set('This file could not be read as an image.'); this.file.set(null); }
  }

  async makeCutout(): Promise<void> {
    const file = this.file();
    if (!file) return;
    this.cutout.set('working');
    try {
      const blob = await removeBackground(file);
      if (this.file() !== file) return; // The owner picked another file meanwhile.
      if (blob.size > MAX_UPLOAD_BYTES) throw new Error('too large');
      this.cutoutBlob.set(blob);
      this.cutoutUrl.set(URL.createObjectURL(blob));
      this.useCutout.set(true);
      this.cutout.set('ready');
    } catch {
      if (this.file() === file) this.cutout.set('failed');
    }
  }

  save(): void {
    const file = this.file();
    const name = this.name.trim();
    if (!file || this.saving()) return;
    if (!name) { this.saveError.set('Enter a name.'); return; }
    const blob = this.useCutout() && this.cutoutBlob() ? this.cutoutBlob()! : file;
    const fileName = blob === file ? file.name : 'cutout.png'; // The server ignores the name and makes its own.
    this.saving.set(true);
    this.saveError.set(null);
    this.jewelry.upload(name, this.type, blob, fileName).subscribe({
      next: item => {
        this.saving.set(false);
        this.items.update(list => [...list, item]);
        this.toast.set(`${item.name} saved.`);
        this.resetForm();
      },
      error: (error: unknown) => { this.saving.set(false); this.handleAuthError(error) || this.saveError.set(this.message(error)); }
    });
  }

  resetForm(): void {
    this.clearPreviews();
    this.name = '';
    this.file.set(null);
    this.fileError.set(null);
    this.saveError.set(null);
    const input = this.fileInput();
    if (input) input.nativeElement.value = '';
  }

  toggleActive(item: JewelryItem): void {
    const next = !item.active;
    this.jewelry.setActive(item.id, next).subscribe({
      next: saved => {
        this.items.update(list => list.map(x => (x.id === saved.id ? saved : x)));
        this.version.update(v => v + 1);
        this.toast.set(saved.active ? `${saved.name} is shown to customers.` : `${saved.name} is hidden from customers.`);
      },
      error: (error: unknown) => {
        this.version.update(v => v + 1); // Puts the toggle back to the saved value.
        if (!this.handleAuthError(error)) this.listError.set(this.message(error));
      }
    });
  }

  askDelete(item: JewelryItem): void { this.deleteError.set(null); this.pendingDelete.set(item); }

  confirmDelete(item: JewelryItem): void {
    this.deleting.set(true);
    this.jewelry.remove(item.id).subscribe({
      next: () => {
        this.deleting.set(false);
        this.pendingDelete.set(null);
        this.items.update(list => list.filter(x => x.id !== item.id));
        this.toast.set(`${item.name} deleted.`);
      },
      error: (error: unknown) => {
        this.deleting.set(false);
        if (error instanceof ApiError && error.status === 404) { this.pendingDelete.set(null); this.load(); return; }
        if (this.handleAuthError(error)) { this.pendingDelete.set(null); return; }
        this.deleteError.set(this.message(error));
      }
    });
  }

  // A wrong or missing PIN locks the page again. Returns true when it handled the error.
  private handleAuthError(error: unknown): boolean {
    if (!(error instanceof ApiError) || (error.status !== 401 && error.status !== 503)) return false;
    this.jewelry.pin.set(null);
    this.pinError.set(this.pinMessage(error));
    return true;
  }

  private pinMessage(error: unknown): string {
    if (error instanceof ApiError && error.status === 401) return 'Wrong PIN.';
    if (error instanceof ApiError && error.status === 503) return 'The owner PIN is not set on the server. Set OWNER_PIN for the API.';
    return this.message(error);
  }

  private message(error: unknown): string {
    return error instanceof ApiError ? error.message : 'Something went wrong. Please try again.';
  }

  private clearPreviews(): void {
    for (const url of [this.originalUrl(), this.cutoutUrl()]) if (url) URL.revokeObjectURL(url);
    this.originalUrl.set(null);
    this.cutoutUrl.set(null);
    this.cutoutBlob.set(null);
    this.transparent.set(null);
    this.cutout.set('idle');
    this.useCutout.set(false);
  }
}
