// Role: Unit tests for the customer try-on placement math: earrings by real size and on the
// correct screen side, ear hiding, necklace and tikka position, and the landmark smoother.

import { describe, expect, it } from 'vitest';
import { FACE, LandmarkSmoother, Point, TUNING, earVisibility, earringPlacements, faceFrame, necklacePlacement, tikkaPlacement, toPixels } from './overlay-math';

function landmarks(points: Record<number, Point>): Point[] {
  return Array.from({ length: 478 }, (_, i) => points[i] ?? { x: 0, y: 0 });
}

// Level, front facing face: 200 px wide, 240 px forehead to chin. 234 is on screen left.
function face(): Point[] {
  return landmarks({
    [FACE.rightEdge]: { x: 100, y: 200 }, [FACE.leftEdge]: { x: 300, y: 200 },
    [FACE.rightLobe]: { x: 105, y: 250 }, [FACE.leftLobe]: { x: 295, y: 250 },
    [FACE.nose]: { x: 200, y: 230 }, [FACE.forehead]: { x: 200, y: 80 }, [FACE.chin]: { x: 200, y: 320 }
  });
}

const mirror = (pts: Point[]) => pts.map(p => ({ x: 400 - p.x, y: p.y }));

describe('faceFrame', () => {
  it('measures the face and its scale', () => {
    const f = faceFrame(face());
    expect(f.faceWidth).toBe(200);
    expect(f.faceHeight).toBe(240);
    expect(f.down).toEqual({ x: 0, y: 1 });
    expect(f.pixelsPerMeter).toBeCloseTo(200 / TUNING.faceWidthMeters);
  });

  it('toPixels mirrors x for the front camera', () => {
    expect(toPixels({ x: 0.25, y: 0.5 }, 640, 480, true)).toEqual({ x: 480, y: 240 });
  });
});

describe('earrings', () => {
  it('hang from the lobes, pushed outward, sized by real length', () => {
    const { left, right } = earringPlacements(face(), 2, 3);
    const h = 200 * TUNING.earringHeight;
    expect(left.height).toBeCloseTo(h);
    expect(left.width).toBeCloseTo(h / 2);
    expect(right.width).toBeCloseTo(h / 3);
    expect(left.x).toBeCloseTo(105 - 200 * TUNING.earringOutward);
    expect(right.x).toBeCloseTo(295 + 200 * TUNING.earringOutward);
    expect([left.anchorX, left.anchorY]).toEqual([0.5, 0]);
  });

  it('the photo left earring stays on the screen left when the view is mirrored', () => {
    const { left, right } = earringPlacements(mirror(face()), 2, 3);
    expect(left.x).toBeLessThan(right.x);
    expect(left.width).toBeCloseTo((200 * TUNING.earringHeight) / 2);
  });

  it('fit.size scales them', () => {
    expect(earringPlacements(face(), 2, 2, { size: 1.5 }).left.height).toBeCloseTo(200 * TUNING.earringHeight * 1.5);
  });

  it('the ear on the side turning away fades out', () => {
    const v = earVisibility({ x: 280, y: 230 }, { x: 100, y: 200 }, { x: 300, y: 200 });
    expect(v).toEqual({ right: 1, left: 0 });
  });
});

describe('necklace', () => {
  it('sits below the chin, sized by fit.width, level for a level head', () => {
    const p = necklacePlacement(face(), 0.8, { width: 1.5, drop: 0.1 });
    expect(p.x).toBeCloseTo(200);
    expect(p.y).toBeCloseTo(320 + 240 * (TUNING.necklaceDrop + 0.1));
    expect(p.width).toBeCloseTo(300);
    expect(p.height).toBeCloseTo(240);
    expect(p.rotation).toBeCloseTo(0);
  });

  it('with a known neckline, that edge rests at the base of the neck', () => {
    const p = necklacePlacement(face(), 1, {}, 0.4);
    expect(p.y).toBeCloseTo(320 + 240 * TUNING.neckBase);
    expect(p.anchorY).toBe(0.4);
  });

  it('uses the default width without a fit', () => {
    expect(necklacePlacement(face(), 1).width).toBeCloseTo(200 * TUNING.necklaceWidth);
  });
});

describe('tikka', () => {
  it('hangs from just above the top of the forehead, centred', () => {
    const p = tikkaPlacement(face(), 2);
    expect(p.x).toBeCloseTo(200);
    expect(p.y).toBeCloseTo(80 - 240 * TUNING.tikkaRaise);
    expect(p.height).toBeCloseTo(200 * TUNING.tikkaHeight);
    expect(p.width).toBeCloseTo(p.height / 2);
  });
});

describe('LandmarkSmoother', () => {
  it('smooths a small shake and passes a big move', () => {
    const s = new LandmarkSmoother(0.4, 12);
    s.smooth([{ x: 0, y: 0 }]);
    expect(s.smooth([{ x: 2, y: 0 }])[0].x).toBeCloseTo(1); // alpha 0.5
    expect(s.smooth([{ x: 100, y: 0 }])[0].x).toBe(100);
  });

  it('smooths depth (z) with the same share', () => {
    const s = new LandmarkSmoother<{ x: number; y: number; z: number }>(0.4, 12);
    s.smooth([{ x: 0, y: 0, z: 0 }]);
    expect(s.smooth([{ x: 2, y: 0, z: 10 }])[0].z).toBeCloseTo(5);
  });
});
