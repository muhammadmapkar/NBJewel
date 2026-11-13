<!-- Role: Records the only shared handoff and lock for turn-based customer storefront work. -->
# AI memory handoff

## Lock
- Agent: codex-storefront
- Since: 2026-11-14T01:08:23+04:00
- Status: held for setup and phase 2 task 1.

## Branch and last commit
- Branch: `feature/customer-storefront`.
- Remote: `origin/feature/customer-storefront`; upstream exists; fetch and fast-forward pull succeeded.
- Verified starting HEAD: `a4f8ef3acb049daaea9016dc18dfbf2886e708f0` (Phase 1).
- Snapshot commit: the commit containing this handoff; resolve with `git log -1 --format=%H -- AI-MEMORY-HANDOFF.md`. Compare it with HEAD on the next start. A literal self-referencing commit hash cannot be recorded inside the same commit.

## Current goal
Complete setup, then phase 2 task 1: preserve storefront metadata and a clearly labelled sample platinum product through the swappable catalog data layer. API first, then storefront. No SQL execution.

## Done
- Phase 1 at `a4f8ef3`: equal image cards, details blocks, two-line names, photo preview with Retake/Save/Share, ring uploads blocked, FAQ restored.
- Startup: fetched all remotes, confirmed branch and upstream, pulled with `--ff-only`, read the prior handoff fully, checked date and found no active lock, matched HEAD to the supplied seed.
- Setup files written: replaced stale handoff statements, added the handoff protocol to AGENTS.md, and added Claude/Copilot routing instructions. Validation passed; setup checkpoint prepared.

## Next steps (ordered checklist)
- [x] Setup: handoff/protocol validation passed; these four files form the setup checkpoint, pushed before task 1 begins.
- [ ] 1. Storefront metadata and sample platinum support. Size L. Files: backend models, repositories, sample-data, docs/item_catalog_schema.sql. Done when a labelled sample platinum product keeps metal, purity, and storefront metadata. Test: `dotnet test backend/NbJewel.Api.Tests`.
- [ ] 2. Public read-only endpoints for products and metal rates. Size M. Files: controllers, Knowledge/KnowledgeService.cs, Identity/ErpBoundaryFilter.cs, backend tests. Anonymous reads return published allowed products only; unpublished and excluded categories stay hidden. Test: backend suite.
- [ ] 3. Complete pricing and change evidence. Size L. Files: ItemPricing.cs, ItemModels.cs, repositories, backend tests. A sample rate change gives metal, making, stones, VAT, total, effective date, and change explanation. Test: backend suite.
- [ ] 4. Durable cutout pipeline link. Size L. Files: Jewelry/, controllers, repositories, existing cutout/catalog scripts. Upload stores a linked transparent preview asset that survives restart. Use existing scripts only; no new removal tooling. Licensing open, quality manual. Test: backend suite.
- [ ] 5. ERP upload and explicit publication controls. Size M. Files: ERP catalog/core data and KnowledgeController.cs. Ready sample item publishes; unfinished cutout cannot publish. Test: ERP npm test/build and backend suite.
- [ ] 6. Storefront loads from configurable API address. Size M. Files: storefront catalog service/models, app.config.ts, config/service tests. Configured address uses API; unset address uses static catalog. Test: storefront npm test/build.
- [ ] 7. Dated rates, metal filters, price explanations. Size M. Files: storefront header, catalog, shop/product pages. Gold, silver, platinum use matching rate, breakdown, real effective date, no invented freshness. Test: storefront npm test/build.
- [ ] 8. Prove updates appear without rebuilding. Size M. Files: backend and storefront service tests. Publish, update, revoke a sample product; storefront reads reflect each change. Test: backend/storefront suites.
- [ ] 9. Warnings, docs, phase gate. Size S. Files: shop page, vitest.config.ts, README.md, docs/superpowers/plans/, this handoff. All six baseline checks pass; completed work and manual checks stay separate. Test: backend build/test, ERP npm test/build, storefront npm test/build.
- [ ] After task 9: run the lead review, stop, wait for Muhammad.

## Blockers and decisions
- The prior handoff described another branch and old SQL authorization. Replaced from Muhammad's current seed and protocol; current instructions prohibit all Docker/SQL data steps.
- Ruling: use this existing feature checkout and this handoff as the only ledger; no worktree, extra handoff, or AI-memory service mutation. Agents are sequential and the owner's forbidden worktree stays untouched.
- Ruling: snapshot commit is resolved from git history because a commit cannot embed its own hash. Record verified predecessor and task implementation hashes as work advances.
- Known gaps: backend pricing omits stone value and VAT; storefront uses static gold rates; cutouts are separate from ERP items.
- Known warnings: unused RouterLink in ShopPage; Vitest config loading.
- No questions to owner. Choose conservative sample-only behavior and log assumptions. Accounts, chatbot, locator are later phases.

## Files touched
- This turn: `AGENTS.md`, `AI-MEMORY-HANDOFF.md`, `CLAUDE.md`, `.github/copilot-instructions.md`.
- Pre-existing owner changes preserved, excluded from staging: PROGRESS.md, docs/run-guide.md, customer-tryon/package-lock.json; untracked browser security plan, old proof image, pasted jewelry image.

## Test commands and last results
- Prior seed only, not rerun today: backend 309 passed, 6 skipped; ERP 66 passed; storefront 50 passed; builds OK.
- Startup: `git fetch --all --prune` passed; `git pull --ff-only` reported already up to date.
- Setup validation: Python assertions on all required sections, four role comments, routing pointers and no em-dashes passed; `git diff --check -- AGENTS.md AI-MEMORY-HANDOFF.md CLAUDE.md .github/copilot-instructions.md` passed.
- Task 1 backend tests pending; SQL connection variables will be removed from child processes and `.env` files will not be read.

## Manual checks for Muhammad
- Phone and desktop alignment.
- Camera capture, Retake, native share.
- Cutout quality (manual review required).
- Real browser CORS and cookies.

## Agent log (newest first)
- 2026-11-14T01:08:23+04:00 codex-storefront: took the previously absent lock, matched seed commit, wrote setup files. Owner changes untouched. Setup validation passed; setup commit/push followed by task 1.
