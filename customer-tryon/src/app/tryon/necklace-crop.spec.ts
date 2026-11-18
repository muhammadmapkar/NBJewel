// Role: Verifies that camera fitting crops a closed necklace while keeping open designs intact.
import { describe, expect, it } from 'vitest';
import * as fitting from './necklace-crop';

function silhouette(draw: (x: number, y: number) => boolean) {
  const width = 100;
  const height = 100;
  const data = new Uint8ClampedArray(width * height * 4);
  for (let y = 0; y < height; y++) {
    for (let x = 0; x < width; x++) {
      if (draw(x, y)) data[(y * width + x) * 4 + 3] = 255;
    }
  }
  return { width, height, data };
}

const loop = (x: number, y: number) => {
  const outer = ((x - 50) / 38) ** 2 + ((y - 43) / 34) ** 2;
  const inner = ((x - 50) / 34) ** 2 + ((y - 43) / 30) ** 2;
  return (outer <= 1 && inner >= 1) || (Math.abs(x - 50) < 5 && y >= 76 && y < 90);
};

describe('necklaceFrontCrop', () => {
  it('cuts through the opening below the clasp and keeps the front and pendant', () => {
    const top = fitting.necklaceFrontCrop(silhouette(loop));
    expect(top).toBeGreaterThan(0.18);
    expect(top).toBeLessThan(0.4);
  });

  it('keeps an already open necklace', () => {
    const open = silhouette((x, y) => y >= 25 && loop(x, y));
    expect(fitting.necklaceFrontCrop(open)).toBe(0);
  });

  it('keeps a small ornamental hole from triggering a crop', () => {
    const charm = silhouette((x, y) => {
      const radius = Math.hypot(x - 50, y - 37);
      return radius >= 5 && radius <= 10;
    });
    expect(fitting.necklaceFrontCrop(charm)).toBe(0);
  });
});
