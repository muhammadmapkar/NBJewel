# NB Jewel customer try-on

A separate, customer facing web app (not part of the ERP). Customers open it on a phone, pick a
design, allow the camera, and wear the jewelry live. The video never leaves the phone.

Demo only: the 6 designs are watermarked stock previews from `Jewellery Images/`, with placeholder
names (assumed). Replace them with licensed or NB Jewel's own product photos before showing it to
real customers.

## What it does

A demo jewellery shop for customers, built around the live try-on. All designs, names, prices,
specifications, gold rates and stores are SAMPLE data (assumed), and the app says so.

- Home: hero, collections (Bridal, Temple, Antique, Daily wear), bestsellers, new arrivals, how try-on works.
- Catalogue (/shop): type chips, Filter panel (type, gold purity, price, weight, stone, occasion),
  sort (popular, price low or high, newest), result count. A set also shows under each kind it contains.
- Product page: swipe pictures (the set and each piece) with tap to zoom, item code, price with VAT,
  Try on (full set or one piece alone), Add to bag, wishlist, Enquire on WhatsApp, specifications
  (metal, purity, gross and net weight, stones, size, hallmark, making charge, occasion), price
  breakdown (gold x sample rate, making charge, stones, VAT), description, similar designs.
- Bag and wishlist (kept in this browser only). Reserve in store (name, mobile, store, date) gives a
  reference number; nothing is sent to a real store (demo). Enquire on WhatsApp opens a prefilled chat.
- Live try-on with 3D effects:
  - Real-scale physics in two directions: pieces swing side to side and forward and back (head
    moving closer, farther, or turning), lag and settle. The necklace only sways a little.
  - Turning with the head: earrings narrow as the head turns (depth from MediaPipe landmarks).
  - Hiding: an earring is cut where it passes behind the face outline; the necklace chain ends
    fade where they go behind the neck.
  - Light and shadow: pieces dim in a dark room and take a little of the room colour (measured on
    the skin); a soft shadow falls under each piece.
  - Necklaces rest at the base of the neck: the catalog build finds each necklace's inner front edge.
  - Chips switch pieces of a set; a strip switches designs; price and Add to bag stay in view;
    shutter saves a photo (Save, or Share where supported).
  - Jewelry hides 0.25 s after the face is lost, so it never floats where the face was.
  - Friendly messages: no https, camera blocked, no camera, camera busy, model failed to load, no face.

## Run it

```bash
cd customer-tryon
npm install
npm start
```

Open http://localhost:4300 (the camera works on localhost without https). Tests and build:

```bash
npm test
npm run build
```

`?cpu=1` at the end of a try-on address forces MediaPipe's CPU path, for a phone whose GPU path
misbehaves.

## Deploy (Vercel, from a phone or a computer)

1. Vercel, Add New, Project, import the repository.
2. Root Directory: `customer-tryon`. `vercel.json` sets the build command and output folder.
3. No environment variables are needed: the catalog and images are bundled in the app.
4. Deploy, then open `https://<project>.vercel.app` on the phone.

If the import screen has no branch picker, Vercel builds the default branch first. Then use
Settings, Environments, Production, Branch Tracking to choose the branch with this folder.

## Update the catalog

1. Put photos in `Jewellery Images/` (white, grey or checkerboard background works best).
2. Add or edit an entry in `PRODUCTS` in `scripts/build-tryon-catalog.py`: a name, and for each piece a
   seed point (x, y in the photo) or a box. `fit` tunes the necklace width and drop (assumed, by eye).
3. Run `python3 scripts/build-tryon-catalog.py` (needs Pillow, numpy, scipy). It writes the cut-outs
   and `public/catalog/catalog.json`.

## How it is built

| File | Role |
|---|---|
| `src/app/pages/home.page.ts` | Home: hero, collections, bestsellers, new arrivals |
| `src/app/pages/shop.page.ts` | Catalogue with filter panel and sort |
| `src/app/pages/product.page.ts` | Product page: gallery, specs, price breakdown, buttons |
| `src/app/pages/bag.page.ts`, `wishlist.page.ts`, `reserved.page.ts` | Bag with reserve form, wishlist, confirmation |
| `src/app/pages/tryon.page.ts` | Camera, detection loop, pieces, design strip, Add to bag, snapshot |
| `src/app/tryon/jewelry-renderer.ts` | Draws pieces with 3D swing, head turn, hiding, light and shadow |
| `src/app/tryon/overlay-math.ts` | Landmarks to position and size of each piece (unit tested) |
| `src/app/tryon/physics.ts` | Pendulum physics for hanging pieces (unit tested) |
| `src/app/tryon/pose.ts` | Head turn and tilt, depth, light, face outline (unit tested) |
| `src/app/tryon/landmark-tracker.ts` | MediaPipe Face Landmarker, GPU with CPU fallback |
| `src/app/catalog/catalog.models.ts` | Catalog types, price, filters, sort (unit tested) |
| `src/app/catalog/store.service.ts` | Bag, wishlist and reservation, in this browser only |
| `src/app/ui/` | Header, product card and icons |
| `scripts/cutout-jewelry.py` (repo root) | Background removal for product photos |
| `scripts/build-tryon-catalog.py` (repo root) | Cuts each piece out of the photos and writes the catalog |

MediaPipe: `@mediapipe/tasks-vision` 1.1.1 pinned in `package.json`. The wasm files are copied from
`node_modules` into the build at `/mediapipe/wasm`; the face model loads from Google's pinned URL
(`face_landmarker/float16/1`).

Each camera frame is drawn to the canvas first and the face is found in that drawn frame, so the
jewelry is always drawn on the frame it was found in (no trailing on slow phones).

## Known limits

- Pieces are flat photos given 3D behaviour: there is no back view, and a piece does not bend.
- Hair falling over the ears is not detected, so an earring is drawn over such hair.
- A hand in front of the neck is not detected, so the necklace is drawn over the hand.
- Prices, specs, stores, reservations and the WhatsApp number are demo placeholders (assumed).
- Sizes use an average face width (14 cm); a very small or large face changes the real size shown.
- Earrings are drawn over hair; long hair over the ears is not cut out.
- The headless test browser cannot run MediaPipe's GPU face detector, so tests used the CPU path
  for jumps. Real phone GPU tracking was confirmed by hand on the earlier version.
- Tested in headless Chromium with a fake camera fed test photos, not yet on many real phones.
