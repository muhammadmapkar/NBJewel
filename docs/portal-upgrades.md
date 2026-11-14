<!-- Role: Records the two portal upgrades, provider inventory, local verification and remaining configuration. -->
# Portal upgrades and local testing

Implemented on `feature/customer-storefront`, based on origin checkpoint `7720b2a`. The original accidental local run remains on `backup/local-accidental-run-20261114-113421`. This work does not merge that backup into the cloud code.

## What changed

- ERP: the rendered preview triggers server-side Gemini analysis of its exact authorized image revision. Description and collection remain editable. Late AI replies preserve edits and ignore replacement images. Re-generate is explicit. Missing config, rate limits and invalid responses leave manual entry available. AI never saves, approves or publishes a product.
- Customer portal: public landing, collection/product/rate browsing and store map; real cookie/CSRF signup/login gates on try-on, adding to bag and reservations. The interrupted design/action resumes after authentication. Profile, server-owned bag and reservation requests, account-scoped device wishlist, desktop navigation and mobile bottom navigation are implemented. Catalog source/store survives product links and reloads.
- Store map: keyless OpenStreetMap pan/zoom embed with selected store marker, optional consent-based distance ranking and clickable store catalogs. All bundled locations are assumed examples. ERP catalogs use actual published, eligible, available stock. No sample store claims live production sync.
- Pricing: GoldAPI adapter for timestamped Gold/Silver/Platinum USD pure-metal reference quotes, five-minute cache, stale/unavailable indicators. These are separate from recorded ERP selling rates and purity/making/stones/VAT calculations. No silent USD-to-AED conversion or inferred selling-rate update.
- Reservations are persisted requests awaiting store confirmation. They do not reserve ERP stock, take payment or send a real store notification. Sample customers/profile/bag/reservations reset when the API restarts. Production persistence requires the existing Identity SQL provider and the prepared customer schema.

## API keys: exact count and local status

**Two unique provider keys enable all implemented AI and live market integrations:** one shared Gemini key plus one GoldAPI key. For separate service credentials, use **three dedicated keys**: ERP Gemini, chatbot Gemini and GoldAPI. There are **four recognized API-key variable names**, because the shared Gemini key is an optional fallback. Maps, browser tracking, local cutout processing and local image storage need **zero API keys**.

Status was checked on 2026-11-14 by reading names and nonempty booleans only. Configured means present, not validated by a live provider call. Only the root `.env` exists among the application env files checked. No secret values were printed or modified. All credentials belong in root `.env` for local backend loading, or backend deployment environment/secrets storage. Neither Angular app has a secret `.env` file.

| Service / setting | Local root `.env` | Behavior if omitted |
| --- | --- | --- |
| `GEMINI_API_KEY` | Configured | Shared fallback for both Gemini clients; not needed when both dedicated keys are set |
| `GEMINI_ERP_API_KEY` | Missing; uses configured shared key | Manual copy entry if neither key exists |
| `GEMINI_CHATBOT_API_KEY` | Missing; uses configured shared key | Existing chatbot reports unavailable if neither key exists |
| `METAL_RATES_API_KEY` (GoldAPI) | Missing | Live market unavailable; recorded ERP/sample selling rates and price breakdowns still work |
| Maps key | Not used, zero required | OpenStreetMap requires internet, but no key; store list remains usable if map tiles fail |
| Cutout/storage key | Not used, zero required | Existing local Python processing and filesystem storage |

`GEMINI_MODEL` is Configured. Optional `GEMINI_ERP_MODEL` and `GEMINI_THINKING_LEVEL` are Missing: Vision uses the common model; the existing chatbot uses its default low thinking level when unset. Choose a model supporting image input and structured JSON. No provider key is embedded in public configuration or JavaScript.

## Other service configuration (not API keys)

| Integration | Exact variables / files | Local state and requirement |
| --- | --- | --- |
| ERP SQL | `Repository__Provider=Sql`, `NBJEWEL_SQL_CONNECTION` | Missing; use Sample mode. SQL schemas are prepared only, not applied |
| Identity and customer persistence | `NBJEWEL_IDENTITY_CONNECTION`; `docs/customer-portal-schema.sql` | Missing. Existing identity schema plus new CustomerPortals table are required for persistent accounts |
| AI platform/knowledge store | `AI_PLATFORM_CONNECTION` (fallback `NBJEWEL_IDENTITY_CONNECTION`), `AI_DATA_PROTECTION_SECRET` | Missing. Outside Development, requires SQL and a base64 32-byte protection secret. A Gemini key alone does not provision knowledge/conversation storage |
| Initial production ERP account | `NBJEWEL_BOOTSTRAP_EMAIL`, `NBJEWEL_BOOTSTRAP_PASSWORD` | Missing; separate from temporary local launcher identity |
| SQL container password | `MSSQL_SA_PASSWORD` | Missing; existing Docker setup only, no Docker action taken |
| Email verification | `SMTP_HOST`, `SMTP_FROM`, `SMTP_PORT`, `SMTP_USERNAME`, `SMTP_PASSWORD`, `APP_PUBLIC_ORIGIN` | All Missing. Port defaults 587. Set public customer origin and provider SMTP credentials for verification email. Do not disable production verification as a fallback |
| Local customer demo | launcher `--customers`; `SampleIdentity__Enabled`, `SampleIdentity__AllowCustomers`, runtime `SampleIdentity__StaffPassword` | Not in `.env`; generated/isolated per run. Loopback Development + Sample only |
| Cutout worker | `CUTOUT_PYTHON_PATH` | Missing in `.env`; defaults python3 with Pillow/numpy/scipy. Verification interpreter is `/tmp/nbjewel-cutout-runtime-venv/bin/python`. No separate service/API key |
| Persistent image files | `ITEM_ASSET_STORAGE_PATH`, `JEWELRY_STORAGE_PATH` | Missing in `.env`; defaults to backend App_Data directories. Launcher isolates its paths under local-sample-demo. Deployment needs persistent storage |
| Legacy jewelry owner PIN | `OWNER_PIN` | Missing; legacy owner-PIN workflow unavailable. New ERP upload uses staff authentication instead |
| Store directory / sync evidence | `STOREFRONT_STORES_PATH` | Missing; bundled illustrative directory used. Configure a reviewed JSON file with real ERP IDs/coordinates and ERP-owned inventory/image UTC sync timestamps. Both must be within 15 minutes, with ERP enabled, non-sample data and SQL provider to show live |
| Browser API addresses | `customer-tryon/public/storefront-config.json`: `apiBaseUrl`, `portalApiBaseUrl`; ERP build `NBJEWEL_API_URL` | Customer config uses bundled demo + localhost:5155 portal API. `NBJEWEL_API_URL` Configured in root `.env`, public deployment build URL only |
| Cross-origin / hosting | `CORS_ORIGINS`, `TRUSTED_PROXIES`, standard .NET environment/URL variables | Missing in `.env`; localhost:4200/4300 allowed in local demo. Configure exact HTTPS origins and trusted proxies for deployment |
| SQL test database | `NBJEWEL_PLATFORM_TEST_CONNECTION` | Missing, deliberately not used; six SQL tests remain skipped |

The existing ERP assistant and website assistant backend use grounded, fixed tools and stored knowledge. This upgrade separates its Gemini credential but does not add a customer chatbot widget or approve a researched FAQ corpus. Those remain separate work from these two requested upgrades. Camera tracking uses existing MediaPipe browser/model assets, not a paid vision API key. Network availability is still required for remotely loaded model assets and the OSM map.

## Start locally

Use three terminals, each starting from the repository root. Stop an older sample API before starting another on the same port. The API prints a fresh temporary staff email/password in that terminal. Do not save it in the repo or browser.

```sh
cd /Users/muhammadnazirahmedmapkar/Documents/NBJewel
CUTOUT_PYTHON_PATH=/tmp/nbjewel-cutout-runtime-venv/bin/python python3 scripts/run-sample-api.py --customers --with-services
```

`--with-services` reads only whitelisted Gemini/model/metal settings from root `.env`, without printing values or loading SQL connections. Process environment overrides those file settings. Omit it to test missing-key fallbacks. The launcher always strips SQL/connection variables. The temporary Python path is available on this machine; on a fresh machine select an interpreter with the existing Pillow/numpy/scipy dependencies.

```sh
cd /Users/muhammadnazirahmedmapkar/Documents/NBJewel/frontend
npm start
```

```sh
cd /Users/muhammadnazirahmedmapkar/Documents/NBJewel/customer-tryon
npm start
```

Open ERP at `http://localhost:4200` and customer portal at `http://localhost:4300`. Keep `localhost` consistent, rather than mixing it with `127.0.0.1`, for cookie/CORS checks. Sample identity is intentionally loopback-only; do not expose it on the LAN for phone testing. Use desktop responsive mode locally. A physical phone requires a separately configured HTTPS deployment with persistent identity and permitted origins.

## Test ERP Vision

1. Sign into ERP with the launcher's temporary staff credentials. Open a supported necklace/earring item and its Images tab.
2. Upload a permitted sample PNG/JPG, at most 5 MB. When the preview renders, expect `Analyzing jewelry image with Gemini...`, followed by draft description and collection.
3. Type during analysis; your changed field must survive the reply. Change both fields, save storefront details, reload the item and verify the saved copy.
4. Use Re-generate with AI to request another draft. For a missing/rate-limited/invalid provider response, expect a manual-entry message and editable fields.
5. Inspect visual quality yourself, check the review confirmation, approve the current revision, then explicitly publish if desired. AI analysis never performs those steps. Representative jewelry cutout quality and licensing still require owner review.

## Test customer journey

1. Start signed out. Browse landing/collections/products, expand recorded rates, open Stores Map and select a store. No login is required. Refresh a store-specific product URL and verify its catalog context remains.
2. Click Try on now. Sign up with a sample email and strong password; the original design opens automatically. Camera permissions and fit/export require a real device/manual review.
3. Sign out, open a product and click Add to bag. Log in; the original design should appear in the bag without another click. Canceling the dialog must leave the bag unchanged.
4. Open Bag, choose an available store/date and sample contact details, then Reserve. Expect a requested reference, an emptied selection and the request in Profile. Requests are demo data and do not notify a real store or hold stock.
5. Save profile details, save a wishlist design, reload, then sign out. A different customer must not see the first customer's bag or reservations. Wishlists are scoped to the account and this browser, not synchronized across devices.
6. Check 390px and 1280px widths. Mobile has Home/Stores/Try-On/Bag/Profile; desktop has Shop/Stores Map/Wishlist/Bag/account navigation. Store-location permission is optional; denying it retains the map/list.

## Verification evidence and limits

- Backend: 394 passed, six SQL tests skipped; build zero warnings/errors. Includes authorization/CSRF/current-revision Vision gates, missing-key/429/malformed AI data, customer ownership and reservation idempotency/explicit selected items, opt-in customer identity, live-sync freshness and spot-cache outage regression.
- ERP: 73 tests passed; typecheck and production build passed. Includes preserving manual edits and ignoring stale Vision replies.
- Customer: 72 tests passed across eight files; strict TypeScript and production build passed. Includes route-context persistence, try-on piece/design navigation and late-401 session regressions.
- No lint script or ESLint configuration exists in either Angular app. Lint is unavailable, not claimed passed. `git diff --check` supplies whitespace validation, not a linter.
- Browser: temporary signup resumed the selected try-on; signed-in add/reserve/profile/sign-out and guest login-to-add passed. Public OSM map rendered; 390px mobile and 1280px desktop layouts checked. Shared store/product reload verified.
- Live Gemini/GoldAPI responses, SMTP delivery, production SQL, real partner sync, physical-device camera/geolocation and representative cutout quality are not certified by these sample checks. No production data/SQL/Docker/deployment action was performed.
- Serena MCP was unavailable in the session. Symbol/reference searches, TypeScript/.NET compilers and tests were used; Headroom was available and used for large-output handling. Subagents performed read-only inspection/review only.

Provider contracts: [Gemini generateContent](https://ai.google.dev/api/generate-content), [Gemini structured output](https://ai.google.dev/gemini-api/docs/structured-output), [OpenStreetMap embeds](https://wiki.openstreetmap.org/wiki/Export), [GoldAPI official examples](https://github.com/goldapi-io/gold-api-examples-js).

## Files created or updated

- `.env.example`
- `AI-MEMORY-HANDOFF.md`
- `README.md`
- `backend/NbJewel.Api.Tests/Jewelry/ItemCutoutHttpTests.cs`
- `backend/NbJewel.Api.Tests/SampleIdentityRuntimeTests.cs`
- `backend/NbJewel.Api/Chat/GeminiLlmClient.cs`
- `backend/NbJewel.Api/Controllers/AuthController.cs`
- `backend/NbJewel.Api/Controllers/ItemImagesController.cs`
- `backend/NbJewel.Api/Controllers/PublicCatalogController.cs`
- `backend/NbJewel.Api/Identity/ErpBoundaryFilter.cs`
- `backend/NbJewel.Api/Identity/IdentityData.cs`
- `backend/NbJewel.Api/Identity/SampleIdentitySetup.cs`
- `backend/NbJewel.Api/NbJewel.Api.csproj`
- `backend/NbJewel.Api/Program.cs`
- `customer-tryon/public/catalog/catalog.json`
- `customer-tryon/public/storefront-config.json`
- `customer-tryon/src/app/app.component.ts`
- `customer-tryon/src/app/app.config.ts`
- `customer-tryon/src/app/catalog/catalog.service.spec.ts`
- `customer-tryon/src/app/catalog/catalog.service.ts`
- `customer-tryon/src/app/catalog/store.service.ts`
- `customer-tryon/src/app/config/storefront-config.ts`
- `customer-tryon/src/app/pages/bag.page.ts`
- `customer-tryon/src/app/pages/home.page.ts`
- `customer-tryon/src/app/pages/product.page.ts`
- `customer-tryon/src/app/pages/reserved.page.ts`
- `customer-tryon/src/app/pages/shop.page.ts`
- `customer-tryon/src/app/pages/tryon.page.ts`
- `customer-tryon/src/app/pages/wishlist.page.ts`
- `customer-tryon/src/app/ui/icons.ts`
- `customer-tryon/src/app/ui/product-card.component.ts`
- `customer-tryon/src/app/ui/site-header.component.ts`
- `customer-tryon/src/styles.css`
- `frontend/src/app/core/data/storefront-admin.service.ts`
- `frontend/src/app/features/catalog/storefront-panel.component.spec.ts`
- `frontend/src/app/features/catalog/storefront-panel.component.ts`
- `scripts/run-sample-api.py`
- `backend/NbJewel.Api.Tests/CustomerPortalHttpTests.cs`
- `backend/NbJewel.Api.Tests/JewelryVisionTests.cs`
- `backend/NbJewel.Api.Tests/SpotRatesTests.cs`
- `backend/NbJewel.Api/Controllers/CustomerPortalController.cs`
- `backend/NbJewel.Api/Jewelry/JewelryVisionClient.cs`
- `backend/NbJewel.Api/Storefront/CustomerPortal.cs`
- `backend/NbJewel.Api/Storefront/SpotRates.cs`
- `backend/NbJewel.Api/Storefront/StoreDirectory.cs`
- `customer-tryon/public/catalog/store-directory.json`
- `customer-tryon/src/app/customer/auth-dialog.component.ts`
- `customer-tryon/src/app/customer/customer-session.spec.ts`
- `customer-tryon/src/app/customer/customer-session.ts`
- `customer-tryon/src/app/customer/store-directory.service.ts`
- `customer-tryon/src/app/pages/profile.page.ts`
- `customer-tryon/src/app/pages/stores.page.ts`
- `customer-tryon/src/app/pages/tryon-navigation.spec.ts`
- `customer-tryon/src/app/pages/verify-email.page.ts`
- `customer-tryon/src/app/ui/market-rates.component.ts`
- `customer-tryon/src/app/ui/mobile-nav.component.ts`
- `docs/customer-portal-schema.sql`
- `docs/portal-upgrades.md`
- `docs/superpowers/plans/2026-11-14-portal-upgrades.md`
