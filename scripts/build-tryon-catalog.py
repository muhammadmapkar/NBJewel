# Role: Builds the customer try-on catalog from the product photos in "Jewellery Images".
# For each photo it removes the background (cutout-jewelry.py), cuts out each piece of a set
# (necklace, left and right earring, tikka) as its own transparent PNG, makes a card image, and
# writes customer-tryon/public/catalog/catalog.json. Names are placeholders (assumed); the
# photos are watermarked stock previews used for the demo only.
# Usage, from the repository root: python3 scripts/build-tryon-catalog.py
# Pieces are picked by a seed point (x, y in the photo) on the cut-out mask, or by a box when a
# piece touches another one in the photo; "erase" boxes clear bits of a touching piece. Fit numbers tune how a piece sits (assumed, by eye).

import importlib.util
import json
from pathlib import Path

import numpy as np
from PIL import Image
from scipy import ndimage

ROOT = Path(__file__).resolve().parent.parent
PHOTOS = ROOT / "Jewellery Images"
OUT = ROOT / "customer-tryon" / "public" / "catalog"

spec = importlib.util.spec_from_file_location("cutout", ROOT / "scripts" / "cutout-jewelry.py")
cutout = importlib.util.module_from_spec(spec)
spec.loader.exec_module(cutout)

# Each product: photo file, name, and pieces. A piece is selected by "seed" (component under the
# point) or "box" (x0, y0, x1, y1). Earrings come as a left and right image.
PRODUCTS = [
    {
        "id": "temple-jhumka-necklace", "photo": "Pasted 2026-11-01 at 11.10.38 AM.png",
        "name": "Temple Jhumka Necklace", "note": "Antique gold with a jhumka drop",
        "pieces": {"necklace": {"all": True, "fit": {"width": 1.45, "drop": 0.05}}},
    },
    {
        "id": "antique-pendant-set", "photo": "Pasted 2026-11-01 at 11.11.17 AM.png",
        "name": "Antique Pendant Set", "note": "Beaded necklace with matching drops",
        "pieces": {
            "necklace": {"seed": (280, 350), "fit": {"width": 1.45, "drop": 0.10}},
            "earringLeft": {"seed": (232, 190)},
            "earringRight": {"box": (285, 126, 372, 266), "erase": [(345, 118, 380, 140)]},
        },
    },
    {
        "id": "filigree-bridal-choker", "photo": "Pasted 2026-11-01 at 11.11.26 AM.png",
        "name": "Filigree Bridal Choker Set", "note": "Fine mesh choker with long drops",
        "pieces": {
            "necklace": {"seed": (280, 250), "fit": {"width": 1.25, "drop": -0.06}},
            "earringLeft": {"seed": (85, 330)},
            "earringRight": {"seed": (468, 330)},
        },
    },
    {
        "id": "kundan-emerald-bridal", "photo": "Pasted 2026-11-01 at 11.11.57 AM.png",
        "name": "Kundan Emerald Bridal Set", "note": "Kundan with emerald drops, earrings and tikka",
        "pieces": {
            "necklace": {"seed": (180, 200), "fit": {"width": 1.45, "drop": 0.04}},
            "earringLeft": {"seed": (48, 250)},
            "earringRight": {"seed": (115, 300)},
            "tikka": {"box": (140, 0, 220, 146)},
        },
    },
    {
        "id": "kundan-medallion-long-set", "photo": "Pasted 2026-11-01 at 11.45.05 AM.png",
        "name": "Kundan Medallion Long Set", "note": "Long haar with a kundan medallion",
        "pieces": {
            "necklace": {"seed": (275, 300), "fit": {"width": 1.25, "drop": -0.15}},
            "earringLeft": {"seed": (82, 380)},
            "earringRight": {"seed": (470, 380)},
        },
    },
    {
        "id": "gold-teardrop-earrings", "photo": "Pasted 2026-11-01 at 11.45.15 AM.png",
        "name": "Gold Teardrop Earrings", "note": "Filigree teardrops with a floral stud",
        "pieces": {"earringLeft": {"seed": (180, 280)}, "earringRight": {"seed": (388, 280)}},
    },
]


def select(alpha: np.ndarray, rule: dict) -> np.ndarray:
    """Boolean mask of the chosen piece."""
    solid = alpha > 40
    if rule.get("all"):
        return solid
    if "box" in rule:
        x0, y0, x1, y1 = rule["box"]
        mask = np.zeros_like(solid)
        mask[y0:y1, x0:x1] = solid[y0:y1, x0:x1]
        return mask
    labels, _ = ndimage.label(ndimage.binary_dilation(solid, iterations=1))
    x, y = rule["seed"]
    label = labels[y, x]
    if label == 0:
        raise ValueError(f"seed {rule['seed']} is not on a piece")
    return (labels == label) & solid


def erase(mask: np.ndarray, rule: dict) -> np.ndarray:
    """Clears small boxes, for bits of another piece (a cord) that touch the chosen one."""
    for x0, y0, x1, y1 in rule.get("erase", []):
        mask[y0:y1, x0:x1] = False
    return mask


def crop_piece(rgba: np.ndarray, mask: np.ndarray, pad: int = 2) -> Image.Image:
    ys, xs = np.nonzero(mask)
    y0, y1 = max(ys.min() - pad, 0), min(ys.max() + pad + 1, mask.shape[0])
    x0, x1 = max(xs.min() - pad, 0), min(xs.max() + pad + 1, mask.shape[1])
    piece = rgba.copy()
    piece[..., 3] = np.where(mask, piece[..., 3], 0)
    return Image.fromarray(piece[y0:y1, x0:x1], "RGBA")


def main():
    (OUT / "pieces").mkdir(parents=True, exist_ok=True)
    (OUT / "cards").mkdir(parents=True, exist_ok=True)
    catalog = []
    for product in PRODUCTS:
        rgb = np.asarray(Image.open(PHOTOS / product["photo"]).convert("RGB"))
        alpha = cutout.cutout(rgb)
        rgba = np.dstack([rgb, alpha])
        # Card image: the whole cut-out, trimmed, for the catalog grid and product page.
        card = crop_piece(rgba, alpha > 40, pad=8)
        card.thumbnail((720, 720))
        card.save(OUT / "cards" / f"{product['id']}.png", optimize=True)
        parts = {}
        for key, rule in product["pieces"].items():
            img = crop_piece(rgba, erase(select(alpha, rule), rule))
            file = f"{product['id']}-{key}.png"
            img.save(OUT / "pieces" / file, optimize=True)
            entry = {"image": f"catalog/pieces/{file}", "aspect": round(img.height / img.width, 4)}
            if "fit" in rule:
                entry["fit"] = rule["fit"]
            parts[key] = entry
            print(f"{file}: {img.width}x{img.height}")
        kinds = (["necklace"] if "necklace" in parts else []) + (["earrings"] if "earringLeft" in parts else []) \
            + (["tikka"] if "tikka" in parts else [])
        catalog.append({
            "id": product["id"], "name": product["name"], "note": product["note"],
            "category": "set" if len(kinds) > 1 else kinds[0],
            "kinds": kinds, "card": f"catalog/cards/{product['id']}.png", "parts": parts,
        })
    (OUT / "catalog.json").write_text(json.dumps(catalog, indent=2) + "\n")
    print("wrote", OUT / "catalog.json", len(catalog), "products")


if __name__ == "__main__":
    main()
