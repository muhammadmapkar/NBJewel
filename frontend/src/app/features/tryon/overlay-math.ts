// Role: Pure math that turns MediaPipe landmarks into where to draw a 2D jewelry image:
// position, size, rotation and opacity. No Angular, no DOM, so it is unit tested on its own
// (overlay-math.spec.ts). All points here are in canvas pixels (see toPixels).
//
// Landmark indices (MediaPipe Face Landmarker, 478 points; Hand Landmarker, 21 points).
// Checked on a test photo in headless Chromium (see docs/tryon-design.md):
//   234 and 454 sit on the face edge at eye level (near the top of the ear), not on the lobe.
//   132 and 361 sit on the face edge at earlobe height, so the earring hook hangs there,
//   pushed a little outward. 234/454 still give the face width and the ear visibility.
//   152 is the chin, 10 the top of the forehead, 1 the nose tip.
//   Hand: 9 middle finger base, 13 ring finger base (MCP), 14 ring finger middle joint (PIP),
//   17 little finger base. A ring sits on the segment between 13 and 14.

export interface Point { x: number; y: number }

// Where and how to draw one image. (x, y) is the canvas pixel where the image anchor lands.
// anchorX and anchorY are fractions of the image size (0.5, 0 is the top center).
export interface Placement {
  x: number;
  y: number;
  width: number;
  height: number;
  rotation: number; // radians, clockwise on screen
  opacity: number; // 0 hides the image
  anchorX: number;
  anchorY: number;
}

export const FACE = { rightEdge: 234, leftEdge: 454, rightLobe: 132, leftLobe: 361, chin: 152, forehead: 10, nose: 1 } as const;
export const HAND = { middleBase: 9, ringBase: 13, ringMiddle: 14, littleBase: 17 } as const;

// Tuning values, in multiples of the face width (or face height, or finger width). Assumed,
// chosen by eye on test photos and the generated sample images.
export const TUNING = {
  earringWidth: 0.18, // earring image width vs face width (sample images: about 0.36 face width tall)
  earringOutward: 0.05, // move the hook outward from the face edge
  earHideShare: 0.25, // ear fully hidden when its side holds 25 percent or less of the face
  earShowShare: 0.35, // ear fully shown at 35 percent or more
  necklaceWidth: 1.3, // necklace image width vs face width
  necklaceDrop: 0.22, // top of the necklace below the chin, vs face height
  necklaceRollFollow: 0.5, // the necklace follows half of the head tilt
  ringAlong: 0.4, // ring position from the finger base (0) to the middle joint (1)
  fingerWidthShare: 0.8, // finger width vs the gap between finger bases
  ringWidth: 1.3, // ring image width vs finger width
  smoothing: 0.4, // low-pass filter: share of the new value per frame when still (1 means no smoothing)
  fastMovePx: 12 // average move per frame, in canvas pixels, at which smoothing switches off (no lag)
} as const;

export function distance(a: Point, b: Point): number { return Math.hypot(b.x - a.x, b.y - a.y); }
export function lerp(a: Point, b: Point, t: number): Point { return { x: a.x + (b.x - a.x) * t, y: a.y + (b.y - a.y) * t }; }
export function clamp(value: number, min: number, max: number): number { return Math.min(max, Math.max(min, value)); }

// MediaPipe gives x and y from 0 to 1. Converts to canvas pixels. mirror flips x, used for the
// front camera so the picture behaves like a mirror.
export function toPixels(landmark: Point, width: number, height: number, mirror: boolean): Point {
  return { x: (mirror ? 1 - landmark.x : landmark.x) * width, y: landmark.y * height };
}

// Angle of the line through a and b, taken from the leftmost point, so mirroring does not
// turn a level line into a half turn. Range is -PI/2 to PI/2.
export function rollAngle(a: Point, b: Point): number {
  const [left, right] = a.x <= b.x ? [a, b] : [b, a];
  return Math.atan2(right.y - left.y, right.x - left.x);
}

// How much of each ear can be seen, from 0 to 1. When the head turns, the face side that turns
// away gets narrow on screen, so its share of the nose to edge distances drops.
export function earVisibility(nose: Point, rightEdge: Point, leftEdge: Point): { right: number; left: number } {
  const right = distance(nose, rightEdge);
  const left = distance(nose, leftEdge);
  const total = right + left;
  if (total === 0) return { right: 0, left: 0 };
  const fade = (share: number) => clamp((share - TUNING.earHideShare) / (TUNING.earShowShare - TUNING.earHideShare), 0, 1);
  return { right: fade(right / total), left: fade(left / total) };
}

// imageAspect is image height divided by image width.
export function earringPlacements(face: readonly Point[], imageAspect: number): [Placement, Placement] {
  const rightEdge = face[FACE.rightEdge];
  const leftEdge = face[FACE.leftEdge];
  const faceWidth = distance(rightEdge, leftEdge);
  const width = faceWidth * TUNING.earringWidth;
  const visible = earVisibility(face[FACE.nose], rightEdge, leftEdge);
  // Unit vector from the left edge to the right edge: "outward" for the right ear.
  const ux = faceWidth === 0 ? 0 : (rightEdge.x - leftEdge.x) / faceWidth;
  const uy = faceWidth === 0 ? 0 : (rightEdge.y - leftEdge.y) / faceWidth;
  const push = faceWidth * TUNING.earringOutward;
  const make = (lobe: Point, sign: number, opacity: number): Placement => ({
    x: lobe.x + ux * push * sign,
    y: lobe.y + uy * push * sign,
    width,
    height: width * imageAspect,
    rotation: 0, // earrings hang straight down
    opacity,
    anchorX: 0.5,
    anchorY: 0
  });
  return [make(face[FACE.rightLobe], 1, visible.right), make(face[FACE.leftLobe], -1, visible.left)];
}

export function necklacePlacement(face: readonly Point[], imageAspect: number): Placement {
  const faceWidth = distance(face[FACE.rightEdge], face[FACE.leftEdge]);
  const chin = face[FACE.chin];
  const forehead = face[FACE.forehead];
  const faceHeight = distance(forehead, chin);
  // Unit vector from the forehead to the chin: "down" along the face.
  const dx = faceHeight === 0 ? 0 : (chin.x - forehead.x) / faceHeight;
  const dy = faceHeight === 0 ? 1 : (chin.y - forehead.y) / faceHeight;
  const drop = faceHeight * TUNING.necklaceDrop;
  const width = faceWidth * TUNING.necklaceWidth;
  return {
    x: chin.x + dx * drop,
    y: chin.y + dy * drop,
    width,
    height: width * imageAspect,
    rotation: rollAngle(face[FACE.rightEdge], face[FACE.leftEdge]) * TUNING.necklaceRollFollow,
    opacity: 1,
    anchorX: 0.5,
    anchorY: 0
  };
}

// The ring image has its band drawn level, with the finger running top to bottom through it
// and the top of the image toward the fingertip.
export function ringPlacement(hand: readonly Point[], imageAspect: number): Placement {
  const base = hand[HAND.ringBase];
  const joint = hand[HAND.ringMiddle];
  const fingerWidth = TUNING.fingerWidthShare * (distance(hand[HAND.middleBase], base) + distance(base, hand[HAND.littleBase])) / 2;
  const segment = distance(base, joint);
  const angle = Math.atan2(joint.y - base.y, joint.x - base.x);
  const width = fingerWidth * TUNING.ringWidth;
  const at = lerp(base, joint, TUNING.ringAlong);
  return {
    x: at.x,
    y: at.y,
    width,
    height: width * imageAspect,
    // Turns the image so its upward axis points along the finger, from base toward the tip.
    // A finger pointing straight up gives 0.
    rotation: angle + Math.PI / 2,
    // A finger pointing at the camera looks very short; fade the ring out then.
    opacity: fingerWidth === 0 ? 0 : clamp((segment / fingerWidth - 0.3) / 0.4, 0, 1),
    anchorX: 0.5,
    anchorY: 0.5
  };
}

// Simple low-pass filter (exponential smoothing) for landmark lists, to stop jitter.
// Each frame keeps (1 - alpha) of the old position. With fastMovePx above 0 the filter adapts:
// alpha grows with the average move of all points, so small shakes are smoothed but a real head
// or hand move passes through at once (no lag). Using one alpha for all points keeps the shape.
// reset() forgets the history, used when the face or hand is lost so the next one does not
// slide in from the old place.
export class LandmarkSmoother {
  private previous: Point[] | null = null;
  constructor(private readonly alpha: number = TUNING.smoothing, private readonly fastMovePx = 0) {}

  smooth(points: readonly Point[]): Point[] {
    const prev = this.previous;
    if (!prev || prev.length !== points.length || points.length === 0) {
      this.previous = points.map(p => ({ x: p.x, y: p.y }));
      return this.previous;
    }
    const alpha = this.fastMovePx > 0
      ? clamp(this.alpha + (1 - this.alpha) * (averageMove(prev, points) / this.fastMovePx), this.alpha, 1)
      : this.alpha;
    const next = points.map((p, i) => ({ x: prev[i].x + (p.x - prev[i].x) * alpha, y: prev[i].y + (p.y - prev[i].y) * alpha }));
    this.previous = next;
    return next;
  }

  reset(): void { this.previous = null; }
}

function averageMove(a: readonly Point[], b: readonly Point[]): number {
  let total = 0;
  for (let i = 0; i < a.length; i++) total += distance(a[i], b[i]);
  return total / a.length;
}

// The four corners of a drawn placement, in canvas pixels. Used by tests, and handy for debugging.
export function placementCorners(p: Placement): Point[] {
  const cos = Math.cos(p.rotation);
  const sin = Math.sin(p.rotation);
  const left = -p.anchorX * p.width;
  const top = -p.anchorY * p.height;
  return [[left, top], [left + p.width, top], [left + p.width, top + p.height], [left, top + p.height]]
    .map(([x, y]) => ({ x: p.x + x * cos - y * sin, y: p.y + x * sin + y * cos }));
}
