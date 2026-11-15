<!-- Role: Records the revised multi-business web portal design and future implementation steps; execution is not authorized. -->
# Business web portals implementation plan

> **Status: planning only. Do not implement until Muhammad explicitly authorizes it.**
> Saving or reading this document, an unchecked task, or a generic instruction to continue is not implementation approval. No product code, schema migration, provisioning or deployment is authorized by this document.
>
> **For agentic workers:** After authorization, use the executing-plans skill for sequential implementation. Keep core implementation unified; use subagents only for isolated read-only review/verification if permitted by the current instructions.

**Revision:** 2026-11-15 by claude-portal-plan. Incorporates review findings F1 to F17, each checked against the code at fcdf3a1 (section 0), and Muhammad's decision list D1 to D9 (section 1).

**Goal:** Give each retailer/wholesaler its own branded customer portal, managed from a Web Portal entry in ERP, using one shared NB JEWEL template.

**Architecture:** One shared application serves many isolated businesses. Each request carries a trusted `BusinessContext` that selects that business's data partition. Customer identity is global; shopping state is per (customer, business). Portal configuration has a draft and a published revision.

**Stack:** Existing Angular standalone apps (ERP `frontend/`, storefront `customer-tryon/`) and .NET Web API. Sample repository for local verification. SQL changes prepared as reviewable files only.

**Labelling rule (AGENTS.md):** Every new table, record, route, flag, folder layout and slug named in this plan is **assumed** design, not existing NB Jewel structure. NB Jewel's real multi-business internals are unknown.

## 0. Review findings verification (code at fcdf3a1)

| ID | Result | Evidence and correction |
| --- | --- | --- |
| F1 | verified, with correction | Singletons: `IItemRepository` (Program.cs:95, :99), `IItemImageAssetRepository` (:116, writes `links/item-{id}.json`), `FileProductPublicationStore` (:139, writes `publications/item-{id}.json`; registered only in Sample mode without a platform connection), `FileJewelryRepository` (:162). Correction: `StoreDirectory` is Scoped (:64), but it still reads one global path. Also singletons that capture the repository: `ItemCutoutPipeline` (:117), `IChatDataReader` (:123), `ChatTools` (:124). Global UNIQUE constraints in item_catalog_schema.sql: :22 Category.Name, :41 Store.Code, :50 Design.DesignCode, :59 Item.ItemCode, :102 Sku; also :28 Metal.Name and :36 Purity. MetalRate (:145) has no owner column. |
| F2 | verified | IdentityData.cs:10-11 (single `AccountType`, global `AllStores`); PermissionGrant :21-25 and StoreGrant :27-31 have no business key. ErpBoundaryFilter.cs:50-52 and KnowledgeController.cs:21 check global grants/AllStores. AllStores staff are created by IdentitySetup.cs:81 (`--bootstrap-identity`, 11 permissions) and SampleIdentitySetup.cs:60 (8 permissions). The 11 permissions are string literals with no constants class. |
| F3 | verified | `CustomerPortalRecord(Id, Json, Revision)` at IdentityData.cs:14-19, with a concurrency token. JSON shape `CustomerState(Name, Phone, Bag, Reservations)` at CustomerPortal.cs:7; `BagEntry(Id, Source)` at :5. CustomerPortalController.cs:25 requires `AccountType == "customer"`. Wishlist key at store.service.ts:22-24 is `nbjewel.wishlist.v2.${source}.${userId or guest}`, with no slug. |
| F4 | verified, with correction | `PublicProduct` carries the full `ItemPrice` (PublicCatalogController.cs:19-21, :124). `metal-rates` is anonymous (:92; ErpBoundaryFilter.cs:37-38). Correction: website `AssistantTools.PublicItem` is a JsonObject (not a record) holding total price and currency (AssistantTools.cs:103-104). product.page.ts:237-238 and bag.page.ts:148-150 build `wa.me/?text=` with price and "Hello NB Jewel" and no number. catalog-api.models.ts:26-30 throws when price is missing. Bundled catalog.json holds price inputs that the browser turns into prices (catalog.models.ts:79). No price-hiding concept exists anywhere. |
| F5 | verified, with correction | ERP serves on the default port 4200 (`ng serve`); the storefront uses `--port 4300`. Correction: staff and customers share one session cookie, `nbjewel.session` (SameSite=Strict, IdentitySetup.cs:32-36), plus CSRF cookie `nbjewel.csrf` and header `X-CSRF-TOKEN`. AccountType tells them apart. |
| F6 | verified, with path correction | storefront-config.json has `"apiBaseUrl": null`. `storefront-catalog.json` is a build link (NbJewel.Api.csproj:22) to `customer-tryon/public/catalog/catalog.json`. Demo validation is at CustomerPortalController.cs:74-77. |
| F7 | verified | StoreDirectory.cs:15-16 reads one `STOREFRONT_STORES_PATH`, falling back to `sample-data/store-directory.json`. `StoreLocation` has no business field. |
| F8 | verified | `AssistantContext` (PlatformContracts.cs:8-9), owner hash and fingerprint (AssistantAccess.cs:95-97), `KnowledgeVersion` and `ProductPublication` (KnowledgeDatabase.cs:28, :30), `PlatformConversation` (PlatformDatabase.cs:6-16) and route `api/ai/{assistant}/conversations` have no business. Two fixed assistants (AiGateway.cs:21). |
| F9 | verified, with correction | `api/jewelry` (JewelryController.cs:14): GET is anonymous; management needs `jewelry.manage` plus AllStores (ErpBoundaryFilter.cs:30, :50). Correction: `/jewelry-images/{file}` is an anonymous minimal API (Program.cs:199), not a static mount. ERP menu "Try-On Jewelry" points to `/owner/jewelry` (mock-data.ts:11). `OwnerPin.Check` is never called. |
| F10 | verified | KnowledgeController.cs:61 has literal "Necklaces"/"Earrings". storefront-admin.service.ts:11-13 has `[3, 6]`. PublicCatalogPolicy.cs:8-10 is already name-based and is the reuse target. |
| F11 | verified, with path correction | Routes are defined in `customer-tryon/src/app/app.config.ts:13-25` (no app.routes.ts). Root-absolute links include product-card.component.ts:16 and site-header.component.ts:23. `providedIn: 'root'` at catalog.service.ts:11 and store.service.ts:10. Reusable helpers exist: `CatalogService.linkParams` (:20) and `catalogContextGuard` (:59-64). home.page.ts and tryon.page.ts drop the context today. |
| F12 | verified | The prior plan grants owner membership to an existing account and forbids hardcoded passwords, but defines no delivery. Precedent: run-sample-api.py prints runtime-generated credentials once. |
| F13 | verified | No WhatsApp field exists in backend, sample-data or .env.example. |
| F14 | applied | The prior plan listed per-product visibility. Removed. |
| F15 | applied | Task A split into A1, A2, A3 (section 7). |
| F16 | applied, with correction | Section 3. There are no Vision, StoreDirectory or SpotRates controllers: those routes live in PublicCatalogController and ItemImages `Analyze`. |
| F17 | applied | Labelling rule above; new structures say "assumed". |

## 1. Confirmed requirements and decisions

### Confirmed requirements (unchanged)

| Requirement | Behavior |
| --- | --- |
| Platform | One shared NB JEWEL platform serving many retailer/wholesaler businesses |
| Portal ownership | One branded portal per business, covering its branches |
| Template | One fixed template; editable branding/content; section visibility only |
| ERP editor | Sidebar "Web Portal" with draft, preview, publish, offline |
| Wholesale v1 | Catalog and enquiries only; no trade pricing, no minimum quantities |
| Customer identity | One customer login; separate bags, wishlists, reservations per business |
| Onboarding | NB JEWEL provisions businesses and owner access; no public business signup |

### ERP editor areas

| Area | Controls |
| --- | --- |
| Branding | Business name, logo, approved color preset |
| Homepage | Banner image, headline, introduction, shop story |
| Collections | Visible collections and featured designs (F14: no per-product visibility; ERP product publication stays the only product gate) |
| Customer actions | Show prices; enable enquiries, try-on, bag, reservation requests |
| Contact and branches | Phone, WhatsApp number (required when enquiries are on), email, opening hours, visible branches |
| Preview and publication | Save draft, preview desktop/mobile, publish, take offline |

### Decision table

All nine are **proposed defaults** pending Muhammad's confirmation (section 9). Rule used: the simplest option that keeps every confirmed requirement.

| ID | Status | Proposed default | Why |
| --- | --- | --- | --- |
| D1 Tenancy | proposed | Per-business data partition behind a `BusinessContext`-resolved storage factory (assumed). Sample/file adapters use one folder per business. SQL adapter: v1 code refuses to start with more than one business; a prepared schema file records an assumed BusinessId-column layout for later review. | Current repositories and file stores stay unchanged inside a partition, so no query is rewritten. Overlapping IDs are safe by construction. SQL is not executable under current authorization anyway. |
| D2 Staff | proposed | One business per staff account in v1 via `BusinessMembership(UserId unique, BusinessId, Role owner/staff)` (assumed). Existing grants and AllStores are read inside that membership. No global web grant remains; the NB JEWEL operator acts only through a host CLI command. | Minimal identity change. The operator role needs no web surface. Route shape `/api/businesses/{slug}/...` stays forward-compatible with multi-membership. |
| D3 Staff can shop | proposed | No. Staff accounts keep receiving the existing 403 on customer routes; a staff member shops with a separate customer account. | `AccountType` is single-valued and the session cookie is shared. Mixing roles would widen the permission surface. |
| D4 Hidden price mode | proposed | Hides price, breakdown, making, stone value, VAT, business metal rates and the rate header. Gross/net weight stay visible as product specification. | Weights are product facts, not trade prices. Hiding them too is a one-line change if Muhammad prefers. Trade-off: weight combined with the public market reference allows a rough metal-value estimate. |
| D5 Bundled demo catalog | proposed | Kept on legacy unscoped URLs only (current `source=demo` behavior, labelled sample). `/shops/{slug}` portals are API-only with no bundled fallback. Legacy `source=erp` URLs redirect to `/shops/nb-jewel-demo`. | Retiring it now would bring back the empty-storefront problem (no_items.png), because publication requires manual approval. |
| D6 Website assistant | proposed | Not exposed on portals in v1. `/api/ai/website` stays pinned to nb-jewel-demo and honors its price policy. | No customer chat UI exists yet (handoff), so this is the smallest safe scope. |
| D7 Owner credentials | proposed | Provisioning attaches an existing staff account when present. Otherwise it creates one and prints a runtime-generated password once to the operator terminal, never logged, stored or committed. | Matches the existing run-sample-api.py precedent. A reset token would need SMTP or a new reset flow. |
| D8 Shared registrable domain | proposed | Yes: ERP, storefront and API share one registrable domain in production (hostnames assumed). | Keeps SameSite=Strict cookies and the current CSRF design working for preview and customer sessions. Custom domains stay excluded. |
| D9 Try-on module | proposed | The legacy `/api/jewelry`, `/jewelry-images` and ERP "Try-On Jewelry" menu belong to nb-jewel-demo only (members with `jewelry.manage`). Portal try-on for every business uses that business's approved product previews. | No new per-business jewelry storage, and the menu stays hidden for other businesses. |

### Defaults and boundaries (kept from the prior plan; assumed policy, not NB Jewel policy)

- Retailer preset: prices and existing actions on. Wholesaler preset: prices hidden, enquiries on; try-on, bag and reservations off. Owners can change the switches.
- A new business starts offline. Slugs are unique and immutable. `nb-jewel-demo` is the existing demo business; `sample-wholesale` is the second sample fixture (assumed name).
- Fixed section order. No HTML, JavaScript, custom CSS or embeds.
- Branding uploads reuse bounded PNG/JPG validation; no cutout pipeline.
- Portal settings never bypass product eligibility, current-image approval or explicit publication. Revocation stays immediate.
- Store catalogs and reservations still require actual stock.
- Price hiding and disabled actions are enforced server-side, including requests from stale tabs.
- v1 enquiries are WhatsApp/contact links only; there is no message delivery service.
- Provider keys never appear in the editor.
- Excluded: custom domains, payments, wholesale ordering, self-service signup, production provisioning.

### Global constraints

- Main checkout on `feature/customer-storefront`; AGENTS.md and handoff protocol apply.
- Preserve protected owner files, `.env` files and running user sessions.
- No SQL execution, Docker, deployment, destructive migration, worktree creation or force push.
- Reuse the ERP shell/theme and customer components. Role comments on new files; no em-dashes in UI/docs.
- Sample-only verification with SQL connection settings unset.

## 2. Tenancy and identity design

### 2.1 Business registry (assumed)

`Business(Id, Slug, Name, Kind retailer|wholesaler, CreatedAt)`. Sample mode reads `sample-data/businesses.json` (assumed), seeded with nb-jewel-demo and sample-wholesale. Slug format is `^[a-z0-9-]{3,40}$`, with reserved words `preview`, `api` and `assets` (assumed).

### 2.2 BusinessContext resolution (request-scoped)

| Route family | Resolution rule | Failure |
| --- | --- | --- |
| Public shop `/api/shops/{slug}/...` | Slug to registry; portal must be online | Unknown or offline: identical 404, no catalog |
| Customer shop `/api/customer/shops/{slug}/...` | Slug to registry plus `AccountType customer` | 404 or 403 |
| ERP routes (existing unscoped) | Caller's single membership | No membership: 403 |
| Portal editor `/api/businesses/{slug}/portal/...` | Slug must equal the caller's membership business | Mismatch: 404 |
| Legacy unscoped public/customer/assistant/jewelry | Fixed to nb-jewel-demo | n/a |

A business ID in a body or query string is never trusted. A missing context fails closed.

### 2.3 Data partition (D1)

- `IBusinessStorage.For(businessId)` (assumed) returns that business's `IItemRepository`, `IItemImageAssetRepository`, `IProductPublicationStore`, `SampleItemIdAllocator`, `StoreDirectory`, portal store and knowledge store.
- nb-jewel-demo adopts the existing roots in place: `sample-data/`, `ITEM_ASSET_STORAGE_PATH` and the current `STOREFRONT_STORES_PATH`. Other businesses use `{root}/businesses/{slug}/` (assumed layout). No files are copied or deleted.
- Item, store, category and design IDs are local to a business. Every external reference is a (slug, id) pair: URLs, bag entries, featured items, preview routes and assets.
- Singletons that hold a repository (`ItemCutoutPipeline`, `IChatDataReader`, `ChatTools`) receive the business repository per call. Every cache key includes BusinessId.
- Market reference quotes (`SpotRates`) stay shared. Business metal rates are partitioned.
- The per-business branch directory (F7) is validated at load: every directory entry's store ID must exist among that business's ERP stores, otherwise it is skipped with a logged warning.

### 2.4 Staff and memberships (D2)

- `BusinessMembership(UserId unique, BusinessId, Role)` (assumed).
- `PermissionGrant`, `StoreGrant` and `AllStores` keep their keys and are read inside the membership. AllStores means all branches of that business.
- New permissions: `portal.manage` (draft, assets, preview) and `portal.publish` (publish, offline). Role owner implies all 13 permissions in that business.
- `ErpBoundaryFilter` requires AccountType erp plus membership plus permissions. `KnowledgeController.CanManage` uses the membership instead of the global AllStores. Default deny stays for unmapped actions, and each new controller action gets an explicit map entry.
- `/api/auth/me` adds `business { slug, name, role }` for erp accounts.
- Grant migration (Sample at startup, idempotent; SQL as a prepared file): every existing erp user gets an nb-jewel-demo membership. Bootstrap and sample staff become role owner, everyone else role staff. Existing permissions are unchanged.

### 2.5 Customers (D3)

- AccountType customer is unchanged. Profile (name, phone) stays global.
- Shopping state moves to `CustomerShoppingState(UserId, BusinessId, Json, Revision)` (assumed) holding bag, reservations and revision.
- Wishlist local key becomes `nbjewel.wishlist.v3.${slug}.${userId or guest}`.
- One shared session cookie means one signed-in account per browser profile. This is documented in QA.

### 2.6 Provisioning and owner credentials (D7)

- CLI only (assumed flags): `dotnet run --project backend/NbJewel.Api -- --provision-business --slug <slug> --name <name> --kind retailer|wholesaler --owner-email <email>`.
- Development and Sample only in v1, the same guard as `--bootstrap-identity`.
- Creates the business offline with a preset draft. Attaches an existing staff account, or creates one and prints its generated password once.
- Rejects duplicate, reserved or malformed slugs, and owner emails that already hold another membership.
- No HTTP signup or provisioning route exists.

## 3. Endpoint inventory (F16)

M = membership business; L = fixed nb-jewel-demo; S = slug route. Every non-GET keeps the global antiforgery filter. Every negative test asserts both the status and that no other-business data appears in the body.

### Existing routes

| Verb and route | Permission today | Business rule after | Negative test |
| --- | --- | --- | --- |
| GET /api/items, /api/items/{id} | erp items.read, prices.read, status.read | M | B staff reading an ID that exists only in A gets 404; an overlapping ID returns B's values |
| GET /api/items/{id}/stock, /variants, /price | erp items.read plus stock.read or prices.read | M | same as above |
| POST /api/items/{id}/stock | erp stock.write plus StoreGrant | M; store must be in B | store ID valid only in A is rejected |
| POST /api/items, PUT /api/items/{id}, PATCH /api/items/{id}/status | erp items.write or status.write plus AllStores | M; AllStores means all of B | B write leaves A's partition byte-identical |
| GET, POST /api/items/{itemId}/image; POST /image/analyze, /image/review; GET /image/preview | erp item perms plus AllStores (review also needs knowledge.manage) | M | A revision ID returns 404 to B; B upload never lands in A's folder |
| GET /api/categories, /api/designs | erp reference.read | M | returns only B rows |
| GET /api/stores | erp reference.read, store-filtered | M | returns only B stores |
| GET /api/metal-rates/latest | erp reference.read, prices.read | M | B gets B's rates only |
| GET /api/stock/low | erp reference.read, stock.read | M | only B stock |
| POST /api/chat | erp chat.read | M | B answer never names an A-only item |
| GET, POST, PUT /api/ai/knowledge/documents[/{id}]; POST .../{id}/publish, /revoke | in-controller: erp plus global AllStores plus knowledge.manage | M; AllStores in the membership | A owner cannot read or publish B documents; customer gets 403 |
| POST /api/ai/knowledge/products/{itemId}/publish, /revoke; GET .../products/{itemId} | same | M; category check through PublicCatalogPolicy (F10) | A owner publishing an overlapping ID changes only A |
| POST, GET, DELETE /api/ai/erp/conversations[...] | erp chat.read; owner hash | M; BusinessId added to owner hash and fingerprint | conversation created in A is unreadable by B and after a membership change |
| POST, GET, DELETE /api/ai/website/conversations[...] | anonymous visitor | L (D6) | tools never return sample-wholesale items; price omitted when the demo hides prices |
| GET /api/jewelry | anonymous | L (D9) | n/a, demo data only |
| GET /api/jewelry/all; POST /api/jewelry; PATCH /{id}/active; DELETE /{id} | erp jewelry.manage plus AllStores | M must be nb-jewel-demo | sample-wholesale owner gets 403 |
| GET /api/storefront/products, /products/{id}, /products/{id}/preview, /metal-rates, /stores, /spot-rates | anonymous (named exceptions) | L; demo portal policy applies (spot-rates stays global market data) | never returns sample-wholesale items; `businessId`/`slug` query values ignored |
| GET, PUT /api/customer/profile | customer | none (global) | staff gets 403 |
| POST /api/customer/bag; DELETE /api/customer/bag/{source}/{productId}; POST /api/customer/reservations | customer | L | entries land only in the nb-jewel-demo state |
| GET /api/auth/csrf; POST /register, /login, /confirm-email, /resend-verification, /logout | anonymous plus CSRF | none | register never creates an erp account or a membership |
| GET /api/auth/me | authenticated | adds membership business for erp | customer response has no business |
| GET /jewelry-images/{file} | anonymous minimal API | L | a file name belonging to another business returns 404 |
| GET /health/live, /health/ready | anonymous minimal API | none | body contains no business data |

### New routes (all assumed)

| Verb and route | Permission | Business rule | Negative test |
| --- | --- | --- | --- |
| GET /api/shops/{slug} (published portal config) | anonymous | S, online | offline and unknown return the same 404 |
| GET /api/shops/{slug}/products, /products/{id}, /products/{id}/preview, /stores, /metal-rates | anonymous; price policy | S, online | overlapping ID resolves to the slug's business; hidden mode has null price and empty rates |
| GET /api/shops/{slug}/assets/{assetId} | anonymous | S; asset referenced by the published revision | draft-only asset gets 404 |
| GET /api/customer/shops/{slug}/state; POST .../bag; DELETE .../bag/{productId}; POST .../reservations | customer; current action switches | S | disabled action from a stale tab gets 409 (assumed code); store ID from another business rejected; staff gets 403 |
| GET, PUT /api/businesses/{slug}/portal/draft | erp portal.manage, expected revision | M equals slug | other slug 404; customer 403; missing CSRF 400; stale revision 409 |
| POST /api/businesses/{slug}/portal/assets; GET .../assets/{id} | erp portal.manage | M | oversize or non-PNG/JPG rejected; other business's asset ID 404 |
| GET /api/businesses/{slug}/portal/preview (draft config plus products) | erp portal.manage; `Cache-Control: no-store`, noindex | M | anonymous 401; `preview=true` on public routes ignored |
| POST /api/businesses/{slug}/portal/publish, /offline | erp portal.publish, expected revision | M | manage-only staff 403; invalid draft leaves the live revision byte-identical |
| CLI `--provision-business` | host operator | n/a | POST /api/businesses returns 404 or 405 |

## 4. Price-hiding matrix

| Surface | Prices shown | Prices hidden (D4) | Enforcement and test |
| --- | --- | --- | --- |
| Public product list/detail DTO | full `ItemPrice` | `price: null`; weights kept | server projection; JSON has no total, making, rate, stone or VAT values |
| Product preview image | unchanged | unchanged | n/a |
| Business rates `/api/shops/{slug}/metal-rates` | dated rates | empty list plus `pricesHidden: true` | server |
| Legacy `/api/storefront/metal-rates` | demo policy | demo policy | server |
| Market reference `/api/storefront/spot-rates` | rendered | section not rendered; endpoint stays global market data (not business pricing) | client section switch |
| Website assistant | demo only, total price | demo only, price omitted | AssistantTools reads the demo's published policy |
| ERP item APIs and ERP assistant | unchanged for authorized staff | unchanged | existing permissions |
| Enquiry text | business name, product name/code, portal link, price; `wa.me/{published WhatsApp}` | same without price | client builds from the published config; button absent when enquiries are off |
| Bag and reservation responses | no price fields today | no price fields | keep; bag total hidden client-side |
| Client model | `price` present | `price` optional; mapper accepts null only when the shop config says `pricesHidden`, otherwise throws as today | catalog-api.models.ts unit tests |
| Bundled demo catalog | legacy URLs only | never under `/shops` | route test |
| Stale tab | n/a | server re-reads the published policy on every request | HTTP test after publishing a switch change |

## 5. Draft, preview and publish

Stored data (assumed):
- `PortalDraft(BusinessId, Revision, SettingsJson, UpdatedBy, UpdatedAt)`
- `PortalPublication(BusinessId, Revision, SettingsJson, PublishedBy, PublishedAt)`
- `PortalState(BusinessId, Online)`
- Sample storage: atomic JSON files in the business partition.

Settings content: branding, homepage text and assets, fixed section switches, visible collection names, featured item IDs (max 12, assumed), action switches, contact, and visible branch IDs. Collection names come from the business's published products (`StorefrontMetadata.Collection`).

Server rules:
1. Draft save requires `portal.manage` and an expected revision; a mismatch returns 409 and preserves the other editor's work. Draft may be incomplete but must be well-typed, with text lengths bounded and no HTML.
2. Publish requires `portal.publish` and the expected draft revision. Full validation: name present; color from the preset list; every asset, branch and featured item belongs to the business; every featured item is currently published and eligible; WhatsApp in E.164 format whenever enquiries are on (F13); at least one visible branch whenever reservations are on.
3. Publish writes the new publication atomically (temp file and rename) and sets the portal online. Any failure leaves the previous publication and state untouched.
4. Offline requires `portal.publish`, keeps the publication and makes public reads return 404.
5. Revoked or unpublished products drop from rendering immediately, with no republish needed. Product publication remains the only product gate (F14).
6. Draft assets are served only through the authenticated editor route. Public asset reads require a reference from the current publication.
7. An audit line (business, actor ID, action, revision, time; no secrets) is appended per save, publish and offline.
8. Preview (F5): ERP opens `{storefrontBaseUrl}/shops/{slug}/preview` (assumed nonsecret ERP config key). That storefront route reads `GET /api/businesses/{slug}/portal/preview` with the staff session cookie; this works under D8 same-site. The response is `no-store` and noindex. Preview renders the same template with actions disabled. Customer mutation endpoints already reject staff AccountType, and no `preview` flag on public routes grants draft access.

## 6. Compatibility, legacy mapping and data migration

| Legacy | v1 behavior |
| --- | --- |
| `/`, `/shop`, `/p/:id`, `/bag`, `/wishlist`, `/reserved`, `/try/:id`, `/stores`, `/profile` with `source=demo` or no API config | Unchanged bundled demo (D5), labelled sample; shopping state counts as nb-jewel-demo |
| Same paths with `source=erp` | Redirect to `/shops/nb-jewel-demo/...`, preserving product ID, `store` and try-on `parts` |
| `/api/storefront/*` | Thin alias for nb-jewel-demo (L) |
| `/api/customer/bag`, `/api/customer/reservations` | Alias to nb-jewel-demo shopping state |
| `/api/jewelry`, `/jewelry-images`, ERP `/owner/jewelry` | nb-jewel-demo only (D9) |
| `/api/ai/website` | nb-jewel-demo only (D6) |

Data migration (idempotent; no deletion; SQL as prepared files only):
- **Customer rows:** read-time migration in `CustomerPortal`. When a `CustomerPortalRecord` still holds bag or reservations and no nb-jewel-demo `CustomerShoppingState` exists, move them into it and keep name/phone as the global profile. Both writes go in one save with revision checks. Prepared SQL backfill file (assumed name `docs/business-portal-schema.sql`).
- **Publications and assets:** nb-jewel-demo adopts the existing `publications/`, `links/`, revision folders and ID reservations in place. Nothing is copied into other businesses.
- **nb-jewel-demo portal:** seeded with a retailer-preset publication, online, so the legacy API keeps working. Assumed content, editable later.
- **Staff:** membership migration per 2.4.
- **Wishlist:** on first load the storefront copies `nbjewel.wishlist.v2.{source}.{user}` into the nb-jewel-demo v3 key and leaves v2 in place.
- **Sample identity:** in-memory and reset on restart, so its migration is exercised by tests, not by persisted data.

## 7. Ordered tasks

All tasks are **pending and unauthorized**. Each task follows the same loop: write failing tests, record the RED log, apply the minimal implementation, run the affected suites, update the handoff, checkpoint by explicit path, push.

**Commands (from README/handoff):**
- **Backend:** run inside the README verification wrapper (strips CONNECTION/SQL variables, `Repository__Provider=Sample`) with `CUTOUT_PYTHON_PATH=/tmp/nbjewel-cutout-runtime-venv/bin/python`:
  - `dotnet build backend/NbJewel.Api --no-restore`
  - `dotnet test backend/NbJewel.Api.Tests --no-restore`
  - focused runs add `--filter "FullyQualifiedName~<Class>"` (test class names below are assumed).
- **ERP:**
  - `npm --prefix frontend test`
  - `(cd frontend && npx tsc --noEmit -p tsconfig.app.json)`
  - `NG_BUILD_MAX_WORKERS=2 npm --prefix frontend run build`
- **Storefront:** the same three commands with `customer-tryon`.
- **Always:** `git diff --check -- <paths>`. No lint target exists; report it as absent.

### A1. Business context and memberships
- **Depends on:** none.
- **Touchpoints:** new `backend/NbJewel.Api/Businesses/` (registry, BusinessContext, membership, provisioning CLI); Identity/IdentityData.cs, IdentitySetup.cs, SampleIdentitySetup.cs, ErpBoundaryFilter.cs; KnowledgeController.cs `CanManage`; AuthController `me`; Program.cs; sample-data/businesses.json; prepared SQL file.
- [ ] RED (`BusinessContextTests`, `BusinessMembershipHttpTests`, `ProvisioningCommandTests`):
  - staff without membership gets 403
  - slug mismatch gets 404
  - AllStores covers only the member business
  - customers never resolve an erp business
  - CLI rejects duplicate/reserved slugs and second memberships, and never writes the password to logs or files
  - no HTTP provisioning route
  - existing staff migrate to nb-jewel-demo
- [ ] Implement the minimum. Add `portal.manage` and `portal.publish`.
- [ ] Full backend build/test green.

### A2. Data isolation
- **Depends on:** A1.
- **Touchpoints:** Program.cs registrations; Repositories (factory, SampleItemRepository, SampleItemIdAllocator, FileItemImageAssetRepository); Knowledge (FileProductPublicationStore, KnowledgeService); Jewelry (ItemCutoutPipeline, FileJewelryRepository demo pin); Storefront/StoreDirectory.cs; Chat (ChatTools, ChatDataReader); Platform (AssistantAccess owner/fingerprint, AssistantTools); PublicCatalogController; KnowledgeController.cs:61 to PublicCatalogPolicy; frontend storefront-admin.service.ts `[3, 6]` to name-based (F10).
- [ ] RED (`BusinessIsolationHttpTests`, two businesses with overlapping item, store and category IDs). Every existing-route row in section 3 gets its negative test, including uploads, publications, caches, chat and assistant conversations. Branch directory validation is covered. ERP spec: eligibility by category name.
- [ ] Implement the partition factory. Convert the singletons; nb-jewel-demo adopts the existing roots; SQL mode fails closed with more than one business.
- [ ] Backend full green. ERP test/typecheck/build green.

### A3. Customer data
- **Depends on:** A1, A2.
- **Touchpoints:** Storefront/CustomerPortal.cs; IdentityData.cs (new state record); CustomerPortalController.cs (new shop-scoped routes and legacy aliases; demo validation kept for legacy only); customer-tryon catalog/store.service.ts (v3 wishlist key and copy); prepared SQL.
- [ ] RED (`CustomerShoppingStateTests`, storefront store.service spec):
  - the same customer has separate bags in two businesses
  - a legacy row migrates once into nb-jewel-demo
  - concurrent revision conflict
  - reservation store from another business rejected
  - staff 403
  - wishlist v2 to v3 copy
- [ ] Implement. Run backend full suite and storefront test/typecheck/build.

### B. Portal configuration lifecycle
- **Depends on:** A1 to A3.
- **Touchpoints:** new Businesses/Portal store and service; new `PortalController` (assumed) with explicit ErpBoundaryFilter entries; branding upload reusing bounded PNG/JPG checks.
- [ ] RED (`PortalLifecycleHttpTests`), one test per server rule in section 5:
  - 409 on a stale revision
  - failed publish leaves live byte-identical
  - WhatsApp required with enquiries
  - foreign featured item, branch or asset rejected
  - revoked featured item drops out
  - draft asset not public
  - offline 404
  - audit lines written
- [ ] Implement. Backend full green.

### C1. Public shop API and price hiding
- **Depends on:** B.
- **Touchpoints:** new `ShopsController` (assumed) or extension of PublicCatalogController; legacy `/api/storefront/*` alias; customer action enforcement; AssistantTools demo policy.
- [ ] RED (`ShopPublicHttpTests`, `PriceHidingHttpTests`): every row in section 4 that has server enforcement; unknown and offline return the same 404; stale-tab action rejected.
- [ ] Implement. Backend full green.

### C2. Storefront business routes
- **Depends on:** C1.
- **Touchpoints:** customer-tryon app.config.ts (`/shops/:slug` parent with child routes, legacy redirects); new link helper extending `CatalogService.linkParams`; CatalogService/StoreService reset on slug change; config/models (optional price, `pricesHidden`); header/home/contact templates (branding, sections); product.page.ts and bag.page.ts enquiry text; home.page.ts and tryon.page.ts context fixes.
- [ ] RED (route and service specs):
  - switching slug clears all prior catalog, bag and wishlist state with no stale data
  - every link stays under the current slug
  - hidden mode renders no price
  - enquiry uses the published number and name
  - legacy `source=erp` redirect keeps product and store
  - offline shop shows the unavailable page with no fallback
- [ ] Implement. Storefront test/typecheck/build green.

### D. ERP Web Portal editor and preview
- **Depends on:** B (APIs), C2 (preview route).
- **Touchpoints:** frontend core/mock/mock-data.ts (add "Web Portal"; show "Try-On Jewelry" only for nb-jewel-demo members); app.config.ts route; new `frontend/src/app/features/web-portal/` and its API service; nonsecret storefront base URL config; customer-tryon `/shops/:slug/preview` route.
- [ ] RED (component and service specs):
  - owner versus staff (manage-only cannot publish)
  - fixed section switches only
  - WhatsApp validation message
  - 409 reload-and-keep flow
  - preview opens the storefront URL
  - preview disables actions
- [ ] Implement with existing ERP components. Run ERP and storefront test/typecheck/build.

### E. Two-business QA and release gate
- **Depends on:** all.
- [ ] Read-only review of every section 3 row, legacy alias and cache. Fix material findings, each with a regression test.
- [ ] Run all six gates: backend build/test, ERP test/build, storefront test/build, plus both typechecks.
- [ ] Sample browser demonstration:
  - two branded businesses
  - hidden-price wholesaler
  - the existing upload, review, publish, edit and revoke flow inside nb-jewel-demo
  - preview versus live
  - offline
  - one customer with two separate bags
- [ ] Extend docs/QA_TEST.md. Record prepared SQL and its limits. Update the handoff, checkpoint, push, release the Lock. No deploy, no SQL.

## 8. Acceptance tests per risk

| Risk | Proof | Task |
| --- | --- | --- |
| Cross-business access by route, body, asset ID, cache, chat or assistant | Overlapping-ID negative test for every section 3 row | A2, E |
| Staff escaping their business, or a global grant surviving | Membership tests; no HTTP provisioning; AllStores bounded | A1 |
| Customer state bleeding between businesses or lost in migration | Two-bag test; one-time legacy migration test; wishlist v3 copy | A3 |
| Hidden prices leaking through DTO, rates, assistant, enquiry text or client | Section 4 HTTP and spec tests | C1, C2 |
| Draft or private assets visible publicly; preview flag abuse | Guest preview 401; draft asset 404; `preview=true` ignored | B, D |
| Stale tab bypassing a disabled action | Publish a switch off, then the old request is rejected | C1 |
| Lost shop context or stale data on slug change | Slug-switch and link-helper specs; legacy redirect keeps product/store | C2 |
| Enquiry with no destination | Publish blocked without WhatsApp when enquiries are on | B |
| Try-on module leaking across businesses | Non-demo owner 403 on jewelry management; image 404 | A2 |
| Regression of the existing demo | Legacy URLs and APIs behave as before for nb-jewel-demo; full suites green | E |

Acceptance needs all of the following:
- two businesses running the same template independently
- unpublished edits staying private
- published changes affecting only the owning shop
- correct product and stock eligibility
- global login with separate shopping state per business
- all regression, build and browser checks passing

Prepared SQL alone is not evidence of production readiness.

## 9. Decisions Muhammad must confirm

1. D1: per-business partition via a storage factory; SQL refuses multi-business until a reviewed BusinessId-column schema is applied.
2. D2: one business per staff account; operator is CLI-only; no global web grants.
3. D3: staff cannot shop with their staff login.
4. D4: hidden mode hides price, breakdown and business rates, but keeps weights. Alternative: also hide weights.
5. D5: bundled demo kept on legacy URLs only; portals API-only; `source=erp` redirects to `/shops/nb-jewel-demo`.
6. D6: website assistant not on portals; pinned to nb-jewel-demo.
7. D7: attach an existing account, or print a generated password once to the operator terminal.
8. D8: ERP, storefront and API on one registrable domain in production.
9. D9: legacy try-on module is nb-jewel-demo only; portals use approved product previews.
10. Assumed names: `nb-jewel-demo`, `sample-wholesale`, reserved slugs, 409 for disabled actions, max 12 featured items, E.164 WhatsApp format.

## 10. Execution hold

**Current authorization is limited to saving this plan. Implementation has not started.**

Muhammad will explicitly decide whether and when to execute it. Until then, do not provision businesses, edit application code, create migrations or alter any running service for this feature.
