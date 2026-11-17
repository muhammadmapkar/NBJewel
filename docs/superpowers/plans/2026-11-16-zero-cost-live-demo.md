<!-- Role: Records the approved assumed zero-cost manager demonstration and implementation checkpoints. -->
# Zero-cost live ERP and storefront demo

Authorized 2026-11-16 by Muhammad. Implement repository preparation; owner executes SQL, Docker, credentials and live deployment. Main checkout on feature/customer-storefront only. No .env/secrets access, protected owner files, service restarts, merges, resets, worktrees or force pushes. Terra handles routine work, Sol simple logic, Astra security/architecture/complex work and independent review. Root alone owns handoff, commits and pushes. AI-MEMORY-HANDOFF.md is the only progress ledger.

## Accepted design

One sample business nb-jewel-demo. Two Vercel projects in the existing repo: frontend at /erp and customer-tryon as gateway at /shops. /api proxies through a free Cloudflare Quick Tunnel to a dedicated SQL-backed API on the owner's Mac at 127.0.0.1:5157. Mac/Docker/API/tunnel remain on during the demonstration. Public domain stays nb-jewel.vercel.app; tunnel changes require a gateway rebuild. No new paid services or new Git repository.

SQL holds catalog/identity/customer state/image metadata/publications; durable local folders hold image bytes and portal settings/assets/audit. Preserve existing database and files. A local owner-run setup command creates one chosen, hashed-password SQL owner account without changing it on subsequent starts. Reuse Development-only bootstrap locally; expose only a Production-mode API with sample identity disabled, stable SQL key ring and narrow proxy trust. Secrets are supplied privately through macOS Keychain, never frontend or Git. Demo-only customer verification may explicitly be disabled; provider features must report unavailable without keys and remain optional.

Automatic publication requires explicit stored product intent and current authorization. New products created by publication-authorized staff opt in visibly; existing products remain unchanged. Readiness reuses category/status/price/copy/image gates and manual approval of the current revision. Reconcile every 5 seconds and after mutations. Withdrawal disables intent, replacement images need new approval, audit/publication atomic, retries idempotent. Portal visibility/offline controls remain authoritative.

Storefront refreshes every 20 seconds only while visible and on return to the tab; non-overlapping, shop/store-safe requests. Maintain selections and preview security; stale/unavailable responses disable unverifiable actions. Healthy-network visibility target 30 seconds, without manual reload or Git deployment.

## Implementation checkpoints

- [x] L1: owner SQL inspection/backup/schema guidance, permanent credential setup/reset, secure public-demo launcher and core readiness. Test invalid configuration, no secret output, no environment leakage, no startup SQL mutation, account guards and optional-provider independence.
- [ ] L2: durable publication intent SQL/Sample storage, shared readiness evaluator, worker, permissions and ERP status/control. Test approvals, revision races, revoked authorizations, pause/withdraw races, retries, restart and importer compatibility.
- [ ] L3: polling implementation and tests for visible/hidden/focus, cancellation, shop/store changes, errors, product detail changes and destroyed subscriptions.
- [ ] L4: GitHub checks (no SQL/deployment secrets), complete numbered owner runbook, independent Astra review and final gates. Manager proof: create on live ERP, inspect row in Docker SQL, approve image, see storefront within 30 seconds, price edit/withdraw, same login/data after restart.

## Gates and boundaries

Use focused RED/GREEN then full affected gates. Backend: env -i PATH="$PATH" HOME="$HOME" Repository__Provider=Sample CUTOUT_PYTHON_PATH=/tmp/nbjewel-cutout-runtime-venv/bin/python dotnet test backend/NbJewel.Api.Tests --no-restore. All SQL connection variables absent in agent-run tests; real SQL tests stay skipped. Both apps: npm test, npx tsc --noEmit -p tsconfig.app.json, NG_BUILD_MAX_WORKERS=2 npm run build; hosted build contracts and generated builds with synthetic origins. Full output /tmp/nbjewel-live-<step>.log. Check whitespace, role comments and em/en dashes. Commit explicit paths with wip(codex-portal-impl): prefix and push after each checkpoint.

Local isolated browser verification may use temporary Sample test hosts only; actual tunnel/Vercel/SQL upload/restart and backup/restore are owner-run acceptance checks. SQL scripts additive and reviewed, no destructive tests against existing data. Claude final portal review remains pending even after scoped Astra review.
