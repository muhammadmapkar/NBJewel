// Role: Pure 3D helpers for the try-on, from MediaPipe face landmarks that include depth (z):
// which way the head is turned (yaw) and tilted (pitch), how far the face is from the camera,
// how much a flat piece should narrow when turned, the face outline used to hide a piece that
// goes behind the face, and the room light measured on the skin. Unit tested in pose.spec.ts.
//
// MediaPipe z is in the same scale as x (normalized by image width); a smaller z is nearer the
// camera. toPixels3 turns all three into canvas pixels.

import { FACE, Point, clamp } from './overlay-math';

export interface Point3 extends Point { z: number }

export function toPixels3(landmark: Point3, width: number, height: number): Point3 {
  return { x: landmark.x * width, y: landmark.y * height, z: landmark.z * width };
}

// Face outline (MediaPipe face oval), in order around the face.
export const FACE_OVAL: readonly number[] = [10, 338, 297, 332, 284, 251, 389, 356, 454, 323, 361, 288, 397, 365, 379, 378, 400, 377,
  152, 148, 176, 149, 150, 136, 172, 58, 132, 93, 234, 127, 162, 21, 54, 103, 67, 109];

export function facePolygon(face: readonly Point[]): Point[] { return FACE_OVAL.map(i => face[i]); }

export interface HeadPose {
  yaw: number; // radians; positive when the screen-right side of the face turns away
  pitch: number; // radians; positive when the chin is farther than the forehead (looking up)
}

// Yaw from the depth difference across the face (234 to 454), pitch from forehead to chin.
export function headPose(face: readonly Point3[]): HeadPose {
  const a = face[FACE.rightEdge];
  const b = face[FACE.leftEdge];
  const [left, right] = a.x <= b.x ? [a, b] : [b, a];
  const yaw = Math.atan2(right.z - left.z, Math.max(right.x - left.x, 1e-6));
  const top = face[FACE.forehead];
  const chin = face[FACE.chin];
  const pitch = Math.atan2(chin.z - top.z, Math.max(chin.y - top.y, 1e-6));
  return { yaw, pitch };
}

// How wide a flat piece looks when turned by an angle (never thinner than minScale).
export function turnScale(angle: number, minScale = 0.35): number { return clamp(Math.cos(angle), minScale, 1); }

// Distance from the camera in metres, from the face width on screen (about 14 cm on an adult)
// and the camera focal length in pixels (assumed about 0.8 x frame width for phone cameras).
export function depthMeters(faceWidthPx: number, frameWidthPx: number, faceWidthMeters = 0.14): number {
  if (faceWidthPx <= 0) return 0;
  return (faceWidthMeters * 0.8 * frameWidthPx) / faceWidthPx;
}

export interface Light { brightness: number; tint: { r: number; g: number; b: number } }

// Room light measured on skin pixels (RGBA bytes). A well lit face has a luminance of about 0.6;
// darker rooms dim the jewelry (never below 0.6), very bright ones lift it a little (up to 1.1).
// The tint is the average skin colour, used lightly so gold picks up the room's colour.
export function lightFromPixels(rgba: ArrayLike<number>): Light {
  let r = 0, g = 0, b = 0, n = 0;
  for (let i = 0; i + 3 < rgba.length; i += 4) { r += rgba[i]; g += rgba[i + 1]; b += rgba[i + 2]; n++; }
  if (n === 0) return { brightness: 1, tint: { r: 255, g: 255, b: 255 } };
  r /= n; g /= n; b /= n;
  const luminance = (0.2126 * r + 0.7152 * g + 0.0722 * b) / 255;
  return { brightness: clamp(luminance / 0.6, 0.6, 1.1), tint: { r: Math.round(r), g: Math.round(g), b: Math.round(b) } };
}

// Eases a light reading toward the new one, so the jewelry does not flicker.
export function blendLight(previous: Light, next: Light, share = 0.2): Light {
  const mix = (a: number, b: number) => a + (b - a) * share;
  return {
    brightness: mix(previous.brightness, next.brightness),
    tint: { r: Math.round(mix(previous.tint.r, next.tint.r)), g: Math.round(mix(previous.tint.g, next.tint.g)), b: Math.round(mix(previous.tint.b, next.tint.b)) }
  };
}

// True when the point is inside the polygon (even-odd rule).
export function insidePolygon(p: Point, polygon: readonly Point[]): boolean {
  let inside = false;
  for (let i = 0, j = polygon.length - 1; i < polygon.length; j = i++) {
    const a = polygon[i], b = polygon[j];
    if ((a.y > p.y) !== (b.y > p.y) && p.x < ((b.x - a.x) * (p.y - a.y)) / (b.y - a.y) + a.x) inside = !inside;
  }
  return inside;
}
