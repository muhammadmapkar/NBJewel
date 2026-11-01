// Role: Unit tests for the jewelry physics: a piece at rest stays at rest, a head move makes it
// lag and swing the right way, the swing dies out, a necklace sways less than an earring, the
// angle maps to the right canvas rotation, and the pivot tracker turns positions into acceleration.

import { describe, expect, it } from 'vitest';
import { EARRING, NECKLACE, PivotMotion, Swing, SwingState, stepSwing, swingToRotation } from './physics';

const PPM = 1000; // 1000 px per metre (a face about 140 px wide)
const still = { x: 0, y: 0 };

function run(state: SwingState, seconds: number, accel = still, settings = EARRING): SwingState {
  let s = state;
  const frames = Math.round(seconds * 60);
  for (let i = 0; i < frames; i++) s = stepSwing(s, 1 / 60, accel, PPM, settings);
  return s;
}

describe('stepSwing', () => {
  it('hanging straight down with a still head stays put', () => {
    expect(run({ angle: 0, velocity: 0 }, 2)).toEqual({ angle: 0, velocity: 0 });
  });

  it('when the head speeds up to the right, the earring lags to the left (negative angle)', () => {
    const s = run({ angle: 0, velocity: 0 }, 0.05, { x: 3000, y: 0 });
    expect(s.angle).toBeLessThan(0);
  });

  it('a constant push leans it to the angle where gravity balances the push', () => {
    // Pivot accelerating right at 0.5 g: tan(angle) = -0.5.
    const s = run({ angle: 0, velocity: 0 }, 4, { x: 0.5 * 9.81 * PPM, y: 0 });
    expect(s.angle).toBeCloseTo(-Math.atan(0.5), 2);
  });

  it('a swing dies out and returns to hanging straight down', () => {
    const s = run({ angle: 0.5, velocity: 0 }, 4);
    expect(Math.abs(s.angle)).toBeLessThan(0.01);
  });

  it('it really swings: after half a period it is on the other side', () => {
    // Small angle period: 2 pi sqrt(L/g), about 0.35 s for 3 cm.
    const period = 2 * Math.PI * Math.sqrt(EARRING.lengthMeters / 9.81);
    const s = run({ angle: 0.3, velocity: 0 }, period / 2);
    expect(s.angle).toBeLessThan(0);
  });

  it('a necklace sways much less than an earring for the same head move', () => {
    const push = { x: 6000, y: 0 };
    const earring = run({ angle: 0, velocity: 0 }, 0.15, push, EARRING);
    const necklace = run({ angle: 0, velocity: 0 }, 0.15, push, NECKLACE);
    expect(Math.abs(necklace.angle)).toBeLessThan(Math.abs(earring.angle) / 2);
  });

  it('never swings past the limit', () => {
    const s = run({ angle: 0, velocity: 0 }, 1, { x: 1e6, y: 0 });
    expect(Math.abs(s.angle)).toBeLessThanOrEqual(EARRING.maxAngle);
  });

  it('a long frame gives the same result as many short ones (stable steps)', () => {
    const one = stepSwing({ angle: 0.4, velocity: 0 }, 0.1, still, PPM, EARRING);
    const many = run({ angle: 0.4, velocity: 0 }, 0.1);
    expect(one.angle).toBeCloseTo(many.angle, 1);
  });
});

describe('swingToRotation', () => {
  it('a piece swung toward +x is drawn rotated counterclockwise (negative canvas rotation)', () => {
    // Canvas rotation r sends the image down axis (0, 1) to (-sin r, cos r). For the bottom to go
    // to +x, -sin r must be positive, so r is negative.
    const r = swingToRotation(0.3);
    expect(-Math.sin(r)).toBeGreaterThan(0);
  });
});

describe('PivotMotion', () => {
  it('a point moving at constant speed has no acceleration', () => {
    const m = new PivotMotion(1);
    let a = { x: 0, y: 0 };
    for (let i = 0; i < 10; i++) a = m.update({ x: i * 10, y: 0 }, 1 / 30, 1e9);
    expect(a.x).toBeCloseTo(0, 6);
  });

  it('a point that speeds up has positive acceleration', () => {
    const m = new PivotMotion(1);
    let a = { x: 0, y: 0 };
    // x = t^2 sampled every 0.1 s has acceleration 2.
    for (let i = 0; i < 5; i++) a = m.update({ x: (i * 0.1) ** 2, y: 0 }, 0.1, 1e9);
    expect(a.x).toBeCloseTo(2, 6);
  });

  it('caps huge jumps (tracking glitches)', () => {
    const m = new PivotMotion(1);
    m.update({ x: 0, y: 0 }, 1 / 30, 500);
    m.update({ x: 0, y: 0 }, 1 / 30, 500);
    const a = m.update({ x: 1000, y: 0 }, 1 / 30, 500);
    expect(a.x).toBe(500);
  });

  it('a long pause starts fresh instead of reading it as a huge move', () => {
    const m = new PivotMotion(1);
    m.update({ x: 0, y: 0 }, 1 / 30, 1e9);
    m.update({ x: 1, y: 0 }, 1 / 30, 1e9);
    expect(m.update({ x: 500, y: 0 }, 1, 1e9)).toEqual({ x: 0, y: 0 });
  });
});

describe('Swing', () => {
  it('a head that moves right then stops makes the earring swing and settle', () => {
    const swing = new Swing(EARRING);
    const angles: number[] = [];
    for (let i = 0; i < 180; i++) {
      const x = i < 10 ? i * i * 2 : 200 + (i - 10) * 0; // speeds up, then stops abruptly
      swing.update({ x: Math.min(x, 200), y: 0 }, 1 / 60, PPM);
      angles.push(swing.angle);
    }
    expect(Math.min(...angles)).toBeLessThan(-0.05); // lagged behind while speeding up
    expect(Math.max(...angles)).toBeGreaterThan(0.05); // swung forward when the head stopped
    expect(Math.abs(angles[angles.length - 1])).toBeLessThan(0.02); // settled
  });
});
