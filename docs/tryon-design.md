# Virtual try-on: design notes

A customer facing 2D jewelry try-on, separate from the ERP chatbot. A shop owner uploads jewelry images at `/owner/jewelry`. A customer opens `/tryon` on a phone, picks a piece, allows the camera, and sees the piece on their face or hand in real time.

Sample data only. The 6 seeded items are generated placeholder drawings (`scripts/generate-jewelry-seed.py`), not real products or brand images.

## How it works

```
Phone browser                                         .NET API (NbJewel.Api)
-------------                                         ----------------------
/tryon grid  ---- GET /api/jewelry ---------------->  JewelryController
             <--- active items (name, type, imageUrl)  -> IJewelryRepository
/tryon/:id   ---- GET /jewelry-images/<id>.png ---->     -> FileJewelryRepository
  camera (getUserMedia, stays on the phone)              (jewelry.json + images/ under
  MediaPipe Tasks Vision (wasm, in the browser)           JEWELRY_STORAGE_PATH)
  overlay-math.ts -> canvas: video frame + jewelry
  snapshot = that canvas saved as JPG

/owner/jewelry -- X-Owner-Pin header ------------->  GET /all, POST, PATCH /active, DELETE
```

- No video or photo is sent anywhere. Face and hand tracking run in the browser. The snapshot is made from the canvas on the phone and saved or shared from there.
- The chatbot code (`backend/NbJewel.Api/Chat`) is not touched.

## Files

| Area | File | Role |
|---|---|---|
| API | `backend/NbJewel.Api/Jewelry/IJewelryRepository.cs` | Swappable data boundary (SQL or blob storage later) |
| API | `backend/NbJewel.Api/Jewelry/FileJewelryRepository.cs` | JSON metadata plus image files, seeds 6 samples on first start |
| API | `backend/NbJewel.Api/Jewelry/UploadValidator.cs` | PNG or JPEG by file signature, 5 MB limit, name and type checks |
| API | `backend/NbJewel.Api/Jewelry/OwnerPin.cs` | Compares `X-Owner-Pin` with `OWNER_PIN` (fixed time compare) |
| API | `backend/NbJewel.Api/Controllers/JewelryController.cs` | The 5 endpoints |
| Web | `frontend/src/app/features/tryon/owner-jewelry.component.ts` | Owner page |
| Web | `frontend/src/app/features/tryon/tryon-grid.component.ts` | Customer grid |
| Web | `frontend/src/app/features/tryon/tryon-camera.component.ts` | Camera try-on and snapshot |
| Web | `frontend/src/app/features/tryon/landmark-tracker.ts` | Loads MediaPipe, GPU with CPU fallback |
| Web | `frontend/src/app/features/tryon/overlay-math.ts` | Pure landmark to overlay math (unit tested) |
| Web | `frontend/src/app/features/tryon/image-tools.ts` | Transparency check and in-browser background removal |

## Endpoints

| Method and path | Who | Notes |
|---|---|---|
| `GET /api/jewelry` | public | Active items only |
| `GET /api/jewelry/all` | owner PIN | All items, also used to check the PIN |
| `POST /api/jewelry` | owner PIN | Multipart: `name`, `type` (earring, necklace, ring), `file` |
| `PATCH /api/jewelry/{id}/active` | owner PIN | Body `{ "active": true }` |
| `DELETE /api/jewelry/{id}` | owner PIN | Removes metadata and the image file |
| `GET /jewelry-images/{file}` | public | Static image files, `nosniff`, CORS for allowed origins |

A wrong or missing PIN returns 401. If `OWNER_PIN` is not set on the server, the owner endpoints return 503, so a forgotten setting never leaves them open.

## Upload rules

- The type is decided from the first bytes of the file (PNG or JPEG signature). The file name, extension and content type sent by the browser are ignored.
- 5 MB limit, checked in the browser for quick feedback and again on the server.
- The server makes the file name (`<random id>.png` or `.jpg`). The client file name is never used on disk.
- Transparent PNGs work best. If the image has no transparent pixels, the owner page offers background removal in the browser with `@imgly/background-removal`, shows the original and the cut-out side by side, and lets the owner pick which one to save.

## Image layout the overlay expects (assumed)

- Earring: hook at the top center of the image, piece hanging down.
- Necklace: the two chain ends on the top edge, the chain and pendant hanging below.
- Ring: band drawn level across the image center, top of the image toward the fingertip.

Owner uploads should follow the same layout. The seeded samples do.

## Landmarks and placement

MediaPipe Face Landmarker (478 points) for earrings and necklaces, Hand Landmarker (21 points) for rings.

| Piece | Landmarks | Position | Scale | Rotation |
|---|---|---|---|---|
| Earring | 132 and 361 (earlobe height), 234 and 454 (face edges), 1 (nose) | Lobe point pushed 5 percent of face width outward | 0.18 x face width | None, earrings hang straight |
| Necklace | 152 (chin), 10 (forehead), 234 and 454 | 22 percent of face height below the chin, along the forehead to chin line | 1.3 x face width | Half of the head tilt |
| Ring | 13 and 14 (ring finger base and middle joint), 9 and 17 | 40 percent of the way from 13 to 14 | 1.3 x finger width (finger width = 0.8 x the gap between finger bases) | Follows the finger direction |

Index check. The task suggested 234 and 454 for the ears. Running Face Landmarker on a test portrait in headless Chromium showed that 234 and 454 sit on the face edge at eye level (near the top of the ear), not at the lobe. 132 and 361 sit on the face edge at lobe height, so the earring hook uses those and 234 and 454 are kept for the face width and ear visibility. For rings, 13 is the ring finger base (MCP) and 14 the middle joint (PIP), so "between 13 and 14" is where a ring is worn. This was checked on MediaPipe's own sample hand photos, finger up and finger down.

Ear hiding: when the head turns, the face side that turns away gets narrow on screen. Each ear's share of the nose to face edge distances is used: 35 percent or more shows the earring fully, 25 percent or less hides it, with a fade in between.

Ring fade: when the finger points at the camera, the 13 to 14 segment looks very short, and the ring fades out instead of drawing a wrong shape.

Smoothing: a simple low-pass filter (exponential smoothing, half of the new position per frame) on all landmarks. It resets when the face or hand is lost for about half a second, so the next one does not slide in from the old place.

All tuning numbers are in `TUNING` in `overlay-math.ts`. They were chosen by eye on test photos (assumed) and should be tuned on real phones with real jewelry photos.

## MediaPipe files (pinned)

- Library: `@mediapipe/tasks-vision` 1.1.1 from npm (exact version in `package.json`).
- Wasm runtime: copied from `node_modules/@mediapipe/tasks-vision/wasm` into the build at `/mediapipe/wasm` (see `angular.json`). It is served from our own host, so it always matches the library version.
- Models: loaded from Google's pinned model URLs (version 1, float16):
  - `https://storage.googleapis.com/mediapipe-models/face_landmarker/face_landmarker/float16/1/face_landmarker.task`
  - `https://storage.googleapis.com/mediapipe-models/hand_landmarker/hand_landmarker/float16/1/hand_landmarker.task`
- GPU first. If the GPU fails to start, the CPU is used. If the GPU starts but finds nothing for about 2 seconds, a CPU copy is also loaded and takes over if it finds the face or hand. This was needed in headless Chromium, where the GPU path found nothing without any error.

Background removal (`@imgly/background-removal` 1.7.0) loads its model files from `staticimgly.com` when the owner presses "Remove background". It is loaded only then.

## Assumptions

- Assumed: one shared owner PIN in `OWNER_PIN` is enough for a demo. Replace with real auth later (per user sign in, roles, rate limiting).
- Assumed: the customer page sits outside the ERP shell so customers do not see the ERP menu or the chatbot. It uses the same theme colors and button styles. The owner page sits inside the shell with a new "Try-On Jewelry" menu entry.
- Assumed: placement and size numbers above. Not tuned on real customers.
- Assumed: front camera by default for all types, with a flip button.

## Known limits

- 2D overlay only. No 3D, no lighting match, no shadows. The piece does not turn with the head (except the necklace following half of a tilt).
- Rings are the hardest. The band is drawn on top of the finger, so it cannot wrap behind it. Small fingers far from the camera give jittery sizes. The finger must face the camera.
- Lighting affects tracking. Dim rooms, strong backlight and motion blur make the face or hand drop out, and the hint "Move your face into the frame" shows.
- Hair over the ears hides real ears but the earrings still draw at the lobe point.
- Uploaded images live on the API host disk. On hosts with a temporary disk they are lost on restart (see `tryon-deploy.md`).
- The first camera start downloads the model (about 4 MB face, 8 MB hand), so it is slow on a weak connection.
- `@imgly/background-removal` is licensed AGPL 3.0 (or a paid commercial license). Fine for an internship demo; needs a decision before any production use (see `questions-for-sakthivel.md`).
- Not yet tested on a real phone. Tested in headless Chromium with a fake camera fed from test photos.
