// Role: Uses the existing ERP widget/theme for real upload, manual preview review and explicit
// publication. It never approves a cutout or publishes an item automatically.
import { Component, OnDestroy, computed, effect, inject, input, output, signal, untracked } from '@angular/core';
import { FormsModule } from '@angular/forms';
import { Observable, forkJoin, of } from 'rxjs';
import { AuthService } from '../../core/auth/auth.service';
import { StorefrontAdminService, StorefrontAsset, publicationReady } from '../../core/data/storefront-admin.service';
import { Item } from '../../core/models';
import { WidgetCardComponent } from '../../shared/ui/widget-card.component';
import { errorMessage } from './item-options';
import { detectCameraTrimPercent, isCameraTrimPercent, MAX_CAMERA_TRIM_PERCENT } from '../../../../../customer-tryon/src/app/tryon/necklace-crop';
@Component({
  selector: 'nb-storefront-panel', standalone: true, imports: [FormsModule, WidgetCardComponent],
  template: `
    <nb-widget-card title="Storefront image and publication">
      <div class="toolbar">
        <button type="button" class="upload" [disabled]="busy() || !canUpload()" (click)="file.click()">Upload item image</button>
        <input #file type="file" accept="image/png,image/jpeg" hidden (change)="upload($event)" />
        <span class="hint">PNG or JPG, up to 5 MB. Necklaces and earrings only.</span>
      </div>
      @if (error()) { <p class="error" role="alert">{{ error() }}</p> }
      @if (notice()) { <p role="status">{{ notice() }}</p> }
      @if (preview(); as image) {
        <div class="previews" [class.necklace]="isNecklace()">
          <figure>
            @if (isNecklace()) { <figcaption>Full cutout and cut line</figcaption> }
            <div class="frame" [class.cut-line-frame]="isNecklace()">
              <img #original [src]="image" alt="Full item cutout for visual review" (load)="previewRendered(original)" />
              @if (isNecklace() && (cameraTrim() ?? 0) > 0) {
                <div class="cut-line" [style.height.%]="cameraTrim()" aria-hidden="true"></div>
              }
            </div>
          </figure>
          @if (isNecklace() && cameraAspect(); as aspect) {
            <figure>
              <figcaption>Camera cutout</figcaption>
              <div class="frame camera-frame" [style.aspect-ratio]="aspect">
                <img [src]="image" alt="Necklace front used by the camera" [style.transform]="'translateY(-' + cameraTrim() + '%)'" />
              </div>
            </figure>
          }
        </div>
        @if (isNecklace()) {
          <label for="camera-trim">Hide the back chain from the camera ({{ cameraTrim() ?? 0 }}% from the top)</label>
          <input #trim id="camera-trim" type="range" min="0" [max]="maxCameraTrim" step="1"
            [value]="cameraTrim() ?? 0" [disabled]="busy() || !canUpload() || cameraTrim() === null" (input)="updateCameraTrim(trim.valueAsNumber)" />
          <p class="hint">Move the dashed line below the clasp. Check that the front sides and pendant are visible in the camera cutout.</p>
          <button type="button" [disabled]="busy() || !canUpload() || !trimChanged()" (click)="saveCameraTrim()">Save camera trim</button>
          @if (trimChanged()) { <p class="hint" role="status">Save the cut line before approving or publishing.</p> }
        }
      }
      <p class="hint">Preview: {{ asset()?.state || 'not uploaded' }}. Quality: {{ asset()?.qualityReview || 'not reviewed' }}. Image licensing remains open for this sample demo.</p>
      <div class="toolbar">
        <button type="button" [disabled]="analyzing() || busy() || !canUpload() || !preview()" (click)="analyze()">Re-generate with AI</button>
        @if (analyzing()) { <span class="hint" role="status">Analyzing jewelry image with Gemini...</span> }
      </div>
      @if (aiNotice()) { <p class="hint" role="status">{{ aiNotice() }}</p> }
      <label>Description <textarea [(ngModel)]="description" maxlength="4000" [disabled]="busy() || !canUpload()"></textarea></label>
      <label>Collection <input [(ngModel)]="collection" maxlength="100" [disabled]="busy() || !canUpload()" /></label>
      <div class="toolbar">
        <button type="button" [disabled]="busy() || !canUpload() || !description.trim()" (click)="saveCopy()">Save storefront details</button>
        <label><input type="checkbox" [(ngModel)]="reviewChecked" [disabled]="busy() || !canManage() || asset()?.state !== 'ready' || !reviewablePreview()" /> I checked this preview's visual quality</label>
        <button type="button" [disabled]="busy() || !canManage() || !reviewChecked || asset()?.state !== 'ready' || !reviewablePreview()" (click)="approve()">Approve preview quality</button>
      </div>
      <p>Storefront: {{ published() ? 'published' : 'not published' }}. The item must be Complete and its current preview approved.</p>
      <div class="toolbar">
        <button type="button" [disabled]="busy() || !canManage() || !ready() || !reviewablePreview()" (click)="publish()">Confirm publish</button>
        <button type="button" [disabled]="busy() || !canManage() || !published()" (click)="revoke()">Revoke publication</button>
        <button type="button" [disabled]="busy()" (click)="load()">Refresh</button>
      </div>
    </nb-widget-card>
  `,
  styles: [`
    .toolbar { display:flex; align-items:center; flex-wrap:wrap; gap:14px; margin:14px 0; }
    button { min-height:38px; padding:0 16px; border:1px solid var(--gold); border-radius:6px; background:white; color:var(--gold); font-weight:700; }
    .upload { background:var(--button); color:var(--ink); border:0; }
    button:disabled { opacity:.45; cursor:default; }
    .hint { color:var(--muted); font-size:12px; }
    .error { color:var(--danger); }
    label { display:block; margin:10px 0; }
    textarea, input:not([type=checkbox]) { display:block; width:100%; max-width:640px; padding:8px; border:1px solid var(--line); border-radius:6px; font:inherit; }
    .frame { max-width:320px; aspect-ratio:1; border:1px solid var(--line); border-radius:8px; background:var(--page); }
    .frame img { width:100%; height:100%; object-fit:contain; }
    .previews.necklace { display:flex; flex-wrap:wrap; align-items:flex-start; gap:16px; }
    figure { margin:0; }
    .necklace figure { width:min(100%, 320px); }
    figcaption { margin-bottom:6px; font-weight:600; }
    .cut-line-frame, .camera-frame { position:relative; overflow:hidden; aspect-ratio:auto; }
    .cut-line-frame img, .camera-frame img { width:100%; height:auto; display:block; }
    .camera-frame img { position:absolute; inset:0 auto auto 0; }
    .cut-line { position:absolute; inset:0 0 auto; pointer-events:none; background:rgba(135,40,40,0.2); border-bottom:2px dashed var(--danger); }
  `]
})
export class StorefrontPanelComponent implements OnDestroy {
  readonly item = input.required<Item>();
  readonly categoryName = input('');
  readonly itemSaved = output<Item>();
  private readonly api = inject(StorefrontAdminService);
  private readonly auth = inject(AuthService);
  readonly asset = signal<StorefrontAsset | null>(null);
  readonly saved = signal<Item | null>(null);
  readonly preview = signal<string | null>(null);
  readonly busy = signal(false); readonly published = signal(false);
  readonly error = signal(''); readonly notice = signal('');
  private generation = 0;
  private analysis = 0;
  private trimEdit = 0;
  private autoAnalyzeRevision: string | null = null;
  readonly analyzing = signal(false); readonly aiNotice = signal('');
  readonly maxCameraTrim = MAX_CAMERA_TRIM_PERCENT;
  readonly isNecklace = computed(() => this.categoryName() === 'Necklaces');
  readonly cameraTrim = signal<number | null>(null);
  private readonly baselineTrim = signal<number | null>(null);
  readonly trimChanged = computed(() => this.isNecklace() && this.cameraTrim() !== this.baselineTrim());
  readonly reviewablePreview = computed(() => !!this.preview() && this.asset()?.itemId === this.item().id &&
    (!this.isNecklace() || (this.cameraTrim() !== null && !this.trimChanged())));
  readonly cameraAspect = computed(() => {
    const asset = this.asset(); const trim = this.cameraTrim();
    return asset && asset.height > 0 && trim !== null ? asset.width / (asset.height * (1 - trim / 100)) : null;
  });
  description = ''; collection = ''; reviewChecked = false;
  readonly canUpload = computed(() => { const u = this.auth.identity(); return u?.accountType === 'erp' && u.allStores && ['items.read', 'items.write', 'prices.read', 'status.read'].every(p => u.permissions.includes(p)); });
  readonly canManage = computed(() => this.canUpload() && this.auth.identity()!.permissions.includes('knowledge.manage'));
  readonly ready = computed(() => publicationReady(this.saved() ?? this.item(), this.asset(), this.categoryName()));
  constructor() { effect(() => { this.item(); untracked(() => { this.saved.set(null); this.load(); }); }); }
  ngOnDestroy(): void { this.analysis++; this.generation++; this.clearPreview(); }
  private clearPreview(): void { const value = this.preview(); if (value) URL.revokeObjectURL(value); this.preview.set(null); this.cameraTrim.set(null); this.baselineTrim.set(null); }
  load(): void {
    const generation = ++this.generation; this.analysis++; this.analyzing.set(false);
    const item = this.saved() ?? this.item(); this.description = item.storefront?.description ?? ''; this.collection = item.storefront?.collection ?? '';
    this.error.set(''); this.reviewChecked = false; this.clearPreview();
    forkJoin({ asset: this.api.asset(item.id), publication: this.canManage() ? this.api.state(item.id) : of({ published: false }) }).subscribe({
      next: result => { if (generation !== this.generation || item.id !== this.item().id) return; this.asset.set(result.asset); this.published.set(result.publication.published); if (result.asset?.state === 'ready') this.api.preview(item.id, result.asset.revisionId).subscribe({ next: blob => { if (generation === this.generation && item.id === this.item().id) this.preview.set(URL.createObjectURL(blob)); }, error: e => { if (generation === this.generation) this.error.set(errorMessage(e)); } }); },
      error: e => { if (generation === this.generation) this.error.set(errorMessage(e)); }
    });
  }
  upload(event: Event): void {
    const input = event.target as HTMLInputElement; const file = input.files?.[0]; input.value = '';
    if (!file) return;
    if (!['image/png', 'image/jpeg'].includes(file.type) || file.size > 5 * 1024 * 1024) { this.error.set('Choose PNG or JPG, up to 5 MB.'); return; }
    this.run(this.api.upload(this.item().id, file), 'Preview generated. Check its visual quality before approval.', value => { this.autoAnalyzeRevision = (value as StorefrontAsset).revisionId; this.aiNotice.set(''); });
  }
  previewRendered(image: HTMLImageElement): void {
    if (image.src !== this.preview()) return;
    const saved = this.asset()?.cameraTrimPercent;
    const trim = isCameraTrimPercent(saved) ? saved : detectCameraTrimPercent(image);
    this.baselineTrim.set(trim); this.cameraTrim.set(trim);
    if (this.autoAnalyzeRevision && this.autoAnalyzeRevision === this.asset()?.revisionId) {
      this.autoAnalyzeRevision = null; this.analyze();
    }
  }
  analyze(): void {
    const asset = this.asset(); if (!asset || !this.preview() || !this.canUpload() || this.analyzing()) return;
    const token = ++this.analysis; const itemId = this.item().id;
    const before = { description: this.description, collection: this.collection, trimEdit: this.trimEdit };
    this.analyzing.set(true); this.aiNotice.set('');
    this.api.analyze(itemId, asset.revisionId).subscribe({
      next: value => {
        if (token !== this.analysis || itemId !== this.item().id || value.revisionId !== this.asset()?.revisionId) return;
        if (this.description === before.description) this.description = value.description;
        if (this.collection === before.collection) this.collection = value.collection;
        if (this.isNecklace() && this.trimEdit === before.trimEdit && isCameraTrimPercent(value.cameraTrimPercent)) {
          this.updateCameraTrim(value.cameraTrimPercent);
          this.aiNotice.set('AI suggested a cut line. Check the camera cutout before saving.');
        } else {
          this.aiNotice.set('AI draft ready. Review and edit before saving. Your edits made during analysis were kept.');
        }
        this.analyzing.set(false);
      },
      error: () => { if (token === this.analysis) { this.analyzing.set(false); this.aiNotice.set('AI suggestions are unavailable or busy. You can enter the details manually or try again.'); } }
    });
  }
  updateCameraTrim(trim: number): void {
    if (!isCameraTrimPercent(trim) || trim === this.cameraTrim()) return;
    this.trimEdit++; this.cameraTrim.set(trim); this.reviewChecked = false;
  }
  saveCameraTrim(): void {
    const asset = this.asset(); const trim = this.cameraTrim();
    const itemId = this.item().id;
    if (!asset || asset.itemId !== itemId || trim === null || !this.trimChanged() || !this.canUpload() || this.busy()) return;
    this.run(this.api.saveCameraTrim(itemId, asset.revisionId, trim), 'Camera trim saved. Review this revision before publishing.', value => {
      if (this.item().id !== itemId || this.asset()?.revisionId !== asset.revisionId) return;
      this.analysis++; this.analyzing.set(false); this.reviewChecked = false;
      this.asset.set(value as StorefrontAsset); this.baselineTrim.set(trim); this.published.set(false);
    }, false);
  }
  saveCopy(): void {
    const item = this.saved() ?? this.item();
    this.run(this.api.saveCopy(item, { ...item.storefront, description: this.description, collection: this.collection, isSample: item.storefront?.isSample ?? true }), 'Storefront details saved.', value => { this.saved.set(value as Item); this.itemSaved.emit(value as Item); });
  }
  approve(): void { if (this.reviewChecked && this.asset() && this.reviewablePreview()) this.run(this.api.review(this.item().id, this.asset()!.revisionId), 'Preview quality approval saved.'); }
  publish(): void { if (this.ready() && this.asset() && this.reviewablePreview()) this.run(this.api.publish(this.item().id, this.asset()!.revisionId), 'Item published.'); }
  revoke(): void { this.run(this.api.revoke(this.item().id), 'Publication revoked.'); }
  private run(call: Observable<unknown>, message: string, accept?: (value: unknown) => void, reload = true): void {
    this.busy.set(true); this.error.set(''); this.notice.set('');
    call.subscribe({ next: value => { accept?.(value); this.busy.set(false); this.notice.set(message); if (reload) this.load(); }, error: e => { this.busy.set(false); this.generation++; this.error.set(errorMessage(e)); this.asset.set(null); this.published.set(false); this.clearPreview(); } });
  }
}
