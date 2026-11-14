<!-- Role: Records origin reconciliation, evidence for the eight requested features, verified fixes, and remaining configuration or implementation gaps. -->
# Origin reconciliation and eight-feature audit

## Preservation and authoritative baseline

- Backup: `backup/local-accidental-run-20261114-113421`, commit `d98c7b52a326a87d4a93aed568708a1d7d7693ea`.
- Captures all six initially modified/untracked files and the complete original commit ancestry. Ignored secrets, installed dependencies and runtime assets remain in place, unchanged. Other existing local branches remain intact.
- Owner explicitly authorized overriding protected-file exclusions for backup and alignment after automatic approval review blocked the first attempts. Those exclusions still apply to subsequent cleanup.
- Live `git ls-remote --heads origin` returned only `demo-changes`, `tryon-feature`, and `feature/customer-storefront`. There is no remote main/master. Remote HEAD is demo-changes, an older ERP demo. The storefront branch is the appropriate existing implementation baseline.
- Fetched all remotes, reset the existing storefront checkout to `origin/feature/customer-storefront`, then ran `git pull --ff-only`. Baseline HEAD and remote both equal `5e6066f2dcee8fd49f7086cc34cd1630ade5ca57`; working tree was clean. No semantic merge was performed.
- There was no local commit divergence on that branch. The accidental local state comprised three tracked modifications and three untracked files. Existing local-only commits on other branches were retained, not merged or reset.

## Audit plan and constraints

- [x] Preserve local state and verify exact remote alignment.
- [x] Track A: compare environment key names, correct example configuration, inspect clutter and lockfile parity. Owns only `.env.example` and `.gitignore`.
- [x] Track B: audit all eight requirements and repair reproduced backend consistency bugs. Orchestrator owns backend sources/tests, this report and the sole handoff.
- [x] Track C: isolated dependency installation, typechecks, builds and sample tests. No product source changes.
- [x] Review final diff and record test evidence. Final checkpoint releases the lock; commit/push verification follows. Only `origin feature/customer-storefront` is an authorized push target.

Architecture for a reproduced assistant visibility gap: reuse one eligibility policy across public catalog and website assistant reads, including history evidence revalidation. Regression cases must prove that a published product moved to Draft or an excluded category/metal becomes unavailable. Preserve ERP tool permissions and existing publication/asset review gates. Run targeted red/green tests and the full sample backend suite. No new services or UI redesign.

All runtime/tests use Sample with SQL connection variables removed. SQL files are reviewed only, never applied. Cutout review and licensing gates remain. Do not infer participating stores, current market rates, customer policies or approval from sample data. Serena is unavailable in the tool inventory; repository reference searches, TypeScript and C# compilers are the fallback. Headroom is available and used for command-output compression.

## Feature findings

| # | Requirement | Finding at authoritative baseline |
| --- | --- | --- |
| 1 | Product card alignment | Implemented: shared flex card, 4:5 image frame and fixed 160px details; responsive visual review still manual. |
| 2 | Catalog exclusions and camera/photo preview | Implemented for necklace/earring catalog; ring/watch/bracelet excluded; camera, uploaded-photo preview, retake/save/share exist. Device and real-image quality checks remain manual. |
| 3 | Customer flow | Anonymous browsing and header/bag exist. Bag/reservations are browser-local. No storefront login gate, customer profile or server cart. Backend Identity foundation exists. This is missing implementation, not only configuration. |
| 4 | ERP/SQL/cutout pipeline | ERP upload runs existing cutout script, stores revisions, requires explicit quality approval/publication, then public API reflects updates. Default storefront config selects bundled demo. No automatic background queue or arbitrary SQL change listener; SQL schemas require manual provisioning/migration review. |
| 5 | Live transparent pricing | Timestamped recorded ERP/sample rates and price breakdown exist; no external live spot-price provider or scheduler. Do not label sample prices live. |
| 6 | Multiple metals | Gold, Silver, Platinum and matching purity rates implemented. No other metals are supported by the public allowlist; external market refresh is absent. |
| 7 | Customer chatbot and researched FAQ | Grounded website assistant backend foundation exists, but no customer chatbot UI. FAQ file is a research/question inventory, not an approved populated answer knowledge base. Fixed product visibility inconsistency: tools and history now reject Draft and unsupported category/metal products even if a publication record remains. |
| 8 | ERP store locator | Not implemented. Store model has only id/name/city; no coordinates, public participation contract, sync heartbeat, nearest-store query or map UI. A Maps key alone cannot enable this. |

The claim that all eight features were already completed is not supported by any current origin branch. Missing product domains are recorded explicitly instead of being presented as working or merely disabled by absent credentials.

## Fixes and forensic findings

1. Fixed a reproduced assistant boundary bug. Before the fix, three ineligible products returned `ok`, and history evidence remained valid after a published necklace moved to Draft. `Models/PublicCatalogPolicy.cs` now supplies the same Complete/category/metal policy to `Controllers/PublicCatalogController.cs`, `Platform/AssistantTools.cs`, and `Platform/AssistantEvidence.cs`. Positive fixtures remain eligible; public search retains batched paging. Additional tests cover category and unsupported-metal changes after publication. ERP grants and asset approval checks are preserved.
2. Corrected `.env.example`: added actual identity/platform, cutout, hosting, SMTP, deployment and sample settings; documented both allowed development origins and unset-versus-empty defaults. Optional connection/credential settings stay commented to avoid activating SQL or invalid protection settings. No real values were added.
3. No dead tracked build/cache artifacts or clear missing ignore entries were found. `.gitignore` already covers dependencies, build products, secrets, local tool folders and runtime image data. No speculative deletion or component consolidation was performed.
4. Both package-lock v3 root dependency sets match their manifests. Isolated `npm ci` succeeded for both apps; lockfiles stayed byte-identical. Existing install warnings remain: deprecated `uuid@8.3.2`, and the ERP Angular builder's optional Vitest `^3.1.1` peer versus pinned `5.0.5`. These did not fail installation, typecheck, tests or build. No speculative package downgrade was made.
5. No `process.env`/`import.meta.env`/Python environment mechanism replaces .NET configuration. The storefront runtime address is `customer-tryon/public/storefront-config.json`; `NBJEWEL_API_URL` belongs to the ERP frontend deployment generator. Confusing these leaves the customer site in bundled mode.

## SQL and synchronization review

- `docs/item_catalog_schema.sql` is an assumed sample schema for empty databases. `SqlDatabaseSetup` skips setup when Item already exists; it is not an upgrade migration. Existing databases need reviewed migration planning for `Item.StorefrontJson` and `Item.VatPercent` (and parity checks against the current adapter). No incremental migration runner was found.
- `docs/identity-schema.sql` defines Identity; `docs/platform-schema.sql` defines conversation/knowledge/publication storage. Existing platform schemas need `AiPublicationProducts.AssetRevisionId` and an externally assigned, non-IDENTITY `ItemId` contract. Do not rerun CREATE TABLE scripts blindly on existing databases.
- Catalog reads use `SqlItemRepository` directly when Sql is configured. No SQL trigger, CDC listener, message queue or ERP ingestion worker exists. This is not a deployed ERP-to-Web background synchronization service.
- The existing upload route invokes `scripts/cutout-jewelry.py` in a bounded subprocess. It is not a separate queue worker. Source/preview revisions use filesystem storage, including in SQL catalog mode; persistent asset storage must be provisioned alongside SQL.
- Publication intentionally requires explicit review of the current preview revision and a Complete supported product. New uploads do not auto-approve or auto-publish. This preserves the required manual quality and licensing boundaries.
- All SQL review was static/offline. No database connection, setup, seed, migration or Docker action was executed. Six opt-in SQL tests remain skipped.

## Exact local configuration and external prerequisites

Local `.env` was inspected for key names only. `GEMINI_API_KEY`, `GEMINI_MODEL`, and `NBJEWEL_API_URL` are present; their values/validity were not displayed or tested. The following application keys are absent from that file. Absence is acceptable for optional/default settings and does not prove they are absent from a separately configured shell or host.

| Purpose | Keys absent from local .env | Requirement |
| --- | --- | --- |
| Sample repository and hosting | `Repository__Provider`, `ASPNETCORE_ENVIRONMENT`, `DOTNET_ENVIRONMENT`, `ASPNETCORE_URLS` | Use Sample and Development for local checks; launch settings/launcher supply hosting defaults. |
| ERP catalog SQL | `NBJEWEL_SQL_CONNECTION` | SQL Server connection string, not a generic DB URL; needed only for Sql mode after reviewed schema provisioning. Keep unset for sample work. |
| Persistent accounts | `NBJEWEL_IDENTITY_CONNECTION` | SQL Server Identity database and reviewed schema. Does not create a storefront profile/cart UI. |
| Conversations and approved FAQ | `AI_PLATFORM_CONNECTION`, `AI_DATA_PROTECTION_SECRET` | Platform SQL (falls back to identity connection only when unset). Outside Development, supply a secret base64-encoded 32-byte protection key. Gemini configuration alone cannot enable durable website chat. |
| Verification email | `SMTP_HOST`, `SMTP_FROM`, `SMTP_PORT`, `SMTP_USERNAME`, `SMTP_PASSWORD`, `APP_PUBLIC_ORIGIN` | Working SMTP sender and correct app origin when verification is required; port defaults to 587, credentials depend on sender. No emails sent in this audit. |
| Cutout and assets | `CUTOUT_PYTHON_PATH`, `ITEM_ASSET_STORAGE_PATH`, `JEWELRY_STORAGE_PATH` | Existing Python interpreter with Pillow/numpy/scipy; persistent writable assets. Default interpreter is python3. Temporary test interpreter is not durable deployment configuration. No cutout API key/worker service exists. |
| Browser/proxy hosting | `CORS_ORIGINS`, `TRUSTED_PROXIES` | Configure actual origins/proxies outside supplied localhost defaults; cookie-based accounts require compatible same-site hosting. |
| Optional administration | `NBJEWEL_BOOTSTRAP_EMAIL`, `NBJEWEL_BOOTSTRAP_PASSWORD`, `OWNER_PIN` | Existing explicit admin/bootstrap or legacy upload paths only. Do not put real credentials in tracked files. |
| Optional runtime controls | `GEMINI_THINKING_LEVEL`, `SampleIdentity__Enabled`, `SampleIdentity__StaffPassword`, `AiSecurity__RequireEmailVerification` | Prefer the sample launcher for temporary staff login; it generates credentials at runtime. It deliberately disables customer registration. |
| Not used during this audit | `MSSQL_SA_PASSWORD`, `NBJEWEL_PLATFORM_TEST_CONNECTION` | Docker/opt-in SQL tests only; left unset. |

For the existing local ERP-to-storefront demo, the nonsecret JSON `apiBaseUrl` must explicitly select `http://localhost:5155` instead of its committed `null` bundled-demo default. Then an authorized staff user uploads, reviews and publishes a supported sample product. An empty API catalog is expected until publication. No automatic fallback may resurrect a revoked product.

There is no consumed metal-market API key or Maps API key in this codebase. Making requirements 5 and 8 real requires implementing a selected provider/adapter and supplying authoritative store coordinates, public participation permission, sync timestamps and inventory mapping. Inventing environment names would not enable those features. Requirement 3 needs storefront authentication/profile/cart implementation; requirement 7 needs customer chat UI, approved FAQ answers and platform storage. A key-only setup cannot run all eight requirements.

## Verification

Commands were executed in sanitized child environments: `Repository__Provider=Sample`; SQL/connection variables removed. Frontend install/build checks used isolated copies under `/tmp/nbjewel-trackc-verification`, preserving live dependencies and protected lockfiles.

| Command | Result |
| --- | --- |
| `dotnet build backend/NbJewel.Api --no-restore` | Passed, zero warnings/errors. |
| `dotnet test backend/NbJewel.Api.Tests --no-restore` | Passed: 380 tests, 6 SQL skipped, 0 failed. Baseline was 374 passed / 6 SQL skipped. |
| `npm ci --no-audit --no-fund` in each isolated Angular app | Passed; lockfiles unchanged. |
| `npx tsc --noEmit -p tsconfig.app.json` in each app | Passed. |
| `npm test` in ERP frontend | 72 passed across 11 files. |
| `npm test` in customer storefront | 64 passed across 6 files. |
| `NG_BUILD_MAX_WORKERS=2 npm run build` in each app | Passed. |
| Lint | Not run: neither app has a lint script/config. Compilers are not a substitute for a configured linter. |
| `git diff --check` | Passed. |

Logs: `/tmp/nbjewel-trackc-verification/`; failing-before-fix evidence `/tmp/nbjewel-reconciliation-red.log`; initial green suite `/tmp/nbjewel-reconciliation-green.log` (378 passed, 6 skipped); final reviewed logs `/tmp/nbjewel-reconciliation-final-build.log` and `/tmp/nbjewel-reconciliation-final-tests.log`.

Initial sandbox socket/build restrictions and install network restrictions required approved retries; those retries passed. Serena was unavailable, so no Serena verification is claimed. Independent medium-reasoning review found no critical/important defect; minor test coverage and environment-default wording observations were addressed.

## Manual checks and remaining implementation

- Visual card alignment on target phone widths, camera permission/capture, uploaded-photo fit and native share/export are not newly browser-tested in this audit.
- Review representative permitted cutouts and licensing before publishing real images. Synthetic tests do not approve those images.
- Provision and validate SQL separately under explicit authorization. Six skipped tests are not SQL verification.
- Finish customer account/profile/cart UI and authenticated persistence; no login is currently required to add to the browser-local bag.
- Select and implement the market-rate feed, define quote currency/unit conversion and freshness rules, and keep spot rates distinct from ERP selling rates.
- Implement customer chat UI and ingest explicitly approved researched FAQ answers. The existing FAQ inventory does not constitute an approved answer corpus.
- Define the public participating-store/location/sync-health contract and implement its backend/map UI. Existing sample store city labels cannot establish real ERP participation or current inventory synchronization.

These are incomplete implementation items, not hidden successful features. The reconciliation and reproduced bug repair do not amount to completing all eight product requirements.

## Checkpoint identity

Final report and lock release are committed together. Resolve the reconciliation commit with `git log -1 --format=%H -- RECONCILIATION_REPORT.md`; the original origin baseline and local backup remain recorded above. The backup branch remains local as requested.
