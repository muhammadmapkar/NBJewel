<!-- Role: Records phase 2 scope and review evidence; session continuity remains only in AI-MEMORY-HANDOFF.md. -->
# Customer storefront phase 2 gate

## Scope and assumptions

Sample-only internship demo. Assumed category mapping and storefront copy are demo policy, not confirmed NB Jewel internals. Work is confined to `feature/customer-storefront`; no merge, deployment, Docker action, SQL execution, production data or account provisioning.

## Completed implementation

1. Optional storefront metadata and labelled Draft platinum sample retained through repository operations.
2. Anonymous read-only product/rate endpoints expose only published Complete allowed products through narrow DTOs.
3. Metal, making, stones, configured VAT and total reconcile; recorded rate dates and change evidence are explicit.
4. Existing cutout script creates durable item-linked source/preview revisions. Generation never approves quality or licensing.
5. ERP Images tab uploads, reviews, publishes and revokes with existing authentication, CSRF and permissions. File-backed sample publication decisions bind to the current approved revision.
6. Nonsecret runtime configuration chooses API or bundled catalog. API failures do not restore stale bundled products.
7. Metal filters, matching metal/purity rates, recorded header dates and product explanations; unknown bundled dates stay unknown. Cents remain visible.
8. Backend HTTP and storefront service proofs read publication, changed copy/prices and revocation without a storefront rebuild. A page reload or explicit service refresh fetches updates; no polling claim.
9. Unused ShopPage RouterLink and Vitest config-loading warnings corrected. README updated for actual protected/sample behavior.

## Automated gate

All six checks passed on 2026-11-14:

| Check | Result |
|---|---|
| `dotnet build backend/NbJewel.Api` | Passed, zero warnings/errors |
| `dotnet test backend/NbJewel.Api.Tests` | 349 passed, 6 SQL skipped, zero failed |
| ERP `npm test` | 71 passed |
| ERP `NG_BUILD_MAX_WORKERS=2 npm run build` | Passed |
| Storefront `npm test` | 64 passed, config warning removed |
| Storefront `NG_BUILD_MAX_WORKERS=2 npm run build` | Passed, unused RouterLink warning removed |

SQL integration checks are intentionally skipped with all SQL/connection variables removed and Sample forced. Test image data is synthetic, not client photos. Backend logs: `/tmp/nbjewel-task9-backend-build.log` and `/tmp/nbjewel-task9-backend-test.log`.

Lead review is the next gate; no verdict is claimed yet.

## Manual gate for Muhammad

- Inspect phone and desktop alignment, including recorded-rate details and metal filters.
- Camera capture, Retake, Save and native share on a real device.
- Inspect permitted sample cutout quality before approving publication; licensing remains open.
- Real browser cross-origin images/canvas, cookies and CSRF with explicitly allowed origins.
- Full staff Images-tab upload, copy, revision review, publication and revocation with an existing legitimate staff session. Default unconfigured identity cannot sign in and no staff account was created.

## Persistence and readiness limits

Assets and sample publication decisions survive restart on the same persistent filesystem. Sample item edits remain memory-only. SQL adapter/schema changes are prepared files and have not been applied or validated against a server. Backend tests use isolated test identities rather than inventing a demo account. API earring preview placement assumes a reviewed single earring can be used on both sides; visual fit remains manual.

This gate is for reviewing a sample demo. It is not approval for production, a tax rule, source-photo rights, cutout visual quality, merge or deployment. Accounts, chatbot enhancements and locator remain later phases. Stop after the lead review and wait for Muhammad.
