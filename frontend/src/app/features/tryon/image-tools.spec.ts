// Role: Unit tests for the pure transparency check used by the owner upload page.

import { describe, expect, it } from 'vitest';
import { hasTransparentPixels } from './image-tools';

// Builds RGBA bytes for n pixels with the given alpha values, repeated.
function rgba(alphas: number[]): Uint8ClampedArray {
  const data = new Uint8ClampedArray(alphas.length * 4);
  alphas.forEach((a, i) => { data[i * 4 + 3] = a; });
  return data;
}

describe('hasTransparentPixels', () => {
  it('a fully opaque image has no transparency', () => expect(hasTransparentPixels(rgba(Array(100).fill(255)))).toBe(false));
  it('a cut-out with a clear border counts as transparent', () => expect(hasTransparentPixels(rgba([...Array(50).fill(0), ...Array(50).fill(255)]))).toBe(true));
  it('a few stray clear pixels below 1 percent do not count', () => expect(hasTransparentPixels(rgba([0, ...Array(199).fill(255)]))).toBe(false));
  it('an empty image is not transparent', () => expect(hasTransparentPixels(new Uint8ClampedArray(0))).toBe(false));
});
