<!-- Role: Gives Muhammad a short local QA checklist for ERP jewelry publication and the customer journey. -->

Current portal changes require an owner-controlled restart to load new backend code. This agent left existing services untouched. Current catalog behavior and SQL setup: see [SQL catalog QA](SQL_CATALOG_QA.md). The storefront no longer loads a separate bundled catalog. The sample launcher below remains sample-only and must not be used to demonstrate SQL persistence.
# QA: ERP jewelry to storefront

Allow 15 minutes. Use sample data. Pass = create in ERP, publish, see it on the website, edit it, then revoke it.

## 1. Start the apps

Keep existing frontend servers. Restart the API for fresh credentials/Gemini, using Ctrl+C in its original terminal first. Restart BEFORE testing: new sample items/accounts/reservations reset on restart.

**Terminal 1: backend and temporary logins**

```sh
cd /Users/muhammadnazirahmedmapkar/Documents/NBJewel
CUTOUT_PYTHON_PATH=/tmp/nbjewel-cutout-runtime-venv/bin/python python3 scripts/run-sample-api.py --customers --with-services
```

**Terminal 2: ERP**

```sh
cd /Users/muhammadnazirahmedmapkar/Documents/NBJewel/frontend
npm start
```

**Terminal 3: customer website**

```sh
cd /Users/muhammadnazirahmedmapkar/Documents/NBJewel/customer-tryon
npm start
```

The Python path exists locally and needs Pillow/numpy/scipy. No SQL/Docker needed. If port 5155 is occupied, stop the older API; a failed startup does not change its password.

## 2. URLs and credentials

| Purpose | URL / credentials |
| --- | --- |
| ERP login | [Open ERP](http://localhost:4200/sign-in) |
| ERP email | `sample.staff@nbjewel.invalid` |
| ERP password | Copy **Temporary staff password:** from Terminal 1 after successful startup. Changes on restart; never save it in GitHub. |
| ERP new item | [Create jewelry item](http://localhost:4200/catalog/new) after staff login |
| Actual ERP storefront | [Browse published ERP jewelry](http://localhost:4300/shop?source=erp) |
| Customer account | Private window → Add to bag → Sign up. Test email: `qa.customer@example.invalid`; test-only password: `QaOnly!Jewelry2026`. NOT pre-created: sign up first. Never reuse a real password. |
| Public API evidence | [Published products JSON](http://localhost:5155/api/storefront/products) |

Keep ERP in the normal browser and customers in a private window: localhost ports share login cookies.

Home and Shop both load the API-only demo business at `/shops/nb-jewel-demo`. An empty catalog before publication is normal. Legacy `source=erp` and `source=demo` URLs redirect there; demo-source links drop the old store filter.

## 3. Main test: create, upload, publish, update, revoke

Record your item ID from the ERP URL, for example `/catalog/32`. Use that actual ID wherever `ID` appears below.

| Check | Action | Expected result |
| --- | --- | --- |
| [ ] Create | Open Create jewelry item. Code: `QA-NECKLACE-001` (use a new suffix if taken); name: `QA Temple Necklace`; category: **Necklaces**; select an existing design code; metal: **Gold**; purity: **22K**; gross/net weight: **10 g** each; stone weight: **0**; making type: **Percent**; making value: **10**. Leave optional HUID blank. Save. | New item opens with its own ID and starts as Draft. |
| [ ] Complete | Click **Change Status**, select **Complete**, and save the status. Check Overview has a calculated price. | Status is Complete and price is available. |
| [ ] Upload | Open **Images**. Upload a permitted necklace PNG/JPG, at most 5 MB. | Preview generated; quality is pending. Upload alone does not publish. |
| [ ] Gemini | Wait after the preview renders. | Loading message, then editable Description/Collection suggestions. If provider unavailable, manual entry remains possible; record AI as blocked separately. |
| [ ] Save copy | Use description `QA sample necklace for storefront sync testing.` and collection `Temple`. Click **Save storefront details**. | Success message; copy remains when leaving and reopening Images. |
| [ ] Approve | Inspect the actual preview. If acceptable, tick **I checked this preview's visual quality**, then **Approve preview quality**. | Quality becomes approved for the current image. Do not approve a poor cutout just to pass the test. |
| [ ] Publish | Click **Confirm publish**. | Success message and **Storefront: published**. |
| [ ] View | In the private window, open the ERP storefront link above and refresh. Open your new item. | Correct name, image, saved description, collection and price breakdown. Public browsing requires no login. The API JSON contains the same item ID. |
| [ ] Update | In ERP edit the name to `QA Temple Necklace Updated` and making value to **20**; save. Refresh the customer product page. | Updated name and higher making charge/total, without rebuilding either app. |
| [ ] Revoke | Back in ERP Images, click **Revoke publication**. Refresh the ERP storefront list and the previously opened product page. | Item disappears from the list and its detail becomes unavailable. |

Direct product URL: `http://localhost:4300/shops/nb-jewel-demo/p/ID` (replace `ID`). Home, product and store links retain the business and selected store.

**Pass only if publish → appearance → updated copy/price → revoke all work.** Bundled design files are no longer a catalog source.

## 4. Other quick checks

- [ ] Guest Add to bag / Try on → signup/login → original action resumes. Canceling login does nothing. Test camera permission/fit manually.
- [ ] Re-generate with AI works; typing during analysis preserves manual edits. Replacing the image hides the old publication until reviewed/published again.
- [ ] Public Stores Map opens without login and labels sample locations honestly. Below 768px, check Home / Stores / Try-On / Bag / Profile navigation.
- [ ] Use an eligible published ERP design for the bag/reservation/Profile flow. Reservations are requests, not stock holds or real store notifications.

**Stock caveat:** a new item has no store stock automatically. Publishing makes it eligible for the all-ERP catalog, but store-specific listings/reservations also require available inventory. To inspect seeded stock use item **7**, `NKL-0007` / Modern Necklace → Stock tab. Do not run SQL for this test.

## 5. If something fails

- **Can't log in:** use the latest successfully started API's password, not an old terminal's; keep all URLs on `localhost`. Customer credentials work only after signup and expire on API restart.
- **Publish disabled:** check Complete status, nonempty saved description, supported category/metal, a rendered ready preview and manual quality approval.
- **Wrong shop visible:** open `/shops/nb-jewel-demo` or the intended business slug explicitly. There is no bundled catalog fallback.
- **Published but missing:** check the public API JSON. If absent, check publication/eligibility. If present, clear UI filters, verify `source=erp` and reload.
- **AI unavailable:** root `.env` needs `GEMINI_MODEL` and either `GEMINI_ERP_API_KEY` or fallback `GEMINI_API_KEY`; start with `--with-services`. Manual publication testing can continue. Never put keys in frontend files.
- **Live metal feed unavailable:** `METAL_RATES_API_KEY` is optional and currently missing; recorded sample selling rates still work. This does not block the main sync test.

Report a failure with: checkbox/step, item ID/code, page URL, expected versus actual result, screenshot and visible error text. Exclude passwords/API keys. Full setup inventory: [portal upgrades](portal-upgrades.md).


## Business portal QA appendix (assumed sample design)

Use only sample data. The assumed fixtures are `nb-jewel-demo` (retailer, NB Jewel) and `sample-wholesale` (wholesaler, Assumed sample wholesale). Their overlapping item 7 and branch 1 IDs deliberately exercise isolation. These fixtures do not describe real NB Jewel internals.

The checklists below are repeatable owner checks. Fresh automated and browser evidence follows separately; unchecked manual cases are not claimed as browser-tested.

### Editor and publication

- [ ] Sign in to ERP as a sample business owner and open Web Portal in the existing shell.
- [ ] Edit the fixed branding, homepage content, section switches, collections, featured designs, contact details and visible branches. Verify that arbitrary HTML, JavaScript and custom CSS are unavailable.
- [ ] Save a draft and confirm the public portal retains the previous published revision.
- [ ] Open desktop and mobile authenticated preview. Check draft branding, scoped products and selected-store stock filtering. Verify customer actions are disabled and preview links preserve preview context.
- [ ] Reopen preview after losing staff authentication and confirm private draft content is cleared and denied.
- [ ] Publish a valid draft and check the public portal uses that revision. Confirm draft assets become public only when referenced by the live publication.
- [ ] Take the portal offline. Compare unknown and offline public URLs and confirm neither reveals a catalog or draft asset.
- [ ] Use a manage-only staff fixture: save and preview succeed, publish and offline are denied.
- [ ] Save from two editor tabs. Confirm the stale tab receives 409, retains local edits and offers a deliberate merge with the current revision before retry.

### Customer and price policy

- [ ] Hide prices and check product cards, details, making charges, breakdown, rate headers, totals, try-on displays and WhatsApp text contain no selling prices or price labels. Product weights remain visible.
- [ ] Check scoped and legacy public API aliases return null or omitted price fields and no business metal rates under hidden mode; authorized ERP pricing remains available.
- [ ] Open a price-visible website assistant conversation, hide prices, then confirm prior history cannot be read and fresh hidden answers omit price labels and values.
- [ ] Disable bag or reservations after opening a customer tab. Confirm stale writes are rejected without changing shopping state.
- [ ] Sign in as one customer and add designs independently to both businesses. Confirm bags, wishlists and reservations remain separate while name and phone remain global.
- [ ] Check the single shared session cookie: signing in or out affects both portals and ERP in that browser profile. Staff use a separate customer account for shopping.
- [ ] Verify ERP-only legacy wishlist migration copies into the nb-jewel-demo v3 key once; obsolete demo entries are not imported and a deliberately cleared v3 wishlist stays cleared.
- [ ] Follow legacy product, store and try-on links. Verify valid store and parts context survives under `/shops/nb-jewel-demo` and obsolete demo store IDs are dropped.
- [ ] Check enquiry links use the published business name and WhatsApp destination, omit hidden prices and retain the scoped product and selected-store link.

### Isolation and negative checks

- [ ] Switch between businesses with overlapping IDs and confirm old catalog, directory and customer state clears immediately. Pending responses from the previous business must not repopulate the page.
- [ ] Attempt another business's item, image revision, publication, knowledge document, conversation, draft, asset and branch IDs. Confirm denial or absence without foreign data in the response.
- [ ] Attempt foreign references in create, update, stock, publication and reservation requests. Confirm rejection and unchanged other-business state.
- [ ] Verify unassigned staff and customers cannot gain business access through forged body/query values, global grants or public preview flags.
- [ ] Confirm wholesale staff cannot manage the legacy demo jewelry module, while anonymous jewelry images expose only approved active demo records.
- [ ] Revoke membership or grants and retry with the existing cookie. Confirm current access is enforced and a request cannot rebind its repository to another business mid-request.
- [ ] Inspect health responses for absence of business data.

### Provisioning and SQL boundaries

- [ ] In an isolated Development/Sample host, exercise the host-only provisioning CLI with an eligible unassigned staff account and a newly generated owner. Confirm the new portal starts offline, invalid/duplicate/reserved slugs are rejected and customers cannot become owners.
- [ ] Confirm generated credentials appear only once in the operator terminal and are not committed or logged. Sample Identity accounts and generated credentials are memory-only for that host lifetime. Do not treat a separate provisioning process as durable account creation for a later host.
- [ ] Confirm a provisioned Sample business without catalog fixtures starts empty without falling back to demo data.
- [ ] Review `docs/business-portal-schema.sql` as future-only SQL prepared after `docs/sql-demo-schema.sql`. It is not required by the current runtime. Do not execute either file during this QA.
- [ ] Verify SQL startup remains pinned to exactly `nb-jewel-demo` and fails closed when a second business is configured. Multi-business SQL is unsupported. Offline DI/schema contract tests do not constitute SQL integration execution.

### Eight repeatable clean gates

Run backend child processes with SQL connection variables absent. The clean environment below intentionally supplies only runtime essentials and Sample provider settings. Use the existing approved cutout interpreter without installing removal tooling.

```sh
env -i PATH="$PATH" HOME="$HOME" Repository__Provider=Sample CUTOUT_PYTHON_PATH=/tmp/nbjewel-cutout-runtime-venv/bin/python dotnet test backend/NbJewel.Api.Tests --no-restore
env -i PATH="$PATH" HOME="$HOME" Repository__Provider=Sample CUTOUT_PYTHON_PATH=/tmp/nbjewel-cutout-runtime-venv/bin/python dotnet build backend/NbJewel.Api --no-restore
npm --prefix frontend test
(cd frontend && npx tsc --noEmit -p tsconfig.app.json)
NG_BUILD_MAX_WORKERS=2 npm --prefix frontend run build
npm --prefix customer-tryon test
(cd customer-tryon && npx tsc --noEmit -p tsconfig.app.json)
NG_BUILD_MAX_WORKERS=2 npm --prefix customer-tryon run build
```

Record fresh results and logs when these commands run. Keep conditional SQL integration tests skipped. A known sandbox Angular worker abort requires rerunning the same build with approved escalation, without changing dependencies or settings.

### Automated test references

Backend: `BusinessContextTests`, `BusinessMembershipHttpTests`, `ProvisioningCommandTests`, `ImporterCompatibilityTests`, `BusinessIsolationHttpTests`, `BusinessSqlBoundaryTests`, `CustomerShoppingStateTests`, `CustomerPortalHttpTests`, `PortalLifecycleHttpTests`, `PriceHidingHttpTests`, `ShopPublicHttpTests`, `PortalEditorChoicesTests`, and `PlatformConversationTests`.

ERP: `frontend/src/app/features/web-portal/web-portal.spec.ts`.

Storefront: `customer-tryon/src/app/catalog/business-catalog.spec.ts`, `business-routing.spec.ts`, `store.service.spec.ts`, `catalog.service.spec.ts`, `customer-tryon/src/app/customer/store-directory.service.spec.ts`, and `customer-tryon/src/app/pages/tryon-navigation.spec.ts`.

### Fresh evidence, 2026-11-16

The implementation agent completed a local route/cache review and the Sample checks below. **Independent review pending** for Claude. This is not a production security certification.

The browser run used the existing HTTP test factory with real Identity password/cookie/CSRF/permission middleware, EF InMemory stores and isolated asset/portal folders under `/tmp`. A temporary runner listened only on `127.0.0.1:5156`; copies served from the existing Angular builds used `127.0.0.1:4201` and `:4301`, with nonsecret test API/preview address overrides. No repository runtime config or owner service was changed. Synthetic retail/wholesale/customer identities existed only for that host lifetime; generated passwords were terminal-only. The assumed wholesale fixture explicitly enabled bag and disabled enquiries for this check. Its shipping preset still starts offline with customer actions off and needs a real WhatsApp destination before enquiries can publish.

| Browser check | Observed result |
| --- | --- |
| Two brands | Same template rendered Assumed Rose Retail and Assumed Silver Wholesale independently, including 390px mobile and 1280px desktop checks. |
| Draft versus live | Saving the retail name/headline/preset kept the live NB Jewel publication unchanged. Authenticated mobile/desktop saved-draft preview showed the new branding with disabled shopping/wishlist controls. Publishing updated live branding. |
| Hidden wholesale | Product `WS-ONLY`, item 7, displayed its synthetic image, name and 10 g weight with no price, making charge, breakdown or selling-rate header. Its bag had no price or total. |
| Existing image workflow | Uploaded repository synthetic necklace to demo item 7, inspected the actual ready preview, manually saved sample copy, explicitly approved only that synthetic preview and published it. No real photo approval or AI provider success is claimed. |
| Edit without rebuild | Retail item name changed to Assumed edited retail necklace; making percentage 10.89 to 20.89 changed the customer total from AED 2,403.78 to AED 2,620.56 after reload. |
| Offline | Taking retail offline made the product route unavailable; authenticated draft preview still worked. After restoring retail, wholesale was separately taken offline and also became unavailable. |
| One customer, two bags | A single customer login resumed the retail add intent. Switching to wholesale item 7 initially showed Add to bag, then its own bag contained only WS-ONLY. Returning to retail showed only NKL-0007 with its retail price. |
| Preview access | After changing from staff to customer, opening the private retail preview showed Preview unavailable and no draft content. |
| Revoke and legacy URL | Revoking retail item 7 removed its public detail; wholesale item 7 stayed visible. Legacy `/p/7?source=erp&store=1&parts=necklace` redirected to the demo slug with store/parts intact. |
| End state | Browser signed out, viewport override reset, temporary QA listeners stopped, and the existing API on localhost:5155 remained running. No test publication was written to its storage. |

Local screenshot evidence: `/tmp/nbjewel-E-wholesale-mobile.jpg`, `/tmp/nbjewel-E-wholesale-desktop.jpg`, `/tmp/nbjewel-E-retail-bag-desktop.jpg`. These are synthetic Sample evidence, not deployment artifacts. The temporary host harness was not added as a production startup mode.

Final gates: backend **447 passed, 7 SQL skipped, 0 failed**, backend build **0 warnings/errors**; ERP **79 passed**, storefront **88 passed**; both strict typechecks and both bounded production builds passed. Logs: `/tmp/nbjewel-E-final-{backend-test,backend-build,erp-test,erp-typecheck,erp-build,store-test,store-typecheck,store-build}.log`. Importer compatibility and SQL adapter/DI setup without any connection or new table are included in the backend suite. No linter target exists. No SQL, migration, Docker or external model/feed/SMTP operation ran.

### Route and cache review record

Every row in plan section 3 was reviewed at task D checkpoint `b5ffeee`, including the D additions. No further material implementation finding required a code change during E. Earlier preview cache/authentication and store-filter findings were fixed and regression-tested in D.

| Section 3 surface | Enforcement and automated evidence |
| --- | --- |
| Items storage, list/detail, stock/variants/price, create/update/status and references/rates/low stock | Middleware resolves membership before controller DI; permission/store filtering remains inside that business. `BusinessMembershipHttpTests`, `ImporterCompatibilityTests`, `BusinessIsolationHttpTests`. |
| Upload, analyze, review and private image preview | Owning partition options/repository/pipeline, exact revision validation and explicit ERP permissions. `BusinessIsolationHttpTests`, `ItemCutoutHttpTests`, `JewelryVisionTests`. |
| Legacy chat and knowledge documents/product publish/revoke/status | Fresh membership, own catalog/knowledge/publication stores, shared public eligibility. `BusinessIsolationHttpTests`, `KnowledgeAdminHttpTests`, `LegacyChatEndToEndTests`. |
| ERP conversations and website conversations | ERP owner/fingerprint include business; website stays demo-only and carries publication policy. `BusinessIsolationHttpTests`, `PriceHidingHttpTests`, `PlatformConversationTests`, `ScopedAssistantToolsTests`. |
| Legacy jewelry active/admin and jewelry-images files | Fixed demo repository; non-demo management denied; file path/name and active visible-type metadata checked on each image read. `BusinessIsolationHttpTests`, `AuthorizationBoundaryTests`, `JewelryRepositoryTests`. |
| Legacy storefront products/detail/preview/rates/stores/spot reference | Fixed demo context, live portal policy, fresh publication/asset checks, no-store; spot reference intentionally shared. `PublicCatalogHttpTests`, `PriceHidingHttpTests`, `ShopPublicHttpTests`. |
| Auth/CSRF/registration/me, global customer profile, legacy shopping aliases | Identity remains global; customers never gain staff membership, staff cannot shop; old shopping aliases target demo. `BusinessMembershipHttpTests`, `IdentitySessionTests`, `CustomerPortalHttpTests`, `CustomerShoppingStateTests`. |
| Shops config/assets/catalog/stores/rates and per-shop customer state/actions | Route slug binds before DI; offline/unknown empty 404, current settings and own published references govern reads/mutations. `PortalLifecycleHttpTests`, `PriceHidingHttpTests`, `ShopPublicHttpTests`, `CustomerShoppingStateTests`. |
| Portal draft/save/upload/assets/choices/stores/preview/product preview/publish/offline | Explicit manage or publish action map, matching membership, CSRF, revision conflicts, business-owned references and atomic publication envelope. `PortalLifecycleHttpTests`, `PortalEditorChoicesTests`, ERP web-portal spec. |
| Health/live and health/ready | Fixed status-only response or generic unavailable; no business payload. Reviewed minimal endpoint bodies. |
| Process caches and browser caches | Repository/pipeline cache keyed by business; knowledge database namespace includes host/business; assistant owners/fingerprints include business, evidence cache is request-local. Market reference alone is global. Catalog cancellation, directory business plus preview scope, wishlist business/user keys and customer request sequences are tested in business/catalog/directory/store/conversation specs. |

The repeatable manual checklist above remains useful for real devices, permitted representative photos, actual WhatsApp destinations and independent review. Real cutout fit/licensing, camera/export, live providers, SMTP, production hosting and SQL persistence were not verified here.

### Owner-run SQL and provisioning steps

1. Preserve the existing Sample session/assets and follow [SQL catalog QA](SQL_CATALOG_QA.md) for any later database/schema/reference/import work. The agent did not execute it.
2. SQL mode supports exactly `nb-jewel-demo`. A second registry business makes startup fail closed. ERP SQL accounts implicitly belong to demo (AllStores owner, otherwise staff); customers do not. Catalog, image metadata, publications, locations and customer records keep their existing tables and keys.
3. Portal drafts/publication/state/audit/assets use atomic files under `BUSINESS_STORAGE_PATH` in both providers. Keep this filesystem durable and writable. Customer v2 per-shop state stays inside the existing CustomerPortals JSON/Revision row; no new membership/shopping table or data migration is required by v1.
4. `docs/business-portal-schema.sql` is future-only, ordered after `docs/sql-demo-schema.sql`, additive with transaction/IF OBJECT_ID guards and no data or existing-table/UNIQUE changes. Review it independently; do not apply it just to enable these portals. The seven SQL tests remain skipped until separately authorized connections/setup exist.
5. For Sample operator onboarding only, activate the Development memory-only identity options with SQL/connection settings absent, then use `dotnet run --project backend/NbJewel.Api -- --provision-business --slug <new-slug> --name <assumed-name> --kind retailer|wholesaler --owner-email <email>`. Supply runtime credentials through the operator's existing safe launch environment, never a committed file. The command keeps its own host serving; it cannot share in-memory accounts with a separately running API. Existing assigned members/customers and duplicate/reserved slugs are rejected. New businesses start offline and have an empty catalog until their own data is supplied. Never stop an occupied owner service automatically.
