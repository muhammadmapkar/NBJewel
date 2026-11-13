// Role: Unit tests for the 3D helpers: head turn and tilt from landmark depth, narrowing of a
// turned piece, distance from the camera, room light, and the point-in-face test.

import { describe, expect, it } from 'vitest';
import { FACE } from './overlay-math';
import { Point3, blendLight, depthMeters, headPose, insidePolygon, lightFromPixels, toPixels3, turnScale } from './pose';

function face(points: Record<number, Point3>): Point3[] {
  return Array.from({ length: 478 }, (_, i) => points[i] ?? { x: 0, y: 0, z: 0 });
}

const front = () => face({
  [FACE.rightEdge]: { x: 100, y: 200, z: 0 }, [FACE.leftEdge]: { x: 300, y: 200, z: 0 },
  [FACE.forehead]: { x: 200, y: 80, z: 0 }, [FACE.chin]: { x: 200, y: 320, z: 0 }
});

describe('headPose', () => {
  it('a face looking at the camera has no turn and no tilt', () => {
    expect(headPose(front())).toEqual({ yaw: 0, pitch: 0 });
  });

  it('screen-right side farther away gives a positive yaw of the right size', () => {
    const f = front();
    f[FACE.leftEdge] = { x: 300, y: 200, z: 200 }; // 200 px deeper over 200 px across: 45 degrees
    expect(headPose(f).yaw).toBeCloseTo(Math.PI / 4);
  });

  it('works the same when the view is mirrored (points swap sides)', () => {
    const f = front();
    f[FACE.rightEdge] = { x: 300, y: 200, z: 200 };
    f[FACE.leftEdge] = { x: 100, y: 200, z: 0 };
    expect(headPose(f).yaw).toBeCloseTo(Math.PI / 4);
  });

  it('chin farther than forehead is a positive pitch (looking up)', () => {
    const f = front();
    f[FACE.chin] = { x: 200, y: 320, z: 240 };
    expect(headPose(f).pitch).toBeCloseTo(Math.PI / 4);
  });
});

describe('turnScale, depth and toPixels3', () => {
  it('a piece turned 60 degrees looks half as wide, never thinner than the minimum', () => {
    expect(turnScale(Math.PI / 3)).toBeCloseTo(0.5);
    expect(turnScale(Math.PI / 2)).toBe(0.35);
  });

  it('a face half as wide on screen is twice as far', () => {
    expect(depthMeters(100, 1000) / depthMeters(200, 1000)).toBeCloseTo(2);
    expect(depthMeters(0, 1000)).toBe(0);
  });

  it('z is scaled by the frame width like x', () => {
    expect(toPixels3({ x: 0.5, y: 0.5, z: 0.1 }, 1000, 500)).toEqual({ x: 500, y: 250, z: 100 });
  });
});

describe('light', () => {
  const pixels = (v: number) => new Uint8ClampedArray([v, v, v, 255, v, v, v, 255]);

  it('a well lit face keeps the jewelry as it is', () => {
    expect(lightFromPixels(pixels(153)).brightness).toBeCloseTo(1);
  });

  it('a dark room dims it, but not below 0.6', () => {
    expect(lightFromPixels(pixels(92)).brightness).toBeCloseTo(0.6, 1);
    expect(lightFromPixels(pixels(10)).brightness).toBe(0.6);
  });

  it('readings ease in, so the jewelry does not flicker', () => {
    const next = blendLight({ brightness: 1, tint: { r: 0, g: 0, b: 0 } }, { brightness: 0.6, tint: { r: 100, g: 100, b: 100 } }, 0.25);
    expect(next.brightness).toBeCloseTo(0.9);
    expect(next.tint.r).toBe(25);
  });

  it('no pixels means neutral light', () => {
    expect(lightFromPixels([]).brightness).toBe(1);
  });
});

describe('insidePolygon', () => {
  const square = [{ x: 0, y: 0 }, { x: 10, y: 0 }, { x: 10, y: 10 }, { x: 0, y: 10 }];
  it('finds points inside and outside', () => {
    expect(insidePolygon({ x: 5, y: 5 }, square)).toBe(true);
    expect(insidePolygon({ x: 15, y: 5 }, square)).toBe(false);
  });
});
