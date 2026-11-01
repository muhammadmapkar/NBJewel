// Role: Simple, real-scale physics for hanging jewelry. An earring or tikka is a damped pendulum
// hanging from a moving point (the ear lobe or the hairline): when the head moves, the piece
// lags, swings and settles under gravity. A necklace uses the same model with a spring that pulls
// it back to rest on the neck, so it only sways a little. Pure functions, unit tested.
//
// Coordinates are canvas pixels, y down. Gravity is 9.81 m/s2 turned into pixels using the
// face width (about 14 cm), so a bigger face on screen gets the same real motion.
// Pendulum with a moving pivot (pivot acceleration ax, ay), angle t (0 = hanging straight down,
// positive = swung toward +x):
//   t'' = -((g + ay) sin t + ax cos t) / L - stiffness (t - rest) - damping t'

import { Point, clamp } from './overlay-math';

export const GRAVITY = 9.81; // m/s2

export interface SwingSettings {
  lengthMeters: number; // pivot to centre of mass
  damping: number; // 1/s, how fast a swing dies out
  stiffness: number; // 1/s2, pull back to rest (0 for a free pendulum)
  maxAngle: number; // radians
}

// Assumed values, chosen so a 6 cm earring swings at about 2 to 3 swings per second and
// settles in about a second, and a necklace only sways a little.
export const EARRING: SwingSettings = { lengthMeters: 0.03, damping: 3.2, stiffness: 0, maxAngle: 1.0 };
export const TIKKA: SwingSettings = { lengthMeters: 0.025, damping: 3.5, stiffness: 0, maxAngle: 0.8 };
export const NECKLACE: SwingSettings = { lengthMeters: 0.06, damping: 5, stiffness: 60, maxAngle: 0.35 };

export interface SwingState { angle: number; velocity: number }

// Advances one swing by dt seconds. accel is the pivot acceleration in px/s2.
export function stepSwing(state: SwingState, dt: number, accel: Point, pixelsPerMeter: number, settings: SwingSettings, rest = 0): SwingState {
  const g = GRAVITY * pixelsPerMeter;
  const length = Math.max(settings.lengthMeters * pixelsPerMeter, 1);
  let { angle, velocity } = state;
  // Small fixed steps keep the integration stable when frames are slow.
  const steps = Math.max(1, Math.ceil(dt / (1 / 240)));
  const h = dt / steps;
  for (let i = 0; i < steps; i++) {
    const acc = -((g + accel.y) * Math.sin(angle) + accel.x * Math.cos(angle)) / length
      - settings.stiffness * (angle - rest)
      - settings.damping * velocity;
    velocity += acc * h; // semi-implicit Euler
    angle += velocity * h;
    if (Math.abs(angle) > settings.maxAngle) { angle = Math.sign(angle) * settings.maxAngle; velocity *= -0.3; }
  }
  return { angle, velocity };
}

// The swing angle as a canvas rotation for an image hanging from its top centre:
// canvas rotation r moves the image's down axis (0, 1) to (-sin r, cos r), so r = -angle.
export function swingToRotation(angle: number): number { return -angle; }

// Tracks a pivot point and estimates its acceleration from three positions, with a low-pass
// filter (tracking noise turns into large fake accelerations otherwise) and a cap.
export class PivotMotion {
  private prev: Point | null = null;
  private velocity: Point = { x: 0, y: 0 };
  private accel: Point = { x: 0, y: 0 };
  private hasVelocity = false;

  constructor(private readonly filter = 0.35) {}

  // Returns the filtered acceleration in px/s2. maxAccel caps it (px/s2).
  update(position: Point, dt: number, maxAccel: number): Point {
    if (!this.prev || dt <= 0 || dt > 0.25) {
      this.prev = position;
      this.velocity = { x: 0, y: 0 };
      this.accel = { x: 0, y: 0 };
      this.hasVelocity = false;
      return this.accel;
    }
    const v = { x: (position.x - this.prev.x) / dt, y: (position.y - this.prev.y) / dt };
    if (this.hasVelocity) {
      const raw = { x: (v.x - this.velocity.x) / dt, y: (v.y - this.velocity.y) / dt };
      const a = {
        x: this.accel.x + (clamp(raw.x, -maxAccel, maxAccel) - this.accel.x) * this.filter,
        y: this.accel.y + (clamp(raw.y, -maxAccel, maxAccel) - this.accel.y) * this.filter
      };
      this.accel = a;
    }
    this.velocity = v;
    this.hasVelocity = true;
    this.prev = position;
    return this.accel;
  }

  reset(): void { this.prev = null; }
}

// One hanging piece: pivot tracking plus its swing state.
export class Swing {
  private state: SwingState = { angle: 0, velocity: 0 };
  private readonly pivot = new PivotMotion();
  constructor(private readonly settings: SwingSettings) {}

  // Returns the canvas rotation to add to the piece. rest is the angle it settles at.
  update(pivot: Point, dt: number, pixelsPerMeter: number, rest = 0): number {
    const accel = this.pivot.update(pivot, dt, 4 * GRAVITY * pixelsPerMeter);
    if (dt > 0 && dt <= 0.25) this.state = stepSwing(this.state, dt, accel, pixelsPerMeter, this.settings, rest);
    return swingToRotation(this.state.angle);
  }

  get angle(): number { return this.state.angle; }

  reset(): void {
    this.state = { angle: 0, velocity: 0 };
    this.pivot.reset();
  }
}
