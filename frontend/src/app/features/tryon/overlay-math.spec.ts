// Role: Unit tests for the try-on overlay math (position, scale, rotation, ear hiding and the
// smoothing filter). Pure functions only; run with "npm test" (Vitest).

import { describe, expect, it } from 'vitest';
import {
  FACE, HAND, LandmarkSmoother, Point, TUNING, clamp, distance, earVisibility, earringPlacements, lerp,
  necklacePlacement, placementCorners, ringPlacement, rollAngle, toPixels
} from './overlay-math';

// Builds a landmark list where only the given indices matter.
function landmarks(size: number, points: Record<number, Point>): Point[] {
  return Array.from({ length: size }, (_, i) => points[i] ?? { x: 0, y: 0 });
}

// A front facing, level face, 200 px wide, 240 px from forehead to chin.
function frontFace(): Point[] {
  return landmarks(478, {
    [FACE.rightEdge]: { x: 100, y: 200 },
    [FACE.leftEdge]: { x: 300, y: 200 },
    [FACE.rightLobe]: { x: 105, y: 250 },
    [FACE.leftLobe]: { x: 295, y: 250 },
    [FACE.nose]: { x: 200, y: 230 },
    [FACE.forehead]: { x: 200, y: 80 },
    [FACE.chin]: { x: 200, y: 320 }
  });
}

// A hand with the ring finger pointing straight up on screen; finger bases 40 px apart.
function uprightHand(): Point[] {
  return landmarks(21, {
    [HAND.middleBase]: { x: 160, y: 400 },
    [HAND.ringBase]: { x: 200, y: 400 },
    [HAND.ringMiddle]: { x: 200, y: 300 },
    [HAND.littleBase]: { x: 240, y: 400 }
  });
}

const near = (value: number, expected: number, digits = 6) => expect(value).toBeCloseTo(expected, digits);

describe('basic helpers', () => {
  it('distance, lerp and clamp', () => {
    expect(distance({ x: 0, y: 0 }, { x: 3, y: 4 })).toBe(5);
    expect(lerp({ x: 0, y: 0 }, { x: 10, y: 20 }, 0.25)).toEqual({ x: 2.5, y: 5 });
    expect(clamp(5, 0, 1)).toBe(1);
    expect(clamp(-5, 0, 1)).toBe(0);
  });

  it('toPixels scales and mirrors x for the front camera', () => {
    expect(toPixels({ x: 0.25, y: 0.5 }, 640, 480, false)).toEqual({ x: 160, y: 240 });
    expect(toPixels({ x: 0.25, y: 0.5 }, 640, 480, true)).toEqual({ x: 480, y: 240 });
  });

  it('rollAngle ignores point order, so mirroring keeps a level line level', () => {
    near(rollAngle({ x: 0, y: 0 }, { x: 10, y: 0 }), 0);
    near(rollAngle({ x: 10, y: 0 }, { x: 0, y: 0 }), 0);
    near(rollAngle({ x: 0, y: 0 }, { x: 10, y: 10 }), Math.PI / 4);
    near(rollAngle({ x: 10, y: 10 }, { x: 0, y: 0 }), Math.PI / 4);
  });
});

describe('earrings', () => {
  it('hang from the lobes, pushed outward, scaled by face width, unrotated', () => {
    const [right, left] = earringPlacements(frontFace(), 2);
    const push = 200 * TUNING.earringOutward;
    near(right.x, 105 - push);
    near(right.y, 250);
    near(left.x, 295 + push);
    near(left.y, 250);
    near(right.width, 200 * TUNING.earringWidth);
    near(right.height, right.width * 2);
    expect(right.rotation).toBe(0);
    expect([right.anchorX, right.anchorY]).toEqual([0.5, 0]);
  });

  it('scale doubles when the face is twice as wide', () => {
    const face = frontFace().map(p => ({ x: p.x * 2, y: p.y * 2 }));
    near(earringPlacements(face, 2)[0].width, 400 * TUNING.earringWidth);
  });

  it('both ears show on a front facing head', () => {
    const [right, left] = earringPlacements(frontFace(), 2);
    expect(right.opacity).toBe(1);
    expect(left.opacity).toBe(1);
  });

  it('the ear on the side that turns away is hidden', () => {
    // Nose close to the left edge: the head turned so the left ear faces away.
    const v = earVisibility({ x: 280, y: 230 }, { x: 100, y: 200 }, { x: 300, y: 200 });
    expect(v.right).toBe(1);
    expect(v.left).toBe(0);
  });

  it('fades between the hide and show limits', () => {
    // Nose at 30 percent of the way from the right edge: right share 0.3, halfway between 0.25 and 0.35.
    const v = earVisibility({ x: 160, y: 0 }, { x: 100, y: 0 }, { x: 300, y: 0 });
    near(v.right, 0.5);
    expect(v.left).toBe(1);
  });

  it('a collapsed face hides both ears instead of failing', () => {
    expect(earVisibility({ x: 0, y: 0 }, { x: 0, y: 0 }, { x: 0, y: 0 })).toEqual({ right: 0, left: 0 });
  });
});

describe('necklace', () => {
  it('sits below the chin along the face line, scaled by face width', () => {
    const p = necklacePlacement(frontFace(), 0.5);
    near(p.x, 200);
    near(p.y, 320 + 240 * TUNING.necklaceDrop);
    near(p.width, 200 * TUNING.necklaceWidth);
    near(p.height, p.width * 0.5);
    near(p.rotation, 0);
    expect([p.anchorX, p.anchorY]).toEqual([0.5, 0]);
  });

  it('follows half of a head tilt', () => {
    // Rotate the whole face 20 degrees around the chin.
    const a = (20 * Math.PI) / 180;
    const chin = { x: 200, y: 320 };
    const face = frontFace().map(p => ({
      x: chin.x + (p.x - chin.x) * Math.cos(a) - (p.y - chin.y) * Math.sin(a),
      y: chin.y + (p.x - chin.x) * Math.sin(a) + (p.y - chin.y) * Math.cos(a)
    }));
    const p = necklacePlacement(face, 0.5);
    near(p.rotation, a * TUNING.necklaceRollFollow);
    // The drop follows the tilted face line, so it moves sideways too.
    near(p.x, chin.x - Math.sin(a) * 240 * TUNING.necklaceDrop);
  });
});

describe('ring', () => {
  it('sits between the ring finger base and middle joint, scaled by finger width', () => {
    const p = ringPlacement(uprightHand(), 0.5);
    near(p.x, 200);
    near(p.y, 400 - 100 * TUNING.ringAlong);
    const finger = 40 * TUNING.fingerWidthShare;
    near(p.width, finger * TUNING.ringWidth);
    near(p.height, p.width * 0.5);
    expect([p.anchorX, p.anchorY]).toEqual([0.5, 0.5]);
  });

  it('a finger pointing up leaves the image unrotated (image top toward the fingertip)', () => {
    near(ringPlacement(uprightHand(), 0.5).rotation, 0);
  });

  it('a finger pointing right turns the image a quarter turn clockwise', () => {
    const hand = landmarks(21, {
      [HAND.middleBase]: { x: 100, y: 160 }, [HAND.ringBase]: { x: 100, y: 200 },
      [HAND.ringMiddle]: { x: 200, y: 200 }, [HAND.littleBase]: { x: 100, y: 240 }
    });
    const p = ringPlacement(hand, 0.5);
    near(p.rotation, Math.PI / 2);
    // The band (image width) must lie across the finger: its corners spread along y, not x.
    const corners = placementCorners(p);
    const spreadY = Math.max(...corners.map(c => c.y)) - Math.min(...corners.map(c => c.y));
    near(spreadY, p.width);
  });

  it('fades out when the finger points at the camera', () => {
    const hand = uprightHand();
    hand[HAND.ringMiddle] = { x: 200, y: 395 };
    expect(ringPlacement(hand, 0.5).opacity).toBe(0);
    expect(ringPlacement(uprightHand(), 0.5).opacity).toBe(1);
  });
});

describe('placementCorners', () => {
  it('an unrotated top center anchor puts the image below and centered on the point', () => {
    const corners = placementCorners({ x: 100, y: 50, width: 20, height: 40, rotation: 0, opacity: 1, anchorX: 0.5, anchorY: 0 });
    expect(corners).toEqual([{ x: 90, y: 50 }, { x: 110, y: 50 }, { x: 110, y: 90 }, { x: 90, y: 90 }]);
  });
});

describe('LandmarkSmoother', () => {
  it('passes the first frame through, then moves part of the way each frame', () => {
    const s = new LandmarkSmoother(0.5);
    expect(s.smooth([{ x: 0, y: 0 }])).toEqual([{ x: 0, y: 0 }]);
    expect(s.smooth([{ x: 10, y: 20 }])).toEqual([{ x: 5, y: 10 }]);
    expect(s.smooth([{ x: 10, y: 20 }])).toEqual([{ x: 7.5, y: 15 }]);
  });

  it('cuts jitter: a point shaking by plus or minus 4 px moves much less', () => {
    const s = new LandmarkSmoother(0.3);
    const out: number[] = [];
    for (let i = 0; i < 40; i++) out.push(s.smooth([{ x: 100 + (i % 2 ? 4 : -4), y: 0 }])[0].x);
    const late = out.slice(20);
    expect(Math.max(...late) - Math.min(...late)).toBeLessThan(4);
  });

  it('adaptive: a small shake is smoothed, a big move passes through at once', () => {
    const s = new LandmarkSmoother(0.4, 12);
    s.smooth([{ x: 0, y: 0 }, { x: 10, y: 0 }]);
    // Average move 2 px: alpha = 0.4 + 0.6 * 2 / 12 = 0.5.
    expect(s.smooth([{ x: 2, y: 0 }, { x: 12, y: 0 }])).toEqual([{ x: 1, y: 0 }, { x: 11, y: 0 }]);
    // Average move about 30 px, above 12: no smoothing, no lag.
    expect(s.smooth([{ x: 31, y: 0 }, { x: 41, y: 0 }])).toEqual([{ x: 31, y: 0 }, { x: 41, y: 0 }]);
  });

  it('adaptive: keeps the shape, every point gets the same share', () => {
    const s = new LandmarkSmoother(0.4, 12);
    s.smooth([{ x: 0, y: 0 }, { x: 100, y: 0 }]);
    const [a, b] = s.smooth([{ x: 4, y: 0 }, { x: 104, y: 0 }]);
    near(b.x - a.x, 100);
  });

  it('reset forgets the old position', () => {
    const s = new LandmarkSmoother(0.5);
    s.smooth([{ x: 0, y: 0 }]);
    s.reset();
    expect(s.smooth([{ x: 50, y: 50 }])).toEqual([{ x: 50, y: 50 }]);
  });

  it('starts fresh when the number of points changes', () => {
    const s = new LandmarkSmoother(0.5);
    s.smooth([{ x: 0, y: 0 }]);
    expect(s.smooth([{ x: 8, y: 8 }, { x: 1, y: 1 }])).toEqual([{ x: 8, y: 8 }, { x: 1, y: 1 }]);
  });
});
