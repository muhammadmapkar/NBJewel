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

## Lead review and corrections

A fresh sequential read-only reviewer assessed `a4f8ef3..87c5c19` as ready with fixes. No critical findings; three important findings were accepted and corrected:

- Public preview/detail/list could authorize approved revision A and resolve pending replacement B. Authorization now returns the exact revision through metadata/preview delivery; stale requested revision URLs fail closed. All three deterministic interleaving regressions failed before the fix and pass after it.
- ERP Images saved copy stayed only in the child and reverted after tab teardown. The child now emits the returned Item; the parent retains it. A real component-class lifecycle regression saves, destroys/recreates Images and verifies the next Save keeps the new copy.
- New memory-only samples could reuse IDs whose old approved assets/publications persisted. An exclusive filesystem reservation keeps IDs monotonic across restart and scans legacy asset/publication IDs. HTTP restart and legacy/corrupt-reservation tests cover the boundary. Full item persistence remains excluded.

Both minor findings were also addressed: the prepared publication ItemId is externally assigned in EF and schema (model-only test, no SQL connection), and the public category allowlist now matches the implemented necklace/earring upload and API storefront contract. Unsupported future categories remain hidden by regression tests.

Final affected checks passed: backend build zero warnings/errors; backend 357 passed, 6 SQL skipped, zero failed; ERP 72 passed and production build passed. Storefront 64 tests/build passed at the six-check gate and its source is unchanged by the review corrections. Logs: `/tmp/nbjewel-review-final-backend-build.log` and `/tmp/nbjewel-review-final-backend-test.log`. All lead-review findings are addressed for the sample-demo gate; manual checks remain open. The reviewer did not independently run tests; execution evidence comes from the implementing agent. No second reviewer or merge was requested.

## Manual gate for Muhammad

- Inspect phone and desktop alignment, including recorded-rate details and metal filters.
- Camera capture, Retake, Save and native share on a real device.
- Inspect permitted sample cutout quality before approving publication; licensing remains open.
- Real browser cross-origin images/canvas, cookies and CSRF with explicitly allowed origins.
- Full staff Images-tab upload, copy, revision review, publication and revocation with an existing legitimate staff session. Default unconfigured identity cannot sign in and no staff account was created.

## Persistence and readiness limits

Assets, sample publication decisions and new sample item ID reservations survive restart on the same persistent filesystem. Reservation metadata must stay with the assets to prevent ID reuse. Sample item edits remain memory-only. SQL adapter/schema changes are prepared files and have not been applied or validated against a server. Backend tests use isolated test identities rather than inventing a demo account. API earring preview placement assumes a reviewed single earring can be used on both sides; visual fit remains manual.

This gate is for reviewing a sample demo. It is not approval for production, a tax rule, source-photo rights, cutout visual quality, merge or deployment. Accounts, chatbot enhancements and locator remain later phases. Stop after the lead review and wait for Muhammad.

## Authorized local runtime follow-on

Muhammad subsequently authorized API mode, legitimate local sample staff sign-in and the browser publication lifecycle. Runtime config now targets `http://localhost:5155`. `scripts/run-sample-api.py` enables one explicit Development/Sample/localhost-only in-memory staff identity with a fresh generated runtime password. Existing password hashing, lockout, cookies, grants and CSRF apply; customer registration is denied in this mode. SQL/connection configuration, public bindings, configured Kestrel endpoints and proxies are rejected. Production/default identity behavior is unchanged.

Native Chrome verification passed: sign in, upload repository synthetic necklace, inspect transparent preview, save labelled copy, approve the revision, publish, observe actual image/copy/dated price in customer app, edit ERP name/copy/making, reload customer app without rebuilding, revoke, and observe unavailable detail plus zero shop designs. Price changed from AED 2,403.78 to AED 2,620.56. Test publication remains revoked. A later API restart expired the old browser session, and fresh staff login succeeded. Password-save prompts were declined. Only the synthetic fixture was approved; real photos/licensing remain manual.

Sequential read-only runtime review found customer registration exceeded single-staff scope; the denial regression failed before the correction and passes after it. Lockout and old-cookie/password restart checks were added. Final backend: 374 passed, 6 SQL skipped; storefront API-config check: 64 tests/build passed. No Angular source changed in this follow-on. Local runtime services remain available; credentials and sample item edits reset on restart. No SQL, Docker, deployment or merge took place.
