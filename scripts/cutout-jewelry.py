# Role: Turns product photos (jewelry on a white, grey or fake "checkerboard transparency"
# background) into transparent PNG cut-outs, and lists the separate pieces in each photo
# (for example a necklace and two earrings in a set photo) with their bounding boxes.
# Gold, stones and enamel are coloured; the backgrounds here are neutral (grey or white), so a
# pixel is background when it is neutral, light, and connected to the photo border, or part of
# a large neutral hole (inside a necklace curve). Small neutral areas (pearls, white stones,
# highlights) are kept. Needs Pillow, numpy and scipy.
# Usage: python3 scripts/cutout-jewelry.py <photo.png> <out.png> [--report]

import sys
import numpy as np
from PIL import Image
from scipy import ndimage

NEUTRAL_MAX_CHROMA = 30   # max(r,g,b) - min(r,g,b) at or below this is neutral (grey, white)
LIGHT_MIN_VALUE = 140     # brightness at or above this is light enough to be background
HOLE_MIN_AREA = 0.0012    # enclosed neutral areas bigger than this share of the photo are holes
EDGE_SOFTEN_CHROMA = (18, 60)  # edge pixels fade from transparent to solid across this chroma range


def plain_white_border(chroma: np.ndarray, value: np.ndarray) -> bool:
    """True when the photo border is almost all plain white (not a grey checkerboard)."""
    ring = np.concatenate([value[0], value[-1], value[:, 0], value[:, -1]])
    ring_c = np.concatenate([chroma[0], chroma[-1], chroma[:, 0], chroma[:, -1]])
    return float(((ring >= 245) & (ring_c <= 8)).mean()) > 0.9


def cutout(rgb: np.ndarray) -> np.ndarray:
    """Returns an alpha channel (0..255) for an RGB image array."""
    h, w, _ = rgb.shape
    rgb_i = rgb.astype(np.int16)
    chroma = rgb_i.max(axis=2) - rgb_i.min(axis=2)
    value = rgb_i.max(axis=2)
    if plain_white_border(chroma, value):
        # Plain white photo: only near pure white is background, so shaded pearls and white
        # stones (slightly darker or warmer than the paper) are kept.
        bg_like = (chroma <= 10) & (value >= 238)
    else:
        bg_like = (chroma <= NEUTRAL_MAX_CHROMA) & (value >= LIGHT_MIN_VALUE)

    labels, count = ndimage.label(bg_like, structure=np.ones((3, 3)))
    border = np.unique(np.concatenate([labels[0], labels[-1], labels[:, 0], labels[:, -1]]))
    sizes = ndimage.sum(bg_like, labels, index=np.arange(count + 1))
    is_bg = np.zeros(count + 1, dtype=bool)
    is_bg[border] = True
    is_bg[sizes >= HOLE_MIN_AREA * h * w] = True
    is_bg[0] = False
    background = is_bg[labels]

    alpha = np.where(background, 0, 255).astype(np.float32)
    # Soft edge: pixels next to the background fade by how coloured they are, which removes the
    # grey fringe left by anti-aliasing against the old background.
    near = ndimage.binary_dilation(background, iterations=2) & ~background
    lo, hi = EDGE_SOFTEN_CHROMA
    soft = np.clip((chroma - lo) / (hi - lo), 0, 1) * 255
    alpha[near] = np.minimum(alpha[near], soft[near])
    return ndimage.median_filter(alpha, size=3).astype(np.uint8)


def pieces(alpha: np.ndarray, min_share=0.004):
    """Bounding boxes (x0, y0, x1, y1, area) of separate pieces, largest first."""
    solid = ndimage.binary_dilation(alpha > 40, iterations=4)  # join pieces split by thin gaps
    labels, count = ndimage.label(solid)
    out = []
    for i, sl in enumerate(ndimage.find_objects(labels), start=1):
        area = int((labels[sl] == i).sum())
        if area >= min_share * alpha.size:
            out.append((sl[1].start, sl[0].start, sl[1].stop, sl[0].stop, area))
    return sorted(out, key=lambda b: -b[4])


def main():
    src, dst = sys.argv[1], sys.argv[2]
    rgb = np.asarray(Image.open(src).convert("RGB"))
    alpha = cutout(rgb)
    Image.fromarray(np.dstack([rgb, alpha]), "RGBA").save(dst)
    if "--report" in sys.argv:
        for box in pieces(alpha):
            print("piece", box)


if __name__ == "__main__":
    main()
