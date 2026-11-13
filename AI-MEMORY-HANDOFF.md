<!-- Role: Records the only shared handoff and lock for turn-based customer storefront work. -->
# AI memory handoff

## Lock
- Agent: codex-storefront
- Since: 2026-11-14T01:41:07+04:00
- Status: held for tasks 3 and 4, explicitly requested by Muhammad.

## Branch and last commit
- Branch: `feature/customer-storefront`.
- Remote: `origin/feature/customer-storefront`; upstream exists; fetch and fast-forward pull succeeded.
- Verified Phase 1 baseline: `a4f8ef3acb049daaea9016dc18dfbf2886e708f0` (Phase 1).
- Setup commit: `d44c748` (pushed).
- Task 1 commit and verified predecessor for this checkpoint: `f7d78e3` (pushed).
- Task 2 is in the snapshot commit resolved below.
- Snapshot commit: the commit containing this handoff; resolve with `git log -1 --format=%H -- AI-MEMORY-HANDOFF.md`. Compare it with HEAD on the next start. A literal self-referencing commit hash cannot be recorded inside the same commit.

## Current goal
Complete tasks 3 and 4 this turn, then push the final checkpoint and release the lock.

Setup and tasks 1-2 complete and checkpointed. Next agent: acquire the Lock, verify HEAD against this snapshot, then start the first unchecked step, phase 2 task 3 (complete pricing and change evidence). API first, then storefront. No SQL execution.

## Done
- Phase 2 task 3: explicit validated stone value/VAT percentage retained through both adapters and older updates; prices reconcile rounded line amounts and include dated recorded-rate evidence with previous total/delta. Full backend sample suite passed 339 tests, 6 SQL skips. SQL schema/adapter changes are prepared only, not applied.
- Phase 2 task 2: anonymous read-only `/api/storefront/products`, `/api/storefront/products/{id}`, `/api/storefront/metal-rates`; explicit named GET boundary exceptions; fresh publication checks, Complete status and category/metal allowlists, narrow DTOs, no-store responses, bounded paging and 503 on unavailable publication storage. Full backend sample suite: 330 passed, 6 SQL skips.
- Phase 2 task 1: optional validated storefront metadata survives create/read/search/explicit update and legacy ERP edits; labelled Draft sample platinum necklace and historical Pt950 rate added; Sample and SQL mappings prepared; full backend sample suite passed 315 tests, 6 SQL checks skipped.
- Phase 1 at `a4f8ef3`: equal image cards, details blocks, two-line names, photo preview with Retake/Save/Share, ring uploads blocked, FAQ restored.
- Startup: fetched all remotes, confirmed branch and upstream, pulled with `--ff-only`, read the prior handoff fully, checked date and found no active lock, matched HEAD to the supplied seed.
- Setup files written: replaced stale handoff statements, added the handoff protocol to AGENTS.md, and added Claude/Copilot routing instructions. Validation passed; setup pushed as `d44c748`.

## Next steps (ordered checklist)
- [x] Setup: handoff/protocol validation passed; these four files form the setup checkpoint, pushed before task 1 begins.
- [x] 1. Storefront metadata and sample platinum support. Size L. Files: backend models, repositories, sample-data, docs/item_catalog_schema.sql. Done when a labelled sample platinum product keeps metal, purity, and storefront metadata. Test: `dotnet test backend/NbJewel.Api.Tests`.
- [x] 2. Public read-only endpoints for products and metal rates. Size M. Files: controllers, Knowledge/KnowledgeService.cs, Identity/ErpBoundaryFilter.cs, backend tests. Anonymous reads return published allowed products only; unpublished and excluded categories stay hidden. Test: backend suite.
- [x] 3. Complete pricing and change evidence. Size L. Files: ItemPricing.cs, ItemModels.cs, repositories, backend tests. A sample rate change gives metal, making, stones, VAT, total, effective date, and change explanation. Test: backend suite.
- [ ] 4. Durable cutout pipeline link. Size L. Files: Jewelry/, controllers, repositories, existing cutout/catalog scripts. Upload stores a linked transparent preview asset that survives restart. Use existing scripts only; no new removal tooling. Licensing open, quality manual. Test: backend suite.
- [ ] 5. ERP upload and explicit publication controls. Size M. Files: ERP catalog/core data and KnowledgeController.cs. Ready sample item publishes; unfinished cutout cannot publish. Test: ERP npm test/build and backend suite.
- [ ] 6. Storefront loads from configurable API address. Size M. Files: storefront catalog service/models, app.config.ts, config/service tests. Configured address uses API; unset address uses static catalog. Test: storefront npm test/build.
- [ ] 7. Dated rates, metal filters, price explanations. Size M. Files: storefront header, catalog, shop/product pages. Gold, silver, platinum use matching rate, breakdown, real effective date, no invented freshness. Test: storefront npm test/build.
- [ ] 8. Prove updates appear without rebuilding. Size M. Files: backend and storefront service tests. Publish, update, revoke a sample product; storefront reads reflect each change. Test: backend/storefront suites.
- [ ] 9. Warnings, docs, phase gate. Size S. Files: shop page, vitest.config.ts, README.md, docs/superpowers/plans/, this handoff. All six baseline checks pass; completed work and manual checks stay separate. Test: backend build/test, ERP npm test/build, storefront npm test/build.
- [ ] After task 9: run the lead review, stop, wait for Muhammad.

## Blockers and decisions
- Task 3 ruling: missing legacy stone/VAT inputs default to zero; no tax rate is invented. Explicit configured demo values are used when supplied. Component-level rounding makes the existing 18K fixture total AED 298.65 rather than 298.64; this is intentional reconciliation. Rate-change totals hold current item inputs constant and are not claimed historical sale prices. A currency change does not report a monetary delta.
- Task 3 plan: add explicit StoneValue and VatPercent inputs (legacy missing values remain zero, not inferred tax advice); validate and preserve them on older ERP edits in both adapters; round line amounts to two decimals and VAT on their sum so the displayed breakdown reconciles; select current and immediately prior recorded rate by date then id; return effective date, rate IDs, prior total, delta and deterministic explanation. Compare the same item inputs at both rates, not an invented historical invoice. Prepare SQL columns/seed mappings only. Test RED then full backend suite, checkpoint/push before task 4.
- Task 4 plan: reuse the existing cutout-jewelry.py algorithm, add bounded decoding/report support, invoke it without a shell and with a timeout, persist item-linked source/preview/manifest under configured local asset storage using generated file names and atomic metadata writes; expose ERP-authorized upload/preview/metadata endpoints. Generated previews require manual quality review and do not publish an item. No new removal engine, SQL, Docker or source-photo scans. Licensing remains open.
- Task 2 uses the existing IPublicationReader/KnowledgeService on every request rather than duplicating publication state. The real default sample startup currently has no configured KnowledgeDatabase provider, so product reads fail closed with 503 until publication storage is configured. HTTP tests supply an isolated EF in-memory provider; task 5 must resolve sample-only publication storage without enabling SQL or real identity/data. Do not present the full storefront pipeline as operational yet.
- Public product price now carries task 3 stones, VAT, effective date/rate ID and recorded-rate change evidence. Sample platinum remains Draft in fixtures and is not auto-published.
- Task 2 static self-review checked allowed GET actions, DTO field boundaries, paging overflow, search limits, current publication reads and safe failure. No independent lead review was run early; retain that gate after task 9.
- Task 2 plan: test anonymous HTTP reads and absence of write routes; add `/api/storefront/products`, `/api/storefront/products/{id}`, `/api/storefront/metal-rates`; explicitly permit only these named GET actions through the ERP boundary; read publication each time, require Complete, map an allowlist of supported category names and Gold/Silver/Platinum, return a narrow DTO, disable response caching, bound searches and pagination, return 503 if publication storage is unavailable. No automatic publication or permission bypass on ERP routes.
- Task 2 category mapping is assumed demo policy: Necklaces -> necklace, Earrings -> earrings, Sets -> set, Maang tikka -> tikka. Rings, coins, bangles, chains, pendants and unknown categories are excluded until explicitly supported. No new chatbot behavior. Cutout readiness publication guard remains task 5.
- Task 1 implementation checkpoint: optional metadata mapped through both repository adapters and SQL seed/schema files; sample product 31 (Platinum/950) is Draft with no image. Historical assumed rate: AED 110/g on 2026-09-30, not a live or fresh market claim. Counts/next-id tests updated for the added fixture; unknown-metal test now uses Palladium. SQL execution is not authorized; SQL parity tests remain skipped. Existing SQL databases require a future reviewed migration for StorefrontJson.
- JSON fixtures remain strict JSON (the format does not allow comments); sample role and assumptions are explicit in the new product's copy and this handoff.
- Task 1 design (assumed demo contract): optional typed storefront metadata on Item and create/update requests, including display description/note, collection, occasions, stones, size, badge, popularity, added date, and sample label. Metadata does not grant publication. Old requests omitting metadata preserve it on update; explicit metadata replaces it.
- Task 1 plan: baseline backend sample suite; write repository tests for platinum seed/read/filter, metadata create/update/legacy writes and validation; observe RED; add the model/shared validation and Sample/SQL mappings; add one Draft sample platinum necklace plus an explicitly historical assumed Pt950 rate; prepare SQL JSON column/check and seed mapping as files; run backend suite and static diff review; checkpoint and push.
- Dependencies reviewed: task 2 consumes item metal/purity/metadata but publication remains in KnowledgeService; task 3 consumes Pt950 rate and Item pricing, stones/VAT remain task 3; task 4 will link cutout assets without treating metadata as readiness; task 6 maps existing storefront display fields. No public endpoints or frontend behavior are added in task 1.
- The prior handoff described another branch and old SQL authorization. Replaced from Muhammad's current seed and protocol; current instructions prohibit all Docker/SQL data steps.
- Ruling: use this existing feature checkout and this handoff as the only ledger; no worktree, extra handoff, or AI-memory service mutation. Agents are sequential and the owner's forbidden worktree stays untouched.
- Ruling: snapshot commit is resolved from git history because a commit cannot embed its own hash. Record verified predecessor and task implementation hashes as work advances.
- Known gaps: storefront still uses static gold rates; cutouts remain separate until task 4; sample publication storage remains task 5.
- Known warnings: unused RouterLink in ShopPage; Vitest config loading.
- No questions to owner. Choose conservative sample-only behavior and log assumptions. Accounts, chatbot, locator are later phases.

## Files touched
- Task 3: ItemModels.cs, ItemPricing.cs, ItemWriteRules.cs, SampleItemRepository.cs, SqlItemRepository.cs, SqlDatabaseSetup.cs, CompletePricingTests.cs, PriceCalculationTests.cs, docs/item_catalog_schema.sql, this handoff.
- Task 2: `backend/NbJewel.Api/Controllers/PublicCatalogController.cs`, `backend/NbJewel.Api/Identity/ErpBoundaryFilter.cs`, `backend/NbJewel.Api.Tests/PublicCatalogHttpTests.cs`, `AI-MEMORY-HANDOFF.md`.
- Task 1: ItemModels.cs, ItemWriteRules.cs, SampleItemRepository.cs, SqlItemRepository.cs, SqlDatabaseSetup.cs, sample-data/items.json and metal-rates.json, docs/item_catalog_schema.sql, StorefrontMetadataTests.cs, ItemWriteTests.cs, ParityTests.cs, Chat/ChatToolsTests.cs.
- This turn: `AGENTS.md`, `AI-MEMORY-HANDOFF.md`, `CLAUDE.md`, `.github/copilot-instructions.md`.
- Pre-existing owner changes preserved, excluded from staging: PROGRESS.md, docs/run-guide.md, customer-tryon/package-lock.json; untracked browser security plan, old proof image, pasted jewelry image.

## Test commands and last results
- Latest, task 3: `dotnet test backend/NbJewel.Api.Tests` in forced Sample mode with all SQL/connection env vars removed: 339 passed, 6 skipped, 0 failed. Log `/tmp/nbjewel-task3-green.log`. RED: 9 expected failures before implementation (`/tmp/nbjewel-task3-red.log`). Diff whitespace check passed.
- Latest, task 2: `dotnet test backend/NbJewel.Api.Tests` with SQL/connection environment variables removed and `Repository__Provider=Sample`: 330 passed, 6 skipped, 0 failed. Build succeeded within this test command, no compiler/analyzer warnings. Log `/tmp/nbjewel-task2-green.log`.
- Task 2 RED before implementation: 10 expected failures for absent routes, 5 hidden-item 404 cases passed. Final 15 HTTP cases all pass, including narrow fields, publication/category/draft filtering, paging, platinum rates/sample label, unavailable storage, immediate revocation, write-route absence and protected ERP routes. Log `/tmp/nbjewel-task2-red.log`.
- Task 2 `git diff --check` passed for all four checkpoint paths. No frontend source changed, so frontend suites were not rerun during these backend tasks; phase gate still requires all six checks.
- Task 1 final: `dotnet test backend/NbJewel.Api.Tests` in forced sample mode with SQL/connection variables removed: 315 passed, 6 skipped, 0 failed. Build performed by dotnet test, no compiler/analyzer warnings. Log `/tmp/nbjewel-task1-green.log`.
- Task 1 RED: 5 failed, 1 passed before implementation. Full-run fixture-count failure corrected, then final full suite passed. `git diff --check` on all task paths passed.
- Static self-review completed for task 1 shared validation, SQL column/parameter/reader and seed mapping, legacy update behavior, sample labels, and excluded owner paths. No independent lead review yet; that gate remains after task 9.
- Prior seed only, not rerun today: backend 309 passed, 6 skipped; ERP 66 passed; storefront 50 passed; builds OK.
- Startup: `git fetch --all --prune` passed; `git pull --ff-only` reported already up to date.
- Setup validation: Python assertions on all required sections, four role comments, routing pointers and no em-dashes passed; `git diff --check -- AGENTS.md AI-MEMORY-HANDOFF.md CLAUDE.md .github/copilot-instructions.md` passed.
- Task 1 baseline: `dotnet test backend/NbJewel.Api.Tests` with all SQL/connection environment variables removed and `Repository__Provider=Sample`: 309 passed, 6 skipped, 0 failed. Initial sandbox run aborted at VSTest socket binding; same command outside sandbox passed. No SQL executed. Logs: `/tmp/nbjewel-task1-baseline.log`.

## Manual checks for Muhammad
- Phone and desktop alignment.
- Camera capture, Retake, native share.
- Cutout quality (manual review required).
- Real browser CORS and cookies.

## Agent log (newest first)
- 2026-11-14T01:47:02+04:00 codex-storefront: task 3 passed 339 tests with 6 SQL skips; static review of parameter/column/reader positions and legacy preservation passed; checkpoint/push before task 4.
- 2026-11-14 codex-storefront: task 3 RED verified, 9 assertion failures for omitted stones/VAT, absent persisted inputs, invalid input acceptance and non-reconciling totals. Existing script dependencies installed only into /tmp/nbjewel-cutout-runtime-venv for task 4; no new removal engine.
- 2026-11-14T01:41:07+04:00 codex-storefront: fetched, confirmed branch, ff-only pull succeeded, read handoff fully, verified HEAD and snapshot are bab3127, reacquired released lock for tasks 3-4. Owner files preserved.
- 2026-11-14T01:21:59+04:00 codex-storefront: task 2 verified (330 passed, 6 SQL skips); updated checklist and limitations, released Lock for the next sequential agent, and prepared explicit-path checkpoint/push. First unchecked task is 3. No owner files, secrets, Docker, SQL data, deployment, merge, deletion or forbidden worktree touched.
- 2026-11-14 codex-storefront: task 2 RED: 10 expected failures for missing public routes, 5 hidden-item 404 cases already pass. Log `/tmp/nbjewel-task2-red.log`. Implementing read-only controller and named GET boundary exceptions.
- 2026-11-14 codex-storefront: task 1 pushed as f7d78e3; task 2 plan recorded and HTTP contract tests next.
- 2026-11-14T01:16:25+04:00 codex-storefront: task 1 complete after 315 passing sample tests, 6 SQL skips. Preparing explicit-path commit and push; task 2 next. SQL adapters/schema were reviewed as files only.
- 2026-11-14 codex-storefront: new metadata/platinum contracts passed in the full run; one pre-existing low-stock fixture-count assertion failed (30 versus 31). The tool correctly treats the new stockless draft sample as zero available. Updated that expectation and added an explicit zero-stock platinum assertion; rerunning the suite.
- 2026-11-14 codex-storefront: task 1 RED confirmed: 5 expected contract failures (missing platinum, metadata create/read/update/preservation and rejection); 1 legacy-null behavior already passes. Log `/tmp/nbjewel-task1-red.log`. Implementing shared validation and repository mappings.
- 2026-11-14 codex-storefront: task 1 baseline passed 309/6 skipped in explicit sample mode. Writing contract tests before implementation.
- 2026-11-14T01:10:18+04:00 codex-storefront: setup pushed as d44c748; task 1 design and ordered implementation plan recorded. New platinum fixture will be Draft, sample-only, without a claimed real photo or cutout.
- 2026-11-14T01:08:23+04:00 codex-storefront: took the previously absent lock, matched seed commit, wrote setup files. Owner changes untouched. Setup validation passed; setup commit/push followed by task 1.
