// Role: Browser helpers for the owner upload: checks whether an image already has transparent
// pixels, and removes the background in the browser with @imgly/background-removal (loaded only
// when the owner asks, because it is large; its model files come from the imgly CDN).
// hasTransparentPixels is a pure function so it can be unit tested.

// True when enough pixels are see-through (alpha below 250) to count as a cut-out image.
// minShare is the share of all pixels, default 1 percent.
export function hasTransparentPixels(rgba: ArrayLike<number>, minShare = 0.01): boolean {
  const pixels = Math.floor(rgba.length / 4);
  if (pixels === 0) return false;
  let clear = 0;
  for (let i = 3; i < rgba.length; i += 4) if (rgba[i] < 250) clear++;
  return clear / pixels >= minShare;
}

// Reads the file into a small canvas (at most 256 px) and checks its alpha channel.
export async function fileHasTransparency(file: Blob): Promise<boolean> {
  if (file.type === 'image/jpeg') return false; // JPEG has no alpha channel.
  const bitmap = await createImageBitmap(file);
  const scale = Math.min(1, 256 / Math.max(bitmap.width, bitmap.height));
  const width = Math.max(1, Math.round(bitmap.width * scale));
  const height = Math.max(1, Math.round(bitmap.height * scale));
  const canvas = document.createElement('canvas');
  canvas.width = width;
  canvas.height = height;
  const context = canvas.getContext('2d');
  if (!context) return false;
  context.drawImage(bitmap, 0, 0, width, height);
  bitmap.close();
  return hasTransparentPixels(context.getImageData(0, 0, width, height).data);
}

// Returns a transparent PNG. Runs fully in the browser; the image is not sent to any server.
export async function removeBackground(file: Blob): Promise<Blob> {
  const module = await import('@imgly/background-removal');
  return module.removeBackground(file, { output: { format: 'image/png' } });
}
