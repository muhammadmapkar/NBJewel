<!-- Role: Explains the sample demo, safe local startup, storefront runtime configuration and phase 2 verification. -->
# nbjewel-assistant

An internship demo for Cetas (jewelry retail ERP, NB Jewel). The ERP Angular app, customer try-on Angular app and .NET API use synthetic sample data. Unconfirmed business rules are assumed demo policy, not NB Jewel internals. No real client data is permitted.

## Project map

- `frontend/`: staff ERP app with the existing theme, catalog, Images tab and chat panel.
- `customer-tryon/`: customer catalog, filters, product breakdown, camera try-on and local wishlist/bag.
- `backend/NbJewel.Api/`: .NET API, fixed read-only chatbot tools, protected ERP mutations and public storefront reads.
- `backend/NbJewel.Api.Tests/`: sample repository and HTTP integration tests. SQL checks require a separately authorized server and remain unexecuted in this phase.
- `sample-data/`: synthetic catalog fixtures, including the explicitly labelled Draft platinum sample. There are 31 ERP items; publication determines the public subset.
- `scripts/cutout-jewelry.py`: existing cutout algorithm. `scripts/build-tryon-catalog.py`: existing bundled catalog builder.
- `docs/item_catalog_schema.sql` and other SQL schema files: prepared contracts only. No schema or data changes were applied for phase 2.
- [Phase 2 gate record](docs/superpowers/plans/2026-11-14-customer-storefront-phase-2.md): scope, test evidence and manual review boundaries.
- `AGENTS.md` and `AI-MEMORY-HANDOFF.md`: agent rules and the only session handoff/lock.

## Local sample startup

Requirements: .NET 10 SDK and Node.js/npm compatible with the repository's Angular toolchain. Use existing installed dependencies. If dependencies are absent, install them using the applicable checked-in lockfile in each app directory. Do not modify the owner's storefront lockfile during agent work.

The catalog needs no model API key. Keep all SQL and connection environment variables unset. These commands do not read a secrets file. Start each service in a separate terminal from the repository root:

```bash
# Terminal 1: sample API, http://localhost:5155
python3 - <<'PYTHON'
import os, subprocess
env = {k: v for k, v in os.environ.items() if 'CONNECTION' not in k.upper() and 'SQL' not in k.upper()}
env['Repository__Provider'] = 'Sample'
subprocess.run(['dotnet', 'run', '--project', 'backend/NbJewel.Api', '--launch-profile', 'http'], env=env, check=True)
PYTHON

# Terminal 2: staff ERP, http://localhost:4200
npm --prefix frontend start

# Terminal 3: customer storefront, http://localhost:4300
npm --prefix customer-tryon start
```

Staff routes require a legitimate ERP session, permissions, all-store scope where required, and CSRF protection. With the default unconfigured identity provider, staff sign-in is unavailable. The explicit local sample launcher below enables one temporary staff identity through the existing Identity password/cookie pipeline. No authentication bypass is added. Public storefront reads can return an empty catalog without a staff session or database. Accounts, chatbot enhancements and locator work are later phases.

For the authorized local staff demo, use this API command instead of Terminal 1 above:

```bash
CUTOUT_PYTHON_PATH=/path/to/prepared/python python3 scripts/run-sample-api.py
```

The launcher prints `sample.staff@nbjewel.invalid` and a freshly generated temporary password to that terminal. Sign in at `http://localhost:4200/sign-in`. Do not save the password in the browser or a file. The account/password/session expire on API restart; run the launcher again for new credentials. This is an assumed sample staff identity with only catalog/read/status/upload/publication permissions, not a real NB Jewel role mapping.

This opt-in mode requires Development, Sample ERP data and no SQL/connection settings. It binds only `localhost:5155`, rejects nonloopback peers/hosts and configured proxies/Kestrel endpoints, and retains existing password hashing, lockout, session cookies, CSRF and per-action grants. Customer registration is disabled in this single-staff sample mode. Default and production identity behavior is unchanged. The launcher does not read `.env`; it removes inherited connection/credential settings and isolates sample assets under `App_Data/local-sample-demo`. For manual launch, opt-in uses `SampleIdentity__Enabled=true` and a strong runtime-only `SampleIdentity__StaffPassword`; never commit credentials.

The chatbot stays inside the .NET API and answers only from ERP data returned by fixed read-only tools. It has no free SQL and must not answer from model memory. Any model credentials belong only in backend environment variables. Model availability was not tested in this phase.

## Storefront source and rates

`customer-tryon/public/storefront-config.json` is a public, nonsecret runtime configuration. Its committed `apiBaseUrl` is `http://localhost:5155`, which selects the local API. Set it to `null` to select the bundled sample catalog. Set the address to `http://localhost:5155` for local API mode, then reload the customer page. On a deployed static host, changing the served config file selects a source without rebuilding JavaScript. No deployment was performed here.

A missing config file (HTTP 404) selects the bundled catalog. Invalid config, unavailable API, malformed data or incomplete pagination shows the catalog error UI and does not restore bundled products. API mode loads all published pages and server-calculated prices. Reload the page to read publication/copy/price changes; the service also supports explicit refresh. It does not poll or promise live updates to an already open page.

Gold, Silver and Platinum match rates by both metal and purity. API rate dates and change explanations come from recorded data, including historical sample dates. Bundled Gold rates have no recorded effective date, which the UI states explicitly. No date is inferred from the current day. Prices show metal, making, stones, configured VAT and a reconciled total. Zero VAT on legacy inputs is an explicit compatibility default, not tax advice. Recorded-rate comparisons hold current item inputs constant and are not historical sales invoices.

Anonymous read-only routes:

- `GET /api/storefront/products` with bounded paging.
- `GET /api/storefront/products/{id}`.
- `GET /api/storefront/products/{id}/preview`.
- `GET /api/storefront/metal-rates`.

The API rechecks publication and returns a narrow public DTO with no ERP stock/store fields. Only Complete items in allowed demo categories/metals with a reviewed current cutout can appear. Revocation hides the product and preview on subsequent reads. Default CORS allows localhost ports 4200 and 4300; nonlocal origins need an explicit backend `CORS_ORIGINS` allowlist. Browser cookies, CSRF, cross-origin canvas and camera behavior still require real browser checks.

## ERP image and publication flow

Using a legitimate staff session, open the catalog item's Images tab. Upload a permitted sample necklace or earring image, save storefront copy, inspect the exact generated revision and explicitly approve its visual quality. Publish with confirmation only after the item is Complete and its supported cutout is ready and approved. An unfinished, failed, unapproved or replaced revision cannot publish. Explicit revocation removes it from subsequent public reads.

The existing Python script needs Pillow, numpy and scipy on the interpreter selected by backend `CUTOUT_PYTHON_PATH` (default `python3`). No new background-removal engine was added. For this session, tests used an isolated temporary interpreter at `/tmp/nbjewel-cutout-runtime-venv/bin/python`; this is not a deployed runtime. Generated preview quality remains manual review and source-photo licensing remains open.

Set nonsecret backend `ITEM_ASSET_STORAGE_PATH` to persistent storage for a running demo. The default is the API's `App_Data/item-assets`. Source/preview revisions and publication decisions persist on that filesystem. Sample ERP item edits themselves are in memory and reset on API restart, including new item records and changed copy/status. New sample item IDs are reserved on the same filesystem and never reused after restart, including IDs found in legacy asset/publication files. Keep that reservation storage together with the assets. Persistent asset links do not make the whole sample ERP durable. Ephemeral host disks require a separately configured persistent volume.

## Verification

Run backend checks with all SQL/connection variables removed and Sample forced. Select a Python interpreter with the existing cutout dependencies:

```bash
CUTOUT_PYTHON_PATH=/path/to/prepared/python python3 - <<'PYTHON'
import os, subprocess
env = {k: v for k, v in os.environ.items() if 'CONNECTION' not in k.upper() and 'SQL' not in k.upper()}
env['Repository__Provider'] = 'Sample'
for command in [
    ['dotnet', 'build', 'backend/NbJewel.Api'],
    ['dotnet', 'test', 'backend/NbJewel.Api.Tests'],
]:
    subprocess.run(command, env=env, check=True)
PYTHON

npm --prefix frontend test
NG_BUILD_MAX_WORKERS=2 npm --prefix frontend run build
npm --prefix customer-tryon test
NG_BUILD_MAX_WORKERS=2 npm --prefix customer-tryon run build
```

The automated gate covers sample behavior, API authorization/publication boundaries, transparent PNG generation/restart, pricing and runtime storefront loading. The publish/update/revoke proofs use HTTP integration and the same storefront service without rebuilding it. SQL integration tests are skipped, not counted as SQL validation.

Manual checks for Muhammad remain separate: phone/desktop alignment; real sample cutout quality and licensing; camera capture, Retake and native share; real-browser CORS, cookies and CSRF; full staff Images-tab flow with a valid identity provider. No merge, deployment, production approval, SQL execution or Docker action is part of this phase.
