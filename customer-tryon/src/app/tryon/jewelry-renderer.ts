// Role: Draws the chosen pieces of a design on the camera canvas with the 3D effects:
// - 3D swing: each hanging piece has a side to side pendulum and a forward and back pendulum
//   (driven by the head moving toward or away from the camera and by head turns), at real scale.
// - Head turn: earrings narrow as the head turns; the necklace narrows less (the neck turns less).
// - Hiding: earrings are cut where they pass behind the face outline, so the far earring goes
//   behind the cheek when the head turns. The necklace ends fade where the chain goes behind the neck.
// - Light: pieces dim in a dark room and pick up a little of the room colour, measured on the skin.
// - Shadow: a soft shadow under each piece, sized to the face.
// - Depth: sizes follow the face size, so pieces grow and shrink as the customer moves.
// Uses the pure helpers in overlay-math.ts, physics.ts and pose.ts.

import { PieceKind, Product } from '../catalog/catalog.models';
import { Placement, Point, earringPlacements, faceFrame, necklacePlacement, tikkaPlacement } from './overlay-math';
import { EARRING, NECKLACE, Swing, TIKKA } from './physics';
import { Light, Point3, blendLight, depthMeters, facePolygon, headPose, lightFromPixels, turnScale } from './pose';

export interface PieceImages { necklace?: HTMLImageElement; earringLeft?: HTMLImageElement; earringRight?: HTMLImageElement; tikka?: HTMLImageElement }

type Key = 'necklace' | 'earringLeft' | 'earringRight' | 'tikka';

interface Draw { image: HTMLImageElement; place: Placement; scaleX: number; scaleY: number; tuck?: boolean; behindFace?: boolean }

const LIGHT_EVERY = 6; // measure the room light every few frames

export class JewelryRenderer {
  private readonly side: Record<Key, Swing> = { necklace: new Swing(NECKLACE), earringLeft: new Swing(EARRING), earringRight: new Swing(EARRING), tikka: new Swing(TIKKA) };
  private readonly depth: Record<Key, Swing> = { necklace: new Swing(NECKLACE), earringLeft: new Swing(EARRING), earringRight: new Swing(EARRING), tikka: new Swing(TIKKA) };
  private readonly layers = new Map<string, HTMLCanvasElement>(); // one small canvas per piece image
  private light: Light = { brightness: 1, tint: { r: 255, g: 255, b: 255 } };
  private frames = 0;

  reset(): void {
    Object.values(this.side).forEach(s => s.reset());
    Object.values(this.depth).forEach(s => s.reset());
    this.frames = 0;
  }

  render(context: CanvasRenderingContext2D, face: Point3[], product: Product, parts: PieceKind[], images: PieceImages, dt: number): void {
    const frame = faceFrame(face);
    const pose = headPose(face);
    const ppm = frame.pixelsPerMeter;
    const depth = depthMeters(frame.faceWidth, context.canvas.width);
    if (this.frames++ % LIGHT_EVERY === 0) this.measureLight(context, face[1], frame.faceWidth);

    // Pivot in metres for the forward and back swing: depth of the face plus the point's own depth.
    const zPivot = (p: Point, z: number): Point => ({ x: depth + z / ppm, y: p.y / ppm });
    const swing = (key: Key, place: Placement, z: number) => ({
      side: this.side[key].update(place, dt, ppm),
      tilt: this.depth[key].update(zPivot(place, z), dt, 1) // metres in, so 1 pixel per metre
    });

    const draws: Draw[] = [];
    const p = product.parts;
    if (parts.includes('necklace') && p.necklace && images.necklace) {
      const place = necklacePlacement(face, p.necklace.aspect, p.necklace.fit, p.necklace.neckline);
      const s = swing('necklace', place, face[152].z);
      draws.push({ image: images.necklace, place: { ...place, rotation: place.rotation + s.side }, scaleX: turnScale(pose.yaw * 0.5, 0.6), scaleY: Math.cos(s.tilt), tuck: true });
    }
    if (parts.includes('tikka') && p.tikka && images.tikka) {
      const place = tikkaPlacement(face, p.tikka.aspect, p.tikka.fit);
      const s = swing('tikka', place, face[10].z);
      draws.push({ image: images.tikka, place: { ...place, rotation: s.side }, scaleX: turnScale(pose.yaw), scaleY: Math.cos(s.tilt) * turnScale(pose.pitch, 0.6) });
    }
    if (parts.includes('earrings') && p.earringLeft && p.earringRight && images.earringLeft && images.earringRight) {
      const { left, right } = earringPlacements(face, p.earringLeft.aspect, p.earringRight.aspect, p.earringLeft.fit);
      const leftZ = face[132].x <= face[361].x ? face[132].z : face[361].z;
      const rightZ = face[132].x <= face[361].x ? face[361].z : face[132].z;
      const sl = swing('earringLeft', left, leftZ);
      const sr = swing('earringRight', right, rightZ);
      // Earrings hang facing forward with the head, so they narrow as it turns.
      const ear = turnScale(pose.yaw, 0.3);
      draws.push({ image: images.earringLeft, place: { ...left, rotation: sl.side }, scaleX: ear, scaleY: Math.cos(sl.tilt), behindFace: true });
      draws.push({ image: images.earringRight, place: { ...right, rotation: sr.side }, scaleX: ear, scaleY: Math.cos(sr.tilt), behindFace: true });
    }

    const outline = facePolygon(face);
    for (const d of draws) this.drawPiece(context, d, frame.faceWidth, outline);
  }

  private drawPiece(context: CanvasRenderingContext2D, d: Draw, faceWidth: number, outline: Point[]): void {
    const { place } = d;
    if (place.opacity <= 0.01 || d.image.naturalWidth === 0 || place.width < 1 || place.height < 1) return;
    const layer = this.layer(d, place);
    context.save();
    if (d.behindFace) {
      // Everything except the face: an earring passing behind the cheek is cut there.
      context.beginPath();
      context.rect(0, 0, context.canvas.width, context.canvas.height);
      outline.forEach((pt, i) => i === 0 ? context.moveTo(pt.x, pt.y) : context.lineTo(pt.x, pt.y));
      context.closePath();
      context.clip('evenodd');
    }
    context.globalAlpha = place.opacity;
    context.shadowColor = 'rgba(30, 15, 5, 0.38)';
    context.shadowBlur = faceWidth * 0.025;
    context.shadowOffsetX = faceWidth * 0.008;
    context.shadowOffsetY = faceWidth * 0.018;
    context.translate(place.x, place.y);
    context.rotate(place.rotation);
    context.scale(Math.max(d.scaleX, 0.05), Math.max(Math.abs(d.scaleY), 0.5));
    context.drawImage(layer, -place.anchorX * place.width, -place.anchorY * place.height, place.width, place.height);
    context.restore();
  }

  // The piece on its own small canvas, with the light and (for a necklace) the neck tuck applied.
  private layer(d: Draw, place: Placement): HTMLCanvasElement {
    let canvas = this.layers.get(d.image.src);
    if (!canvas) { canvas = document.createElement('canvas'); this.layers.set(d.image.src, canvas); }
    const w = Math.min(Math.ceil(place.width), 1024);
    const h = Math.min(Math.ceil(place.height), 1024);
    if (canvas.width !== w || canvas.height !== h) { canvas.width = w; canvas.height = h; }
    const c = canvas.getContext('2d');
    if (!c) return canvas;
    c.globalCompositeOperation = 'source-over';
    c.clearRect(0, 0, w, h);
    c.drawImage(d.image, 0, 0, w, h);
    if (d.tuck) {
      // The chain ends at the top go behind the neck: fade the top band away.
      c.globalCompositeOperation = 'destination-out';
      const g = c.createLinearGradient(0, 0, 0, h * 0.16);
      g.addColorStop(0, 'rgba(0,0,0,1)');
      g.addColorStop(1, 'rgba(0,0,0,0)');
      c.fillStyle = g;
      c.fillRect(0, 0, w, h * 0.16);
    }
    c.globalCompositeOperation = 'source-atop';
    const { brightness, tint } = this.light;
    if (brightness < 0.99) { c.fillStyle = `rgba(0,0,0,${(1 - brightness).toFixed(3)})`; c.fillRect(0, 0, w, h); }
    if (brightness > 1.01) { c.fillStyle = `rgba(255,255,255,${((brightness - 1) * 0.6).toFixed(3)})`; c.fillRect(0, 0, w, h); }
    c.fillStyle = `rgba(${tint.r},${tint.g},${tint.b},0.08)`;
    c.fillRect(0, 0, w, h);
    c.globalCompositeOperation = 'source-over';
    return canvas;
  }

  // Reads a small patch of skin around the nose to estimate the room light.
  private measureLight(context: CanvasRenderingContext2D, nose: Point, faceWidth: number): void {
    const size = Math.max(4, Math.round(faceWidth * 0.18));
    const x = Math.round(nose.x - size / 2);
    const y = Math.round(nose.y - size / 2);
    if (x < 0 || y < 0 || x + size > context.canvas.width || y + size > context.canvas.height) return;
    try {
      const next = lightFromPixels(context.getImageData(x, y, size, size).data);
      this.light = blendLight(this.light, next);
    } catch { /* reading pixels can fail on some browsers; keep the last light */ }
  }
}
