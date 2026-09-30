// Role: The Images tab. Shows an item's pictures in a grid and lets the user
// upload (mock), set the primary image, reorder and remove.
// Assumed, no backend endpoint yet: images come from sample data, and an
// upload only adds a placeholder picture named after the chosen file.

import { Component, computed, effect, inject, input, signal, untracked } from '@angular/core';
import { Observable } from 'rxjs';
import { ItemDataService } from '../../core/data/item-data.service';
import { ItemImage } from '../../core/models';
import { errorMessage } from './item-options';
import { Loadable } from './loadable';

const MAX_BYTES = 5 * 1024 * 1024;
const ALLOWED_TYPES = ['image/jpeg', 'image/png'];
const UPLOAD_ERROR = 'Upload failed. Max 5 MB. JPG or PNG.';

@Component({
  selector: 'nb-image-gallery', standalone: true,
  template: `
    <section class="card">
      <div class="toolbar">
        <button type="button" class="upload" [disabled]="busy()" (click)="fileInput.click()">＋ Upload</button>
        <input #fileInput type="file" accept="image/jpeg,image/png" hidden (change)="onFile($event)">
        <span class="hint">JPG or PNG, up to 5 MB</span>
      </div>
      @if (uploadError()) { <p class="error" role="alert">{{ uploadError() }}</p> }
      @if (actionError()) { <p class="error" role="alert">{{ actionError() }}</p> }

      @switch (images().status) {
        @case ('loading') { <div class="grid" aria-busy="true" aria-label="Loading images">@for (n of skeletons; track n) { <div class="skeleton tile"></div> }</div> }
        @case ('error') { <div class="message">{{ loadError() }} <button type="button" class="link" (click)="load()">Retry</button></div> }
        @default {
          @if (rows().length === 0) {
            <div class="message">No images yet. Upload one.</div>
          } @else {
            <ul class="grid">
              @for (img of rows(); track img.id; let first = $first; let last = $last) {
                <li class="tile">
                  <div class="frame">
                    <img [src]="img.url" [alt]="img.caption">
                    @if (img.isPrimary) { <span class="badge">Primary</span> }
                  </div>
                  <div class="caption" [title]="img.caption">{{ img.caption }}</div>
                  <div class="tile-actions">
                    <button type="button" [disabled]="busy() || img.isPrimary" (click)="makePrimary(img)">Set Primary</button>
                    <button type="button" aria-label="Move up" [disabled]="busy() || first" (click)="move(img, -1)">↑</button>
                    <button type="button" aria-label="Move down" [disabled]="busy() || last" (click)="move(img, 1)">↓</button>
                    <button type="button" class="remove" [disabled]="busy()" (click)="remove(img)">Remove</button>
                  </div>
                </li>
              }
            </ul>
          }
        }
      }
    </section>
  `,
  styles: [`
    .card { background:white; padding:16px; border-radius:8px; }
    .toolbar { display:flex; align-items:center; gap:14px; margin-bottom:14px; }
    .upload { min-height:38px; padding:0 20px; border:0; border-radius:6px; background:var(--button); color:#333; font-weight:700; }
    .hint { color:var(--muted); font-size:12px; }
    .error { margin:0 0 12px; color:var(--danger); font-weight:600; }
    .grid { list-style:none; margin:0; padding:0; display:grid; grid-template-columns:repeat(auto-fill, minmax(180px, 1fr)); gap:14px; }
    .tile { display:flex; flex-direction:column; gap:8px; }
    .frame { position:relative; aspect-ratio:1; border:1px solid var(--line); border-radius:8px; overflow:hidden; background:var(--page); }
    .frame img { width:100%; height:100%; object-fit:cover; display:block; }
    .badge { position:absolute; top:8px; left:8px; padding:3px 10px; border-radius:12px; font-size:12px; font-weight:700; color:#333; background:var(--button); }
    .caption { font-size:12px; color:var(--muted); white-space:nowrap; overflow:hidden; text-overflow:ellipsis; }
    .tile-actions { display:flex; flex-wrap:wrap; gap:6px; }
    .tile-actions button { min-height:30px; padding:0 10px; border:1px solid var(--gold); border-radius:6px; background:white; color:var(--gold); font-size:12px; font-weight:700; }
    .tile-actions .remove { border-color:var(--danger); color:var(--danger); }
    button:disabled { opacity:.45; cursor:default; }
    .message { text-align:center; color:var(--muted); padding:30px 12px; }
    .link { border:0; background:transparent; padding:0; color:var(--gold); font-weight:700; text-decoration:underline; }
    .skeleton.tile { aspect-ratio:1; border-radius:8px; background:linear-gradient(90deg, #efe9d6 25%, #f8f3e4 50%, #efe9d6 75%); background-size:200% 100%; animation:shimmer 1.2s infinite; }
    @keyframes shimmer { from { background-position:200% 0; } to { background-position:-200% 0; } }
  `]
})
export class ImageGalleryComponent {
  private readonly data = inject(ItemDataService);

  readonly itemId = input.required<number>();
  readonly skeletons = [1, 2, 3, 4];
  readonly images = signal<Loadable<ItemImage[]>>({ status: 'loading' });
  readonly busy = signal(false);
  readonly uploadError = signal<string | null>(null);
  readonly actionError = signal<string | null>(null);

  readonly rows = computed(() => { const i = this.images(); return i.status === 'ready' ? [...i.value].sort((a, b) => a.sortOrder - b.sortOrder) : []; });
  readonly loadError = computed(() => { const i = this.images(); return i.status === 'error' ? i.message : ''; });

  constructor() {
    effect(() => { this.itemId(); untracked(() => this.load()); });
  }

  load(): void {
    const id = this.itemId();
    this.images.set({ status: 'loading' });
    this.data.getImages(id).subscribe({
      next: value => { if (id === this.itemId()) this.images.set({ status: 'ready', value }); },
      error: (error: unknown) => { if (id === this.itemId()) this.images.set({ status: 'error', message: errorMessage(error) }); }
    });
  }

  onFile(event: Event): void {
    const input = event.target as HTMLInputElement;
    const file = input.files?.[0];
    input.value = '';
    this.uploadError.set(null);
    if (!file) return;
    if (!ALLOWED_TYPES.includes(file.type) || file.size > MAX_BYTES) {
      this.uploadError.set(UPLOAD_ERROR);
      return;
    }
    this.run(this.data.addImage(this.itemId(), file.name), true);
  }

  makePrimary(img: ItemImage): void { this.run(this.data.setPrimaryImage(this.itemId(), img.id)); }

  remove(img: ItemImage): void { this.run(this.data.removeImage(this.itemId(), img.id)); }

  move(img: ItemImage, step: -1 | 1): void {
    const ids = this.rows().map(i => i.id);
    const from = ids.indexOf(img.id);
    const to = from + step;
    if (from < 0 || to < 0 || to >= ids.length) return;
    [ids[from], ids[to]] = [ids[to], ids[from]];
    this.run(this.data.reorderImages(this.itemId(), ids));
  }

  // Runs one change, then reloads the list so the screen always shows what is stored.
  private run(call: Observable<unknown>, isUpload = false): void {
    this.busy.set(true);
    this.actionError.set(null);
    call.subscribe({
      next: () => {
        this.busy.set(false);
        this.reload();
      },
      error: (error: unknown) => {
        this.busy.set(false);
        if (isUpload) { this.uploadError.set(UPLOAD_ERROR); } else { this.actionError.set(errorMessage(error)); }
      }
    });
  }

  private reload(): void {
    const id = this.itemId();
    this.data.getImages(id).subscribe({
      next: value => { if (id === this.itemId()) this.images.set({ status: 'ready', value }); },
      error: (error: unknown) => { if (id === this.itemId()) this.images.set({ status: 'error', message: errorMessage(error) }); }
    });
  }
}
