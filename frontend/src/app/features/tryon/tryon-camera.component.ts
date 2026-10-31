// Role: The camera try-on page (/tryon/:id). Opens the phone camera (front by default, with a
// flip button), finds the face (earrings, necklaces) or hand (rings) with MediaPipe in the
// browser, smooths the landmarks, and draws the jewelry image on top of the video in one canvas.
// The snapshot button saves that canvas (camera frame plus jewelry) as a JPG. No video leaves
// the phone. Shows friendly messages for no HTTPS, camera denied, no camera, camera busy,
// model load failure, and "no face or hand found".

import { Component, ElementRef, NgZone, OnDestroy, computed, inject, signal, viewChild } from '@angular/core';
import { takeUntilDestroyed } from '@angular/core/rxjs-interop';
import { ActivatedRoute, RouterLink } from '@angular/router';
import { switchMap } from 'rxjs';
import { JewelryItem } from './jewelry.models';
import { JewelryService } from './jewelry.service';
import { LandmarkTracker, createTracker } from './landmark-tracker';
import { LandmarkSmoother, Placement, TUNING, earringPlacements, necklacePlacement, ringPlacement, toPixels } from './overlay-math';

type Phase = 'loading' | 'running' | 'missing' | 'insecure' | 'denied' | 'nocamera' | 'busy' | 'modelerror' | 'imageerror' | 'error';
type Facing = 'user' | 'environment';

// Frames without a face or hand before the hint shows (about half a second at 30 fps).
const LOST_FRAMES = 15;

@Component({
  selector: 'nb-tryon-camera', standalone: true, imports: [RouterLink],
  template: `
    <header class="brand-bar"><a class="back" routerLink="/tryon" aria-label="Back to jewelry">‹</a><span class="title">{{ item()?.name ?? 'Try-On' }}</span></header>
    <main class="stage">
      <video #video playsinline muted autoplay aria-hidden="true"></video>
      <canvas #canvas [class.hidden]="phase() !== 'running'" aria-label="Camera with jewelry"></canvas>

      @switch (phase()) {
        @case ('loading') { <div class="panel"><div class="spinner" aria-hidden="true"></div><p>Starting the camera and loading the try-on. Allow the camera when asked.</p></div> }
        @case ('missing') { <div class="panel"><p>This piece is not available any more.</p><a class="btn" routerLink="/tryon">Back to jewelry</a></div> }
        @case ('insecure') { <div class="panel"><p>The camera only works on a secure (https) link. Open the https address of this page.</p></div> }
        @case ('denied') { <div class="panel"><p>Camera access is blocked. Allow the camera for this site in your browser settings, then try again.</p><button type="button" class="btn" (click)="restart()">Try again</button></div> }
        @case ('nocamera') { <div class="panel"><p>No camera was found on this device.</p><a class="btn" routerLink="/tryon">Back to jewelry</a></div> }
        @case ('busy') { <div class="panel"><p>The camera is in use by another app. Close it and try again.</p><button type="button" class="btn" (click)="restart()">Try again</button></div> }
        @case ('modelerror') { <div class="panel"><p>The try-on could not load. Check your internet connection and try again.</p><button type="button" class="btn" (click)="restart()">Try again</button></div> }
        @case ('imageerror') { <div class="panel"><p>The jewelry picture could not load. Try again in a moment.</p><button type="button" class="btn" (click)="restart()">Try again</button></div> }
        @case ('error') { <div class="panel"><p>Something went wrong with the camera. Try again.</p><button type="button" class="btn" (click)="restart()">Try again</button></div> }
        @case ('running') {
          @if (searching()) { <div class="hint" role="status">{{ hint() }}</div> }
        }
      }
    </main>
    <footer class="controls">
      <button type="button" class="round" (click)="flip()" [disabled]="phase() !== 'running'" aria-label="Switch camera">⟲</button>
      <button type="button" class="shutter" (click)="snapshot()" [disabled]="phase() !== 'running'" aria-label="Take a photo"></button>
      <a class="round" routerLink="/tryon" aria-label="Choose another piece">▦</a>
    </footer>

    @if (photoUrl(); as url) {
      <div class="backdrop">
        <div class="dialog" role="dialog" aria-modal="true" aria-label="Your photo">
          <img [src]="url" alt="Your try-on photo" />
          <div class="buttons">
            <a class="save" [href]="url" [download]="photoName">Save photo</a>
            @if (canShare()) { <button type="button" class="cancel" (click)="share()">Share</button> }
            <button type="button" class="cancel" (click)="closePhoto()">Close</button>
          </div>
        </div>
      </div>
    }
  `,
  styles: [`
    :host { display:flex; flex-direction:column; height:100vh; height:100dvh; background:#111; overflow:hidden; }
    .brand-bar { flex:none; height:52px; display:flex; align-items:center; gap:14px; padding:0 16px; color:white; background:var(--sidebar); }
    .back { color:white; text-decoration:none; font-size:30px; line-height:1; }
    .title { font-weight:700; overflow:hidden; text-overflow:ellipsis; white-space:nowrap; }
    .stage { position:relative; flex:1; min-height:0; display:grid; place-items:center; }
    video { position:absolute; width:1px; height:1px; opacity:0; pointer-events:none; }
    canvas { width:100%; height:100%; object-fit:cover; display:block; }
    canvas.hidden { visibility:hidden; position:absolute; }
    .panel { position:absolute; max-width:320px; margin:16px; padding:20px; border-radius:8px; background:white; color:var(--ink); text-align:center; display:grid; gap:14px; justify-items:center; }
    .panel p { margin:0; line-height:1.5; }
    .hint { position:absolute; top:14px; left:50%; transform:translateX(-50%); max-width:90%; padding:8px 14px; border-radius:18px; background:#000a; color:white; font-weight:600; font-size:13px; text-align:center; }
    .btn, .save { display:inline-grid; place-items:center; min-height:38px; padding:0 20px; border:0; border-radius:6px; background:var(--button); color:#333; font-weight:700; text-decoration:none; }
    .cancel { min-height:38px; padding:0 20px; border:1px solid var(--gold); border-radius:6px; background:white; color:var(--gold); font-weight:700; }
    .spinner { width:32px; height:32px; border-radius:50%; border:4px solid var(--pale); border-top-color:var(--gold); animation:spin 1s linear infinite; }
    @keyframes spin { to { transform:rotate(360deg); } }
    .controls { flex:none; height:96px; display:flex; align-items:center; justify-content:space-around; padding-bottom:env(safe-area-inset-bottom); background:#111; }
    .round { width:48px; height:48px; display:grid; place-items:center; border:0; border-radius:50%; background:#fff2; color:white; font-size:22px; text-decoration:none; }
    .shutter { width:68px; height:68px; border-radius:50%; border:5px solid white; background:var(--button); }
    button:disabled { opacity:.4; cursor:default; }
    .backdrop { position:fixed; inset:0; z-index:60; display:grid; place-items:center; padding:16px; background:#000c; }
    .dialog { width:100%; max-width:420px; padding:14px; border-radius:8px; background:white; display:grid; gap:12px; }
    .dialog img { width:100%; max-height:65vh; object-fit:contain; border-radius:6px; }
    .buttons { display:flex; gap:10px; flex-wrap:wrap; justify-content:center; }
  `]
})
export class TryonCameraComponent implements OnDestroy {
  readonly jewelry = inject(JewelryService);
  private readonly route = inject(ActivatedRoute);
  private readonly zone = inject(NgZone);
  private readonly videoRef = viewChild.required<ElementRef<HTMLVideoElement>>('video');
  private readonly canvasRef = viewChild.required<ElementRef<HTMLCanvasElement>>('canvas');

  readonly item = signal<JewelryItem | null>(null);
  readonly phase = signal<Phase>('loading');
  readonly searching = signal(false);
  readonly facing = signal<Facing>('user');
  readonly photoUrl = signal<string | null>(null);
  readonly photoBlob = signal<Blob | null>(null);
  readonly hint = computed(() => this.item()?.type === 'ring'
    ? 'Show the back of your hand, fingers up'
    : 'Move your face into the frame');
  readonly canShare = signal(false);
  photoName = 'nbjewel-tryon.jpg';

  private stream: MediaStream | null = null;
  private tracker: LandmarkTracker | null = null;
  private trackerKind: 'face' | 'hand' | null = null;
  private jewelImage: HTMLImageElement | null = null;
  private readonly smoother = new LandmarkSmoother(TUNING.smoothing, TUNING.fastMovePx);
  private frame = 0;
  private lastVideoTime = -1;
  private lostFrames = 0;
  private lastLandmarks: { x: number; y: number }[] | null = null;
  private destroyed = false;
  private run = 0; // bumps on every (re)start, so stale async work stops itself

  constructor() {
    this.route.paramMap.pipe(switchMap(params => this.jewelry.getActive(params.get('id') ?? '')), takeUntilDestroyed()).subscribe({
      next: item => {
        this.item.set(item ?? null);
        if (!item) { this.stopAll(); this.phase.set('missing'); return; }
        void this.start();
      },
      error: () => this.phase.set('error')
    });
  }

  ngOnDestroy(): void {
    this.destroyed = true;
    this.stopAll();
    this.tracker?.close();
    this.tracker = null;
    this.closePhoto();
  }

  restart(): void { void this.start(); }

  async flip(): Promise<void> {
    this.facing.set(this.facing() === 'user' ? 'environment' : 'user');
    this.smoother.reset();
    await this.start();
  }

  private async start(): Promise<void> {
    const item = this.item();
    if (!item || this.destroyed) return;
    const run = ++this.run;
    this.stopAll();
    this.phase.set('loading');
    this.searching.set(false);
    if (!window.isSecureContext) { this.phase.set('insecure'); return; }
    if (!navigator.mediaDevices?.getUserMedia) { this.phase.set('nocamera'); return; }

    const kind = item.type === 'ring' ? 'hand' : 'face';
    try {
      const [stream] = await Promise.all([
        this.openCamera(),
        this.loadImage(item).then(img => { this.jewelImage = img; }),
        this.loadTracker(kind)
      ]);
      if (run !== this.run || this.destroyed) { stream.getTracks().forEach(t => t.stop()); return; }
      this.stream = stream;
      const video = this.videoRef().nativeElement;
      video.srcObject = stream;
      await video.play();
      this.lastVideoTime = -1;
      this.lostFrames = 0;
      this.lastLandmarks = null;
      this.smoother.reset();
      this.phase.set('running');
      this.zone.runOutsideAngular(() => { this.frame = requestAnimationFrame(() => this.loop(run)); });
    } catch (error) {
      if (run !== this.run) return;
      this.stopAll();
      this.phase.set(this.phaseFor(error));
    }
  }

  private async openCamera(): Promise<MediaStream> {
    try {
      return await navigator.mediaDevices.getUserMedia({
        audio: false,
        video: { facingMode: this.facing(), width: { ideal: 1280 }, height: { ideal: 720 } }
      });
    } catch (error) {
      throw new CameraError(error);
    }
  }

  private loadImage(item: JewelryItem): Promise<HTMLImageElement> {
    return new Promise((resolve, reject) => {
      const img = new Image();
      img.crossOrigin = 'anonymous'; // needed so the snapshot canvas can be saved
      img.onload = () => resolve(img);
      img.onerror = () => reject(new ImageLoadError());
      img.src = this.jewelry.imageSrc(item);
    });
  }

  private async loadTracker(kind: 'face' | 'hand'): Promise<void> {
    if (this.tracker && this.trackerKind === kind) return;
    this.tracker?.close();
    this.tracker = null;
    try {
      this.tracker = await createTracker(kind);
      this.trackerKind = kind;
    } catch {
      throw new ModelLoadError();
    }
  }

  private phaseFor(error: unknown): Phase {
    if (error instanceof ImageLoadError) return 'imageerror';
    if (error instanceof ModelLoadError) return 'modelerror';
    const name = error instanceof CameraError && error.cause instanceof DOMException ? error.cause.name : '';
    if (name === 'NotAllowedError' || name === 'SecurityError') return 'denied';
    if (name === 'NotFoundError' || name === 'OverconstrainedError' || name === 'DevicesNotFoundError') return 'nocamera';
    if (name === 'NotReadableError' || name === 'AbortError') return 'busy';
    return 'error';
  }

  // Runs outside Angular change detection, once per screen refresh. Work happens only when the
  // camera has a new frame. Order matters: the frame is drawn first and detection runs right
  // after on that same frame, so the jewelry is drawn on the frame it was found in. (Drawing
  // after a slow detection would show a newer frame and the jewelry would trail behind.)
  private loop(run: number): void {
    if (run !== this.run || this.destroyed) return;
    const video = this.videoRef().nativeElement;
    const canvas = this.canvasRef().nativeElement;
    const context = canvas.getContext('2d');
    if (context && this.tracker && video.readyState >= 2 && video.videoWidth > 0 && video.currentTime !== this.lastVideoTime) {
      this.lastVideoTime = video.currentTime;
      if (canvas.width !== video.videoWidth || canvas.height !== video.videoHeight) {
        canvas.width = video.videoWidth;
        canvas.height = video.videoHeight;
      }
      const mirror = this.facing() === 'user';
      this.drawFrame(context, video, canvas, mirror);
      let found: { x: number; y: number }[] | null = null;
      try { found = this.tracker.detect(video, performance.now()); } catch { found = null; }
      if (found) {
        this.lostFrames = 0;
        this.lastLandmarks = this.smoother.smooth(found.map(p => toPixels(p, canvas.width, canvas.height, mirror)));
      } else if (++this.lostFrames >= LOST_FRAMES) {
        this.lastLandmarks = null;
        this.smoother.reset();
      }
      const searching = this.lostFrames >= LOST_FRAMES;
      if (searching !== this.searching()) this.zone.run(() => this.searching.set(searching));
      this.drawJewelry(context);
    }
    this.frame = requestAnimationFrame(() => this.loop(run));
  }

  private drawFrame(context: CanvasRenderingContext2D, video: HTMLVideoElement, canvas: HTMLCanvasElement, mirror: boolean): void {
    context.save();
    if (mirror) { context.translate(canvas.width, 0); context.scale(-1, 1); }
    context.drawImage(video, 0, 0, canvas.width, canvas.height);
    context.restore();
  }

  private drawJewelry(context: CanvasRenderingContext2D): void {
    const points = this.lastLandmarks;
    const image = this.jewelImage;
    const item = this.item();
    if (!points || !image || !item || image.naturalWidth === 0) return;
    const aspect = image.naturalHeight / image.naturalWidth;
    const placements: Placement[] = item.type === 'earring' ? earringPlacements(points, aspect)
      : item.type === 'necklace' ? [necklacePlacement(points, aspect)]
      : [ringPlacement(points, aspect)];
    for (const p of placements) {
      if (p.opacity <= 0.01) continue;
      context.save();
      context.globalAlpha = p.opacity;
      context.translate(p.x, p.y);
      context.rotate(p.rotation);
      context.drawImage(image, -p.anchorX * p.width, -p.anchorY * p.height, p.width, p.height);
      context.restore();
    }
  }

  snapshot(): void {
    const canvas = this.canvasRef().nativeElement;
    try {
      canvas.toBlob(blob => this.zone.run(() => {
        if (!blob) return;
        this.closePhoto();
        const stamp = new Date().toISOString().slice(0, 19).replace(/[:T]/g, '-');
        this.photoName = `nbjewel-tryon-${stamp}.jpg`;
        this.photoBlob.set(blob);
        this.photoUrl.set(URL.createObjectURL(blob));
        const file = new File([blob], this.photoName, { type: 'image/jpeg' });
        this.canShare.set(typeof navigator.canShare === 'function' && navigator.canShare({ files: [file] }));
      }), 'image/jpeg', 0.92);
    } catch {
      this.phase.set('imageerror'); // a tainted canvas means the picture was not served with CORS
    }
  }

  async share(): Promise<void> {
    const blob = this.photoBlob();
    if (!blob) return;
    try { await navigator.share({ files: [new File([blob], this.photoName, { type: 'image/jpeg' })], title: 'My NB Jewel try-on' }); }
    catch { /* the user closed the share sheet */ }
  }

  closePhoto(): void {
    const url = this.photoUrl();
    if (url) URL.revokeObjectURL(url);
    this.photoUrl.set(null);
    this.photoBlob.set(null);
  }

  private stopAll(): void {
    cancelAnimationFrame(this.frame);
    this.stream?.getTracks().forEach(t => t.stop());
    this.stream = null;
  }
}

class CameraError extends Error { constructor(override readonly cause: unknown) { super('camera'); } }
class ImageLoadError extends Error {}
class ModelLoadError extends Error {}
