<!-- Role: Explains the assumed single-origin hosted builds, owner-run Vercel/API setup and the limits of local preparation evidence. -->
# Combined ERP and storefront hosting

Repository preparation is complete for the assumed layout below. Nothing was deployed, no SQL was executed and no Docker image was built or run by the agent. Independent portal security/code review remains pending for Claude.

| Public path | Serves |
| --- | --- |
| `/erp/sign-in`, `/erp/web-portal`, `/erp/catalog/...` | ERP Angular app |
| `/shops/nb-jewel-demo`, `/shops/{slug}/...` | Customer Angular app |
| `/api/...` | Shared .NET API |
| `/jewelry-images/...` | Existing API-authorized public legacy images |

Use the existing NBJewel repository and branch `feature/customer-storefront`. Create separate ERP and storefront Vercel projects. The storefront project is the public gateway. Each business remains a shop path; creating another business does not require another frontend project.

## 1. Vercel project settings

| Setting | ERP project | Storefront/gateway project |
| --- | --- | --- |
| Root Directory | `frontend` | `customer-tryon` |
| Framework Preset | Other | Other |
| Node version | 24.x | 24.x |
| Install Command | `npm ci` | `npm ci` |
| Build Command | `npm run build:deploy` | `npm run build:deploy` |
| Output Directory override | Unset/off | Unset/off |
| Include source files outside Root Directory | Enabled | Enabled |
| Production Branch | `feature/customer-storefront` | `feature/customer-storefront` |

The shared build scripts live in the repository's `scripts/` directory, so outside-root source access is required. Set the production branch under Settings, Environments, Production, Branch Tracking. This does not require changing the GitHub default branch or merging branches.

The committed Vercel files select Framework Other and the hosted build command. The build emits the standard **Vercel Build Output API v3** structure under each app's `.vercel/output/`, including routing. Vercel serves that artifact. Leave an old dashboard Output Directory override disabled; do not replace the generated routing with the previous catch-all-only configuration.

Official references: [monorepo roots](https://vercel.com/docs/monorepos), [production branch](https://vercel.com/docs/git#customizing-the-production-branch), [Build Output API](https://vercel.com/docs/build-output-api), [routing configuration](https://vercel.com/docs/build-output-api/configuration).

## 2. Nonsecret build settings

Set these in the appropriate Vercel environment before building. The URLs below are examples; use the actual assigned deployment addresses.

| Variable | ERP | Storefront/gateway | Example |
| --- | --- | --- | --- |
| `NBJEWEL_PUBLIC_ORIGIN` | Required | Required | `https://nb-jewel.vercel.app` |
| `NBJEWEL_ERP_UPSTREAM` | Unused | Required | `https://your-erp-project.vercel.app` |
| `NBJEWEL_API_UPSTREAM` | Unused | Required | `https://your-api-host.example` |

`NBJEWEL_API_URL` remains a compatibility alias for the public origin. If present alongside `NBJEWEL_PUBLIC_ORIGIN`, it must identify the same origin. Set browser API access to the public origin, not the private API upstream and not a URL ending in `/api`.

Origins must contain a scheme and host, with no path, credentials, query or fragment. HTTPS is required except for explicit HTTP loopback verification. An upstream equal to the public origin is rejected to prevent a forwarding loop. No `.env` file is loaded. No provider key or connection string belongs in these settings.

Browser configuration is generated during the build:

- ERP's generated environment points to the public API origin. Its `portal-config.json` points preview at the public storefront origin.
- Storefront's generated `storefront-config.json` points both catalog and customer requests to the public origin.
- Only explicit public settings are serialized. Backend keys/connections are never copied into these files.
- `frontend/public/portal-config.json`, `customer-tryon/public/storefront-config.json` and `frontend/src/environments/environment.ts` retain the existing local development values.
- Hosted ERP has base `/erp/`. Ordinary `npm start` and `npm run build` retain the existing root base `/`.

Generated settings are in ignored `.hosting/` folders. Hosted static files and route configuration are in ignored `.vercel/output/`. Previous generated output is retained in ignored `.vercel/previous-output-*` directories when rebuilding, rather than deleting existing files.

Configure each preview/staging environment with its own intended origins. A preview build must not accidentally use production accounts or production storage.

## 3. How routing works

The gateway routes API, ERP and legacy image paths before looking for storefront files or returning its SPA page:

1. `/erp` redirects to `/erp/sign-in`.
2. `/erp/...` goes to the ERP upstream at **the same `/erp/...` path**.
3. `/api/...` goes to the API upstream with the `/api` prefix retained.
4. `/jewelry-images/...` goes to the API's existing image endpoint.
5. Existing storefront static files are served directly; remaining storefront navigation uses its own index page.

ERP static files are physically mounted below `/erp/` in its deployment output. Its own SPA fallback also stays there. JavaScript chunks, styles, model/WASM assets and portal config therefore resolve beneath the ERP prefix. The portal settings request uses the document's base URL, and a safe Web Portal return path survives sign-in.

Missing files return 404 instead of HTML. Private API requests go to the API even if a similarly named frontend route exists. API, ERP forwarding, generated runtime configuration and SPA navigation responses use no-store. Current API authentication, CSRF and publication checks remain authoritative.

Use the public hostname for owner/customer sessions and saved draft previews. The ERP upstream address is a routing destination. Hosted cookie/HTTPS/proxy behavior still needs verification on the actual platform; a local gateway test is not evidence that cloud forwarding or TLS settings are correct.

## 4. Backend image and persistent storage

The prepared `backend/Dockerfile` uses .NET 10 Ubuntu noble images. It copies the existing `scripts/cutout-jewelry.py` into the publish context and installs distro packages for Python, Pillow, NumPy and SciPy. It sets `CUTOUT_PYTHON_PATH=/usr/bin/python3` and keeps the existing port 10000. No new background-removal engine is introduced.

`.dockerignore` excludes local API App_Data, environment files, frontend trees and generated/cache folders. Runtime data and credentials must not be baked into the image.

The operator must attach persistent storage at `/var/data`. Defaults are:

```text
BUSINESS_STORAGE_PATH=/var/data/businesses
ITEM_ASSET_STORAGE_PATH=/var/data/item-assets
JEWELRY_STORAGE_PATH=/var/data/legacy-jewelry
```

Without a mounted durable volume, those paths are still temporary container storage. Verify mount permissions and backup/restore on the chosen host. The old `render.yaml` is a historical free, temporary-disk try-on blueprint; it is not a complete production portal configuration.

**Owner-run container checks, not executed by the agent:**

```sh
docker build -f backend/Dockerfile -t nbjewel-api:hosting-check .
docker run --rm --entrypoint /usr/bin/python3 nbjewel-api:hosting-check -c "import PIL, numpy, scipy; print('Existing cutout dependencies import successfully')"
docker run --rm --entrypoint /usr/bin/python3 nbjewel-api:hosting-check scripts/cutout-jewelry.py sample-data/jewelry/necklace-gold-pendant.png /tmp/hosting-preview.png --metadata /tmp/hosting-preview.json
```

Review the generated synthetic preview separately from real permitted jewelry photos. Successful processing does not approve licensing or visual fit.

Production also needs separately configured identity/accounts, database schema/connections, persistent data-protection keys, email verification where required, trusted proxy/HTTPS configuration, approved contacts and any optional provider settings. Keep secrets only on the backend. SQL supports exactly `nb-jewel-demo`; multi-business remains Sample-verified only. Follow [SQL catalog QA](SQL_CATALOG_QA.md) and [platform deployment prerequisites](platform-deployment.md). No prepared SQL was applied during this task.

## 5. Build and local verification evidence

Hosted build commands from the repository root, with assumed example origins:

```sh
NBJEWEL_PUBLIC_ORIGIN=https://your-public-host.example NG_BUILD_MAX_WORKERS=2 npm --prefix frontend run build:deploy
NBJEWEL_PUBLIC_ORIGIN=https://your-public-host.example NBJEWEL_ERP_UPSTREAM=https://your-erp-host.example NBJEWEL_API_UPSTREAM=https://your-api-host.example NG_BUILD_MAX_WORKERS=2 npm --prefix customer-tryon run build:deploy
```

On 2026-11-16 the agent built both apps with explicit loopback origins, then served their generated output through a temporary gateway at `127.0.0.1:4301`, ERP upstream `:4201` and isolated real-Identity Sample test API `:5156`. Only synthetic accounts/data and fresh `/tmp` asset/portal folders were used. The API ran the real existing cutout script to prepare a pending synthetic preview. No owner service or database was used.

Observed in the browser:

- Entering `/erp/web-portal` as a guest redirected to `/erp/sign-in` with the safe return path retained. Owner sign-in returned to the editor.
- Refreshing the nested editor worked. Saving a draft succeeded with real cookie/CSRF requests through the public gateway.
- Mobile saved-draft preview opened at `/shops/nb-jewel-demo/preview` on the same origin; live content stayed unchanged.
- The private ERP image loaded. The agent inspected and approved only the synthetic preview, then published it and observed the public product image through `/api/shops/.../preview`.
- Authenticated product preview loaded its private image through `/api/businesses/.../portal/...`. Reload after staff sign-out showed Preview unavailable.
- Customer sign-in resumed Add to bag; the resulting bag retained the design. The same customer was denied ERP entry at `/erp/access-denied`.
- The isolated product publication was revoked; public detail became unavailable. The browser ended signed out. Temporary servers were stopped.

Read-only HTTP checks also verified nested ERP HTML, generated JSON, prefixed WASM, missing-file 404 responses, protected image/preview 401 responses and invalid-store 400 responses without a storefront fallback. Screenshot: `/tmp/nbjewel-hosting-private-preview.jpg`. HTTP log: `/tmp/nbjewel-hosting-H3-http.log`.

Automated results: Node hosting contracts 5 passed; ERP 80 passed; storefront 88 passed; both strict typechecks, ordinary builds and hosted builds passed. Clean Sample backend tests: 447 passed, 7 SQL skipped, 0 failed; backend build and Release publish passed. Published cutout script SHA256 matched source and 18 sample JSON fixtures were present. Logs: `/tmp/nbjewel-hosting-H1-*.log` and `/tmp/nbjewel-hosting-H2-*.log`.

## 6. Checks after a separately authorized deployment

- Verify actual HTTPS sessions, CSRF, forwarded scheme/IP and no-store behavior on the public hostname.
- Refresh `/erp/web-portal`, `/erp/catalog/{id}` and `/shops/nb-jewel-demo/p/{id}` directly.
- Verify runtime JSON uses the intended public origin, all prefixed ERP assets load, and missing assets return 404.
- Test private preview before/after sign-out and public product image withdrawal.
- Test the owner-run image build, cutout processing and durable volume after restart.
- Verify durable staff/customer records and intended SQL behavior, real email/provider services and permitted-photo quality separately.

Actual Vercel/Render deployment, cloud TLS/proxy behavior, container execution and production persistence have not been verified by these local checks.
