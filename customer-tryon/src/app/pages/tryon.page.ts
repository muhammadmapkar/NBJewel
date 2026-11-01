// Role: The live try-on (/try/:id?parts=necklace,earrings). Opens the phone camera (front by
// default, flip button), finds the face with MediaPipe in the browser, and draws the chosen
// pieces of the design on the video in one canvas: necklace under the chin, earrings at the
// lobes, maang tikka at the hairline. Each piece hangs with simple real-scale physics, so it
// lags, swings and settles when the head moves. Chips switch pieces of a set on and off, and a
// strip switches designs without closing the camera. The shutter saves the canvas as a photo.
// No video leaves the phone. Friendly messages for no https, camera blocked, no camera, camera
// busy, model load failure, and no face found.

import { Component, ElementRef, NgZone, OnDestroy, computed, effect, inject, signal, untracked, viewChild } from '@angular/core';
import { takeUntilDestroyed } from '@angular/core/rxjs-interop';
import { ActivatedRoute, Router, RouterLink } from '@angular/router';
import { combineLatest } from 'rxjs';
import { KIND_LABELS, PieceKind, Product, parseParts } from '../catalog/catalog.models';
import { CatalogService } from '../catalog/catalog.service';
import { LandmarkTracker, createTracker } from '../tryon/landmark-tracker';
import { LandmarkSmoother, Placement, Point, earringPlacements, faceFrame, necklacePlacement, tikkaPlacement, toPixels } from '../tryon/overlay-math';
import { EARRING, NECKLACE, Swing, TIKKA } from '../tryon/physics';

type Phase = 'loading' | 'running' | 'missing' | 'insecure' | 'denied' | 'nocamera' | 'busy' | 'modelerror' | 'error';
type Facing = 'user' | 'environment';
interface Images { necklace?: HTMLImageElement; earringLeft?: HTMLImageElement; earringRight?: HTMLImageElement; tikka?: HTMLImageElement }

// Without a face for this long, the jewelry is hidden (so it never floats where the face was),
// and a little later the hint shows. Time based, so slow cameras behave the same.
const HIDE_AFTER_MS = 250;
const HINT_AFTER_MS = 700;

@Component({
  selector: 'ct-tryon', standalone: true, imports: [RouterLink],
  template: `
    <main class="stage">
      <video #video playsinline muted autoplay aria-hidden="true"></video>
      <canvas #canvas [class.hidden]="phase() !== 'running'" aria-label="Camera with jewelry"></canvas>

      <header class="top">
        <a class="round" routerLink="/" aria-label="Back to designs">‹</a>
        <div class="title"><span class="serif">{{ product()?.name ?? 'Try-On' }}</span></div>
        <button type="button" class="round" (click)="flip()" [disabled]="phase() !== 'running'" aria-label="Switch camera">⟲</button>
      </header>

      @switch (phase()) {
        @case ('loading') { <div class="panel"><div class="spinner" aria-hidden="true"></div><p>Opening the camera. Allow it when your phone asks.</p></div> }
        @case ('missing') { <div class="panel"><p>This design is not available.</p><a class="btn" routerLink="/">See all designs</a></div> }
        @case ('insecure') { <div class="panel"><p>The camera only works on a secure (https) link.</p></div> }
        @case ('denied') { <div class="panel"><p>Camera access is blocked. Allow the camera for this site in your browser settings, then try again.</p><button type="button" class="btn" (click)="restart()">Try again</button></div> }
        @case ('nocamera') { <div class="panel"><p>No camera was found on this device.</p><a class="btn" routerLink="/">Back to designs</a></div> }
        @case ('busy') { <div class="panel"><p>The camera is in use by another app. Close it and try again.</p><button type="button" class="btn" (click)="restart()">Try again</button></div> }
        @case ('modelerror') { <div class="panel"><p>The try-on could not load. Check your internet connection and try again.</p><button type="button" class="btn" (click)="restart()">Try again</button></div> }
        @case ('error') { <div class="panel"><p>Something went wrong with the camera.</p><button type="button" class="btn" (click)="restart()">Try again</button></div> }
        @case ('running') { @if (searching()) { <div class="hint" role="status">Face the camera, with your ears and neck in view</div> } }
      }

      <section class="dock">
        @if (product(); as p) {
          @if (p.kinds.length > 1) {
            <div class="parts" role="group" aria-label="Pieces to wear">
              @for (k of p.kinds; track k) {
                <button type="button" class="pchip" [class.on]="parts().includes(k)" [attr.aria-pressed]="parts().includes(k)" (click)="togglePart(k)">{{ label(k) }}</button>
              }
            </div>
          }
        }
        <div class="controls">
          <div class="strip" aria-label="Other designs">
            @for (o of catalog.products(); track o.id) {
              <button type="button" class="thumb" [class.on]="o.id === product()?.id" (click)="switchTo(o)" [attr.aria-label]="'Wear ' + o.name">
                <img [src]="catalog.url(o.card)" alt="" />
              </button>
            }
          </div>
          <button type="button" class="shutter" (click)="snapshot()" [disabled]="phase() !== 'running'" aria-label="Take a photo"></button>
        </div>
      </section>
    </main>

    @if (photoUrl(); as url) {
      <div class="backdrop">
        <div class="dialog" role="dialog" aria-modal="true" aria-label="Your photo">
          <img [src]="url" alt="Your try-on photo" />
          <div class="buttons">
            <a class="btn" [href]="url" [download]="photoName">Save photo</a>
            @if (canShare()) { <button type="button" class="btn ghost" (click)="share()">Share</button> }
            <button type="button" class="btn ghost" (click)="closePhoto()">Close</button>
          </div>
        </div>
      </div>
    }
  `,
  styles: [`
    :host { display: block; height: 100vh; height: 100dvh; background: var(--night); color: #fff; overflow: hidden; }
    .stage { position: relative; height: 100%; display: grid; place-items: center; }
    video { position: absolute; width: 1px; height: 1px; opacity: 0; pointer-events: none; }
    canvas { position: absolute; inset: 0; width: 100%; height: 100%; object-fit: cover; }
    canvas.hidden { visibility: hidden; }
    .top { position: absolute; top: 0; left: 0; right: 0; display: flex; align-items: center; gap: 10px; padding: calc(10px + env(safe-area-inset-top)) 12px 18px; background: linear-gradient(rgba(0,0,0,0.55), transparent); z-index: 2; }
    .title { flex: 1; min-width: 0; text-align: center; font-size: 20px; font-weight: 600; white-space: nowrap; overflow: hidden; text-overflow: ellipsis; }
    .round { width: 42px; height: 42px; flex: none; display: grid; place-items: center; border: 0; border-radius: 50%; background: rgba(255,255,255,0.16); color: #fff; font-size: 24px; line-height: 1; text-decoration: none; backdrop-filter: blur(6px); }
    .round:disabled { opacity: 0.4; }
    .panel { position: relative; z-index: 3; max-width: 320px; margin: 16px; padding: 22px; border-radius: var(--radius); background: var(--ivory); color: var(--ink); text-align: center; display: grid; gap: 14px; justify-items: center; }
    .panel p { margin: 0; line-height: 1.5; }
    .spinner { width: 34px; height: 34px; border-radius: 50%; border: 4px solid var(--gold-soft); border-top-color: var(--maroon); animation: spin 1s linear infinite; }
    @keyframes spin { to { transform: rotate(360deg); } }
    .hint { position: absolute; top: calc(70px + env(safe-area-inset-top)); left: 50%; transform: translateX(-50%); z-index: 2; max-width: 90%; padding: 8px 14px; border-radius: 999px; background: rgba(0,0,0,0.6); font-size: 13px; font-weight: 500; text-align: center; }
    .dock { position: absolute; left: 0; right: 0; bottom: 0; z-index: 2; padding: 26px 0 calc(14px + env(safe-area-inset-bottom)); background: linear-gradient(transparent, rgba(0,0,0,0.65) 35%); }
    .parts { display: flex; justify-content: center; gap: 8px; padding: 0 12px 12px; flex-wrap: wrap; }
    .pchip { min-height: 34px; padding: 0 14px; border-radius: 999px; border: 1px solid rgba(255,255,255,0.5); background: rgba(0,0,0,0.25); color: #fff; font-size: 13px; font-weight: 500; }
    .pchip.on { background: var(--gold); border-color: var(--gold); }
    .controls { position: relative; display: flex; align-items: center; height: 84px; }
    .strip { display: flex; gap: 10px; overflow-x: auto; padding: 0 16px 0 calc(50% + 50px); scrollbar-width: none; align-items: center; height: 100%; width: 100%; }
    .strip::-webkit-scrollbar { display: none; }
    .thumb { flex: none; width: 56px; height: 56px; padding: 4px; border-radius: 14px; border: 2px solid transparent; background: rgba(255,253,249,0.92); }
    .thumb.on { border-color: var(--gold); }
    .thumb img { width: 100%; height: 100%; object-fit: contain; }
    .shutter { position: absolute; left: 50%; top: 50%; transform: translate(-50%, -50%); width: 74px; height: 74px; border-radius: 50%; border: 5px solid #fff; background: var(--maroon); box-shadow: 0 4px 18px rgba(0,0,0,0.4); }
    .shutter:active { transform: translate(-50%, -50%) scale(0.94); }
    .shutter:disabled { opacity: 0.5; }
    .backdrop { position: fixed; inset: 0; z-index: 10; display: grid; place-items: center; padding: 16px; background: rgba(10,6,5,0.85); }
    .dialog { width: 100%; max-width: 420px; padding: 14px; border-radius: var(--radius); background: var(--ivory); display: grid; gap: 12px; }
    .dialog img { width: 100%; max-height: 65vh; object-fit: contain; border-radius: 10px; }
    .buttons { display: flex; gap: 8px; flex-wrap: wrap; justify-content: center; }
    .buttons .btn { min-height: 44px; flex: 1; }
  `]
})
export class TryonPage implements OnDestroy {
  readonly catalog = inject(CatalogService);
  private readonly route = inject(ActivatedRoute);
  private readonly router = inject(Router);
  private readonly zone = inject(NgZone);
  private readonly videoRef = viewChild.required<ElementRef<HTMLVideoElement>>('video');
  private readonly canvasRef = viewChild.required<ElementRef<HTMLCanvasElement>>('canvas');

  private readonly productId = signal('');
  readonly product = computed(() => this.catalog.products().find(p => p.id === this.productId()));
  readonly parts = signal<PieceKind[]>([]);
  readonly phase = signal<Phase>('loading');
  readonly searching = signal(false);
  readonly facing = signal<Facing>('user');
  readonly photoUrl = signal<string | null>(null);
  readonly canShare = signal(false);
  photoName = 'nbjewel-tryon.jpg';

  private stream: MediaStream | null = null;
  private tracker: LandmarkTracker | null = null;
  private images: Images = {};
  private imagesFor = '';
  private photoBlob: Blob | null = null;
  private readonly smoother = new LandmarkSmoother();
  private readonly swings = { necklace: new Swing(NECKLACE), earringLeft: new Swing(EARRING), earringRight: new Swing(EARRING), tikka: new Swing(TIKKA) };
  private frame = 0;
  private lastVideoTime = -1;
  private lastFrameAt = 0;
  private lastSeenAt = 0;
  private landmarks: Point[] | null = null;
  private destroyed = false;
  private run = 0;
  private cameraStarted = false;

  private readonly partsQuery = signal<string | null>(null);

  constructor() {
    this.catalog.load();
    combineLatest([this.route.paramMap, this.route.queryParamMap]).pipe(takeUntilDestroyed()).subscribe(([params, query]) => {
      this.productId.set(params.get('id') ?? '');
      this.partsQuery.set(query.get('parts'));
    });
    // Runs when the catalog arrives, the design changes, or the chosen pieces change.
    effect(() => {
      this.catalog.state();
      this.productId();
      this.partsQuery();
      untracked(() => this.applyProduct());
    });
  }

  ngOnDestroy(): void {
    this.destroyed = true;
    this.stopCamera();
    this.tracker?.close();
    this.tracker = null;
    this.closePhoto();
  }

  label(kind: PieceKind): string { return KIND_LABELS[kind]; }

  togglePart(kind: PieceKind): void {
    const now = this.parts();
    const next = now.includes(kind) ? now.filter(k => k !== kind) : [...now, kind];
    if (!next.length) return; // keep at least one piece on
    this.parts.set(next);
    void this.router.navigate([], { relativeTo: this.route, queryParams: { parts: next.join(',') }, replaceUrl: true });
  }

  switchTo(product: Product): void {
    if (product.id === this.productId()) return;
    void this.router.navigate(['/try', product.id], { replaceUrl: true });
  }

  restart(): void { void this.startCamera(); }

  async flip(): Promise<void> {
    this.facing.set(this.facing() === 'user' ? 'environment' : 'user');
    this.resetMotion();
    await this.startCamera();
  }

  // Called when the route or catalog changes: picks the pieces, loads their images, and starts
  // the camera the first time. Switching designs keeps the camera running.
  private applyProduct(): void {
    if (this.catalog.state().status === 'loading') return;
    const product = this.product();
    if (!product) { this.stopCamera(); this.phase.set('missing'); return; }
    this.parts.set(parseParts(this.partsQuery(), product));
    if (this.imagesFor !== product.id) {
      this.imagesFor = product.id;
      void this.loadImages(product);
      this.resetMotion();
    }
    if (!this.cameraStarted || this.phase() === 'missing') { this.cameraStarted = true; void this.startCamera(); }
  }

  private async loadImages(product: Product): Promise<void> {
    const load = (path?: string) => path ? new Promise<HTMLImageElement | undefined>(resolve => {
      const img = new Image();
      img.onload = () => resolve(img);
      img.onerror = () => resolve(undefined);
      img.src = this.catalog.url(path);
    }) : Promise.resolve(undefined);
    const p = product.parts;
    const [necklace, earringLeft, earringRight, tikka] = await Promise.all([load(p.necklace?.image), load(p.earringLeft?.image), load(p.earringRight?.image), load(p.tikka?.image)]);
    if (this.imagesFor === product.id) this.images = { necklace, earringLeft, earringRight, tikka };
  }

  private async startCamera(): Promise<void> {
    if (this.destroyed) return;
    const run = ++this.run;
    this.stopCamera();
    this.phase.set('loading');
    this.searching.set(false);
    if (!window.isSecureContext) { this.phase.set('insecure'); return; }
    if (!navigator.mediaDevices?.getUserMedia) { this.phase.set('nocamera'); return; }
    let stream: MediaStream | null = null;
    const camera = navigator.mediaDevices.getUserMedia({ audio: false, video: { facingMode: this.facing(), width: { ideal: 1280 }, height: { ideal: 720 } } })
      .then(s => { stream = s; return s; }, (error: unknown) => { throw new CameraError(error); });
    try {
      await Promise.all([camera, this.loadTracker(run)]);
      if (run !== this.run || this.destroyed || !stream) { stopStream(stream); return; }
      this.stream = stream;
      const video = this.videoRef().nativeElement;
      video.srcObject = stream;
      await video.play();
      if (run !== this.run || this.destroyed) return;
      this.lastVideoTime = -1;
      this.lastSeenAt = performance.now();
      this.landmarks = null;
      this.resetMotion();
      this.phase.set('running');
      this.zone.runOutsideAngular(() => { this.frame = requestAnimationFrame(() => this.loop(run)); });
    } catch (error) {
      camera.then(s => { if (this.stream !== s) stopStream(s); }, () => undefined);
      if (stream !== this.stream) stopStream(stream);
      if (run !== this.run || this.destroyed) return;
      this.stopCamera();
      this.phase.set(phaseFor(error));
    }
  }

  private async loadTracker(run: number): Promise<void> {
    if (this.tracker) return;
    let tracker: LandmarkTracker;
    try { tracker = await createTracker('face', forceCpu()); } catch { throw new ModelLoadError(); }
    if (this.destroyed || run !== this.run || this.tracker) { tracker.close(); return; }
    this.tracker = tracker;
  }

  // Once per new camera frame: draw the frame, find the face in that drawn frame, then draw the
  // jewelry, so the jewelry never trails behind the face.
  private loop(run: number): void {
    if (run !== this.run || this.destroyed) return;
    const video = this.videoRef().nativeElement;
    const canvas = this.canvasRef().nativeElement;
    const context = canvas.getContext('2d');
    if (context && this.tracker && video.readyState >= 2 && video.videoWidth > 0 && video.currentTime !== this.lastVideoTime) {
      this.lastVideoTime = video.currentTime;
      const now = performance.now();
      const dt = this.lastFrameAt ? (now - this.lastFrameAt) / 1000 : 0;
      this.lastFrameAt = now;
      if (canvas.width !== video.videoWidth || canvas.height !== video.videoHeight) {
        canvas.width = video.videoWidth;
        canvas.height = video.videoHeight;
      }
      const mirror = this.facing() === 'user';
      context.save();
      if (mirror) { context.translate(canvas.width, 0); context.scale(-1, 1); }
      context.drawImage(video, 0, 0, canvas.width, canvas.height);
      context.restore();
      // Detect on the canvas just drawn (already mirrored), so the points belong to exactly the
      // frame on screen. Reading the video again could give a newer frame on a slow phone.
      let found: Point[] | null = null;
      try { found = this.tracker.detect(canvas, now); } catch { found = null; }
      if (found) {
        this.lastSeenAt = now;
        this.landmarks = this.smoother.smooth(found.map(p => toPixels(p, canvas.width, canvas.height, false)));
      } else if (this.landmarks && now - this.lastSeenAt > HIDE_AFTER_MS) {
        this.landmarks = null;
        this.resetMotion();
      }
      const searching = now - this.lastSeenAt > HINT_AFTER_MS;
      if (searching !== this.searching()) this.zone.run(() => this.searching.set(searching));
      if (this.landmarks) this.drawJewelry(context, this.landmarks, dt);
    }
    this.frame = requestAnimationFrame(() => this.loop(run));
  }

  private drawJewelry(context: CanvasRenderingContext2D, face: Point[], dt: number): void {
    const product = this.product();
    if (!product) return;
    const parts = this.parts();
    const ppm = faceFrame(face).pixelsPerMeter;
    const img = this.images;
    const p = product.parts;
    if (parts.includes('necklace') && p.necklace && img.necklace) {
      const place = necklacePlacement(face, p.necklace.aspect, p.necklace.fit);
      const swing = this.swings.necklace.update(place, dt, ppm);
      draw(context, img.necklace, { ...place, rotation: place.rotation + swing });
    }
    if (parts.includes('tikka') && p.tikka && img.tikka) {
      const place = tikkaPlacement(face, p.tikka.aspect, p.tikka.fit);
      draw(context, img.tikka, { ...place, rotation: this.swings.tikka.update(place, dt, ppm) });
    }
    if (parts.includes('earrings') && p.earringLeft && p.earringRight && img.earringLeft && img.earringRight) {
      const { left, right } = earringPlacements(face, p.earringLeft.aspect, p.earringRight.aspect, p.earringLeft.fit);
      draw(context, img.earringLeft, { ...left, rotation: this.swings.earringLeft.update(left, dt, ppm) });
      draw(context, img.earringRight, { ...right, rotation: this.swings.earringRight.update(right, dt, ppm) });
    }
  }

  snapshot(): void {
    const canvas = this.canvasRef().nativeElement;
    canvas.toBlob(blob => this.zone.run(() => {
      if (!blob) return;
      this.closePhoto();
      this.photoName = `nbjewel-tryon-${new Date().toISOString().slice(0, 19).replace(/[:T]/g, '-')}.jpg`;
      this.photoBlob = blob;
      this.photoUrl.set(URL.createObjectURL(blob));
      const file = new File([blob], this.photoName, { type: 'image/jpeg' });
      this.canShare.set(typeof navigator.canShare === 'function' && navigator.canShare({ files: [file] }));
    }), 'image/jpeg', 0.92);
  }

  async share(): Promise<void> {
    if (!this.photoBlob) return;
    try { await navigator.share({ files: [new File([this.photoBlob], this.photoName, { type: 'image/jpeg' })], title: 'My NB Jewel try-on' }); }
    catch { /* the share sheet was closed */ }
  }

  closePhoto(): void {
    const url = this.photoUrl();
    if (url) URL.revokeObjectURL(url);
    this.photoUrl.set(null);
    this.photoBlob = null;
  }

  private resetMotion(): void {
    this.smoother.reset();
    this.lastFrameAt = 0;
    Object.values(this.swings).forEach(s => s.reset());
  }

  private stopCamera(): void {
    cancelAnimationFrame(this.frame);
    stopStream(this.stream);
    this.stream = null;
  }
}

function draw(context: CanvasRenderingContext2D, image: HTMLImageElement, p: Placement): void {
  if (p.opacity <= 0.01 || image.naturalWidth === 0) return;
  context.save();
  context.globalAlpha = p.opacity;
  context.translate(p.x, p.y);
  context.rotate(p.rotation);
  context.drawImage(image, -p.anchorX * p.width, -p.anchorY * p.height, p.width, p.height);
  context.restore();
}

// "?cpu=1" in the address forces the CPU path, for testing on a phone or browser whose GPU path
// misbehaves.
function forceCpu(): boolean {
  try { return new URLSearchParams(location.search).get('cpu') === '1'; } catch { return false; }
}

function stopStream(stream: MediaStream | null): void { stream?.getTracks().forEach(t => t.stop()); }

function phaseFor(error: unknown): Phase {
  if (error instanceof ModelLoadError) return 'modelerror';
  const name = error instanceof CameraError && error.cause instanceof DOMException ? error.cause.name : '';
  if (name === 'NotAllowedError' || name === 'SecurityError') return 'denied';
  if (name === 'NotFoundError' || name === 'OverconstrainedError') return 'nocamera';
  if (name === 'NotReadableError' || name === 'AbortError') return 'busy';
  return 'error';
}

class CameraError extends Error { constructor(override readonly cause: unknown) { super('camera'); } }
class ModelLoadError extends Error {}
