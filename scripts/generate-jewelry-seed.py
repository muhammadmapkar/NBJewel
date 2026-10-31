# Role: Generates the 6 sample try-on jewelry images (2 earrings, 2 necklaces, 2 rings)
# as simple transparent PNG placeholders in sample-data/jewelry. No real brand images.
# Run from the repository root: python3 scripts/generate-jewelry-seed.py (needs Pillow).
# Image layout rules used by the try-on overlay (assumed, see docs/tryon-design.md):
#   earring:  hook point at the top center of the image.
#   necklace: the two chain ends sit on the top edge, the chain hangs below.
#   ring:     band centered, drawn horizontally; the finger runs top to bottom through it.

import math
from pathlib import Path
from PIL import Image, ImageDraw

OUT = Path(__file__).resolve().parent.parent / "sample-data" / "jewelry"
GOLD = (212, 175, 55, 255)
GOLD_DARK = (150, 115, 30, 255)
SILVER = (205, 210, 218, 255)
SILVER_DARK = (130, 136, 148, 255)
RUBY = (190, 20, 50, 255)
EMERALD = (20, 150, 90, 255)
PEARL = (245, 240, 230, 255)


def canvas(w, h):
    return Image.new("RGBA", (w, h), (0, 0, 0, 0))


def earring_drop(metal, dark, gem):
    img = canvas(200, 400)
    d = ImageDraw.Draw(img)
    d.ellipse((85, 0, 115, 30), outline=metal, width=6)  # hook loop
    d.line((100, 30, 100, 150), fill=metal, width=6)
    d.ellipse((60, 150, 140, 330), fill=metal, outline=dark, width=5)
    d.ellipse((78, 185, 122, 295), fill=gem)
    return img


def earring_hoop(metal, dark, gem):
    img = canvas(200, 400)
    d = ImageDraw.Draw(img)
    d.ellipse((90, 0, 110, 20), fill=metal)
    d.ellipse((20, 20, 180, 260), outline=metal, width=14)
    d.ellipse((26, 26, 174, 254), outline=dark, width=2)
    d.ellipse((80, 230, 120, 280), fill=gem, outline=dark, width=3)
    return img


def necklace(metal, dark, gem, beads):
    w, h = 800, 420
    img = canvas(w, h)
    d = ImageDraw.Draw(img)
    # The chain is a U from the top left corner area to the top right corner area.
    d.arc((40, -260, 760, 300), start=10, end=170, fill=metal, width=12)
    if beads:
        for i in range(22):
            t = math.radians(14 + i * (152 / 21))
            x = 400 + 360 * math.cos(t)
            y = 20 + 280 * math.sin(t)
            d.ellipse((x - 14, y - 14, x + 14, y + 14), fill=PEARL, outline=dark, width=2)
    d.polygon([(370, 285), (430, 285), (400, 400)], fill=metal, outline=dark)
    d.ellipse((378, 300, 422, 350), fill=gem, outline=dark, width=3)
    return img


def ring(metal, dark, gem, solitaire):
    img = canvas(300, 200)
    d = ImageDraw.Draw(img)
    d.rounded_rectangle((20, 70, 280, 130), radius=28, fill=metal, outline=dark, width=5)
    d.line((30, 100, 270, 100), fill=dark, width=2)
    if solitaire:
        d.polygon([(150, 30), (190, 75), (150, 120), (110, 75)], fill=gem, outline=dark)
    else:
        for x in (105, 150, 195):
            d.ellipse((x - 16, 84, x + 16, 116), fill=gem, outline=dark, width=2)
    return img


def main():
    OUT.mkdir(parents=True, exist_ok=True)
    images = {
        "earring-gold-drop.png": earring_drop(GOLD, GOLD_DARK, RUBY),
        "earring-silver-hoop.png": earring_hoop(SILVER, SILVER_DARK, EMERALD),
        "necklace-gold-pendant.png": necklace(GOLD, GOLD_DARK, RUBY, beads=False),
        "necklace-pearl-silver.png": necklace(SILVER, SILVER_DARK, EMERALD, beads=True),
        "ring-gold-solitaire.png": ring(GOLD, GOLD_DARK, (180, 220, 255, 255), solitaire=True),
        "ring-silver-band.png": ring(SILVER, SILVER_DARK, RUBY, solitaire=False),
    }
    for name, img in images.items():
        img.save(OUT / name, "PNG")
        print("wrote", OUT / name)


if __name__ == "__main__":
    main()
