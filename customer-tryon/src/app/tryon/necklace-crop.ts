// Role: Finds a camera-only top crop for a cutout showing a complete necklace loop.
export const MAX_CAMERA_TRIM_PERCENT = 49;

export function isCameraTrimPercent(value: unknown): value is number {
  return typeof value === 'number' && Number.isInteger(value) && value >= 0 && value <= MAX_CAMERA_TRIM_PERCENT;
}

export function detectCameraTrimPercent(image: HTMLImageElement): number {
  const { naturalWidth, naturalHeight } = image;
  if (!naturalWidth || !naturalHeight) return 0;
  const sample = document.createElement('canvas');
  const scale = Math.min(1, 128 / Math.max(naturalWidth, naturalHeight));
  sample.width = Math.max(1, Math.round(naturalWidth * scale));
  sample.height = Math.max(1, Math.round(naturalHeight * scale));
  const context = sample.getContext('2d', { willReadFrequently: true });
  if (!context) return 0;
  try {
    context.drawImage(image, 0, 0, sample.width, sample.height);
    const crop = necklaceFrontCrop(context.getImageData(0, 0, sample.width, sample.height));
    return Math.min(MAX_CAMERA_TRIM_PERCENT, Math.round(crop * 100));
  } catch { return 0; }
}

export function necklaceFrontCrop({ data, width, height }: Pick<ImageData, 'data' | 'width' | 'height'>): number {
  const size = width * height;
  const seen = new Uint8Array(size);
  const queue = new Int32Array(size);
  const rows = new Uint16Array(height);
  const center = Math.floor(width / 2);
  let tail = 0;
  const visit = (index: number) => {
    if (seen[index] || data[index * 4 + 3] >= 64) return;
    seen[index] = 1;
    queue[tail++] = index;
  };

  for (let start = 0; start < size; start++) {
    tail = 0;
    visit(start);
    if (tail === 0) continue;
    let left = width;
    let right = 0;
    let top = height;
    let bottom = 0;
    rows.fill(0);
    for (let head = 0; head < tail; head++) {
      const index = queue[head];
      const x = index % width;
      const y = Math.floor(index / width);
      left = Math.min(left, x);
      right = Math.max(right, x);
      top = Math.min(top, y);
      bottom = Math.max(bottom, y);
      rows[y]++;
      if (x > 0) visit(index - 1);
      if (x < width - 1) visit(index + 1);
      if (y > 0) visit(index - width);
      if (y < height - 1) visit(index + width);
    }

    // The center opening of a closed necklace is large, enclosed, and starts near the top.
    // An open chain connects to the outside. Small closed details are part of the jewellery.
    if (left === 0 || right === width - 1 || top === 0 || bottom === height - 1 ||
        left >= center || right <= center || top > height * 0.4 || tail < size * 0.08) continue;
    const peakWidth = Math.max(...rows);
    if (peakWidth < width * 0.35) continue;
    const startOfFront = top + Math.ceil((bottom - top) * 0.15);
    const row = rows.findIndex((count, y) => y >= startOfFront && count >= peakWidth * 0.6);
    if (row > 0 && row < height * 0.5) return row / height;
  }
  return 0;
}
