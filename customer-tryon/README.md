# NB Jewel customer try-on

A separate, customer facing web app (not part of the ERP). Customers open it on a phone, pick a
design, allow the camera, and wear the jewelry live. The video never leaves the phone.

Demo only: the 6 designs are watermarked stock previews from `Jewellery Images/`, with placeholder
names (assumed). Replace them with licensed or NB Jewel's own product photos before showing it to
real customers.

## What it does

- Catalog with filters (All, Sets, Necklaces, Earrings). A set also shows under each kind it contains.
- Design page with "Try the full set" and "<piece> only" buttons.
- Live try-on: necklace under the chin, earrings at the lobes, maang tikka at the hairline.
  - Chips switch the pieces of a set on and off. A strip switches designs without closing the camera.
  - Physics: each piece is a damped pendulum hanging from a moving point (lobe, hairline, neck),
    at real scale (gravity 9.81 m/s2, face width taken as 14 cm). Earrings lag, swing and settle
    when the head moves; the necklace has a spring back to the neck, so it only sways a little.
  - Earrings fade out on the side the head turns away from.
  - Jewelry hides 0.25 s after the face is lost, so it never floats where the face was.
  - Shutter saves the camera frame plus jewelry as a JPG (Save, or Share on phones that support it).
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
| `src/app/pages/home.page.ts` | Catalog grid and filters |
| `src/app/pages/product.page.ts` | One design and its try-on buttons |
| `src/app/pages/tryon.page.ts` | Camera, detection loop, drawing, pieces, design strip, snapshot |
| `src/app/tryon/overlay-math.ts` | Landmarks to position and size of each piece (unit tested) |
| `src/app/tryon/physics.ts` | Pendulum physics for hanging pieces (unit tested) |
| `src/app/tryon/landmark-tracker.ts` | MediaPipe Face Landmarker, GPU with CPU fallback |
| `src/app/catalog/` | Catalog types, filter rules (unit tested) and loading |
| `scripts/cutout-jewelry.py` (repo root) | Background removal for product photos |
| `scripts/build-tryon-catalog.py` (repo root) | Cuts each piece out of the photos and writes the catalog |

MediaPipe: `@mediapipe/tasks-vision` 1.1.1 pinned in `package.json`. The wasm files are copied from
`node_modules` into the build at `/mediapipe/wasm`; the face model loads from Google's pinned URL
(`face_landmarker/float16/1`).

Each camera frame is drawn to the canvas first and the face is found in that drawn frame, so the
jewelry is always drawn on the frame it was found in (no trailing on slow phones).

## Known limits

- 2D pictures: no 3D turn, lighting match or shadow. A piece does not wrap behind the neck or hair.
- Sizes use an average face width (14 cm); a very small or large face changes the real size shown.
- Earrings are drawn over hair; long hair over the ears is not cut out.
- The headless test browser cannot run MediaPipe's GPU face detector, so tests used the CPU path
  for jumps. Real phone GPU tracking was confirmed by hand on the earlier version.
- Tested in headless Chromium with a fake camera fed test photos, not yet on many real phones.
