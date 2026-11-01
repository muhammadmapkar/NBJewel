// Role: Pure math that turns MediaPipe face landmarks into where to draw each jewelry piece
// (necklace, left and right earring, tikka): position, size, base rotation and opacity.
// No Angular and no DOM, so it is unit tested (overlay-math.spec.ts). All points are canvas
// pixels. The swing on top of these placements comes from physics.ts.
//
// Face Landmarker indices (478 points), checked on a test photo:
//   234 and 454: face edge at eye level (face width, ear visibility).
//   132 and 361: face edge at earlobe height (earring hook).
//   152 chin, 10 top of the forehead, 1 nose tip.

export interface Point { x: number; y: number }

// (x, y) is where the image anchor lands. anchorX/anchorY are fractions of the image size.
export interface Placement {
  x: number;
  y: number;
  width: number;
  height: number;
  rotation: number; // radians, clockwise on screen
  opacity: number;
  anchorX: number;
  anchorY: number;
}

// Per piece tuning stored in the catalog (assumed, set by eye).
export interface Fit {
  width?: number; // necklace width as a multiple of face width
  drop?: number; // extra drop of the necklace top below the default line, in face heights
  size?: number; // multiplier on the default earring or tikka size
}

export const FACE = { rightEdge: 234, leftEdge: 454, rightLobe: 132, leftLobe: 361, chin: 152, forehead: 10, nose: 1 } as const;

// Assumed real world sizes, in multiples of face width (about 14 cm on an adult).
export const TUNING = {
  earringHeight: 0.42, // a bridal drop earring is about 6 cm long
  earringOutward: 0.05,
  earHideShare: 0.25,
  earShowShare: 0.35,
  necklaceWidth: 1.4,
  necklaceDrop: 0.22, // top of the necklace below the chin, in face heights
  necklaceRollFollow: 0.5,
  tikkaHeight: 0.42,
  tikkaRaise: 0.04, // tikka chain starts this far above landmark 10 (at the hairline), in face heights
  faceWidthMeters: 0.14
} as const;

export function distance(a: Point, b: Point): number { return Math.hypot(b.x - a.x, b.y - a.y); }
export function clamp(value: number, min: number, max: number): number { return Math.min(max, Math.max(min, value)); }

export function toPixels(landmark: Point, width: number, height: number, mirror: boolean): Point {
  return { x: (mirror ? 1 - landmark.x : landmark.x) * width, y: landmark.y * height };
}

// Angle of the line through a and b from its leftmost point, so mirroring keeps it level.
export function rollAngle(a: Point, b: Point): number {
  const [left, right] = a.x <= b.x ? [a, b] : [b, a];
  return Math.atan2(right.y - left.y, right.x - left.x);
}

export interface FaceFrame {
  faceWidth: number;
  faceHeight: number;
  down: Point; // unit vector from forehead to chin
  roll: number;
  pixelsPerMeter: number;
}

export function faceFrame(face: readonly Point[]): FaceFrame {
  const faceWidth = distance(face[FACE.rightEdge], face[FACE.leftEdge]);
  const chin = face[FACE.chin];
  const forehead = face[FACE.forehead];
  const faceHeight = distance(forehead, chin);
  const down = faceHeight === 0 ? { x: 0, y: 1 } : { x: (chin.x - forehead.x) / faceHeight, y: (chin.y - forehead.y) / faceHeight };
  return { faceWidth, faceHeight, down, roll: rollAngle(face[FACE.rightEdge], face[FACE.leftEdge]), pixelsPerMeter: faceWidth / TUNING.faceWidthMeters };
}

// 0 to 1 for each ear. The face side that turns away gets narrow on screen.
export function earVisibility(nose: Point, rightEdge: Point, leftEdge: Point): { right: number; left: number } {
  const right = distance(nose, rightEdge);
  const left = distance(nose, leftEdge);
  const total = right + left;
  if (total === 0) return { right: 0, left: 0 };
  const fade = (share: number) => clamp((share - TUNING.earHideShare) / (TUNING.earShowShare - TUNING.earHideShare), 0, 1);
  return { right: fade(right / total), left: fade(left / total) };
}

// Earrings for the ear on the left and on the right of the screen (after any mirroring), so the
// photo's left earring is always drawn on the left. imageAspect = image height / width.
export function earringPlacements(face: readonly Point[], aspectLeft: number, aspectRight: number, fit: Fit = {}): { left: Placement; right: Placement } {
  const frame = faceFrame(face);
  const rightEdge = face[FACE.rightEdge];
  const leftEdge = face[FACE.leftEdge];
  const visible = earVisibility(face[FACE.nose], rightEdge, leftEdge);
  const height = frame.faceWidth * TUNING.earringHeight * (fit.size ?? 1);
  const ux = frame.faceWidth === 0 ? 0 : (rightEdge.x - leftEdge.x) / frame.faceWidth;
  const uy = frame.faceWidth === 0 ? 0 : (rightEdge.y - leftEdge.y) / frame.faceWidth;
  const push = frame.faceWidth * TUNING.earringOutward;
  const ear234 = { at: face[FACE.rightLobe], sign: 1, opacity: visible.right };
  const ear454 = { at: face[FACE.leftLobe], sign: -1, opacity: visible.left };
  const make = (ear: typeof ear234, aspect: number): Placement => ({
    x: ear.at.x + ux * push * ear.sign,
    y: ear.at.y + uy * push * ear.sign,
    width: height / aspect,
    height,
    rotation: 0, // the swing (physics) sets the angle; gravity pulls straight down
    opacity: ear.opacity,
    anchorX: 0.5,
    anchorY: 0
  });
  const screenLeftIs234 = face[FACE.rightLobe].x <= face[FACE.leftLobe].x;
  return screenLeftIs234
    ? { left: make(ear234, aspectLeft), right: make(ear454, aspectRight) }
    : { left: make(ear454, aspectLeft), right: make(ear234, aspectRight) };
}

export function necklacePlacement(face: readonly Point[], aspect: number, fit: Fit = {}): Placement {
  const frame = faceFrame(face);
  const chin = face[FACE.chin];
  const drop = frame.faceHeight * (TUNING.necklaceDrop + (fit.drop ?? 0));
  const width = frame.faceWidth * (fit.width ?? TUNING.necklaceWidth);
  return {
    x: chin.x + frame.down.x * drop,
    y: chin.y + frame.down.y * drop,
    width,
    height: width * aspect,
    rotation: frame.roll * TUNING.necklaceRollFollow,
    opacity: 1,
    anchorX: 0.5,
    anchorY: 0
  };
}

export function tikkaPlacement(face: readonly Point[], aspect: number, fit: Fit = {}): Placement {
  const frame = faceFrame(face);
  const top = face[FACE.forehead];
  const raise = frame.faceHeight * TUNING.tikkaRaise;
  const height = frame.faceWidth * TUNING.tikkaHeight * (fit.size ?? 1);
  return {
    x: top.x - frame.down.x * raise,
    y: top.y - frame.down.y * raise,
    width: height / aspect,
    height,
    rotation: 0,
    opacity: 1,
    anchorX: 0.5,
    anchorY: 0
  };
}

// Simple low-pass filter for landmark lists. The share of the new position grows with the
// average move, so jitter is smoothed but real moves are followed without lag.
export class LandmarkSmoother {
  private previous: Point[] | null = null;
  constructor(private readonly alpha = 0.4, private readonly fastMovePx = 12) {}

  smooth(points: readonly Point[]): Point[] {
    const prev = this.previous;
    if (!prev || prev.length !== points.length || points.length === 0) {
      this.previous = points.map(p => ({ x: p.x, y: p.y }));
      return this.previous;
    }
    let total = 0;
    for (let i = 0; i < points.length; i++) total += distance(prev[i], points[i]);
    const move = total / points.length;
    const alpha = this.fastMovePx > 0 ? clamp(this.alpha + (1 - this.alpha) * (move / this.fastMovePx), this.alpha, 1) : this.alpha;
    const next = points.map((p, i) => ({ x: prev[i].x + (p.x - prev[i].x) * alpha, y: prev[i].y + (p.y - prev[i].y) * alpha }));
    this.previous = next;
    return next;
  }

  reset(): void { this.previous = null; }
}
