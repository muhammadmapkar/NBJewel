<!-- Role: Records the current unified-platform implementation, cross-agent continuation, review and verification limits. -->
# Unified AI platform progress

## Current state
The implementation was prepared on `feature/unified-ai-foundation` and is being committed and fast-forwarded into the main `/Users/muhammadnazirahmedmapkar/Documents/NBJewel` checkout on `tryon-feature`, at the user's request. Resume from that main folder and read `AI-MEMORY-HANDOFF.md` at its root. The earlier isolated worktree is retained but is no longer the continuation location. The pre-existing untracked jewelry image is not part of the implementation commit. The user explicitly asked to finish beyond the earlier three-phase cap.

Implemented: local SQL Identity, cookie/CSRF authentication, customer/ERP separation, explicit grants and store scopes; the shared Gemini provider and trusted assistant configurations; both owned conversation APIs and Angular panels; fresh per-tool authorization and scoped aggregates; explicit product publication; versioned knowledge, ACLs, lexical retrieval and revocation; encrypted history; idempotency, quotas and leases; persistent usage/security audits and retention cleanup. No AI write tools or unsupported ERP sales, profit or order services were invented.

## Cross-agent continuation
Claude resumed the unfinished Codex work and left its checkpoint in this file. No open ai-memory handoff was found through the scoped CLI; the memory connector was unavailable in the resumed session. The code and this checkpoint were inspected before work continued.

Claude's baseline was 288 passed, 0 failed, 2 skipped. Its changes were preserved: SQL-backed per-network anonymous limits and trusted proxy handling, batched cleanup and bulk stock/public scans, real legacy-route tests, version-bound publication with concurrency protection, deletion after access changes, and cleanup that preserves the original error.

## Remediation completed
- Finding 1: default reservation is 80,384 estimated tokens per question. It is not an upper bound or an exact spend control and is not reconciled to actual usage.
- Findings 2 and 3: per-network limits survive visitor cookie resets; forwarded headers require explicit trusted proxies. Bulk queries and per-read evidence validation caching are retained.
- Finding 4: same-site browser/API deployment with Strict cookies remains the chosen deployment contract.
- Finding 5: obsolete AiGateway and BoundClient classes were removed without deleting files. Tests now exercise the real PlatformOrchestrator and legacy route.
- Finding 6: public model prose is discarded. Server-rendered facts and bounded exact knowledge excerpts are bound to current returned source records. Guards check the final returned public text. ERP language generation still uses heuristic grounding guards, not a proof that every sentence is entailed.
- Finding 7: one retry budget is shared across the whole question, including thinking fallback. The learned omit-thinking setting persists only inside that question.
- Findings 8 and 9: explicit approved-version publication, optimistic concurrency and owner deletion after access changes remain in place.
- Finding 10: Data Protection keys persist as AES-GCM-encrypted XML in SQL under AI_DATA_PROTECTION_SECRET. No persistent key directory is required. Non-Development startup fails closed without valid configuration or readable keys. Development without a secret explicitly warns about ephemeral keys. Unreadable conversations return 410 and can be deleted by their owner.
- Finding 11: Swagger is Development-only. Unknown-email login performs dummy password verification. Email verification defaults on outside Development; pending registration does not authenticate. SMTP delivery is configurable, fragment links are cleared by the frontend, and confirmation requires an explicit CSRF-protected POST. No real emails were sent.

## Verification
Parent independent final commands:
- `dotnet test backend/NbJewel.Api.Tests --no-restore --disable-build-servers -v quiet -m:1`: 303 passed, 0 failed, 2 skipped, 305 total.
- `npm test -- --reporter=dot`: 66 passed across nine files.
- `npm run build -- --progress=false`: exit 0, initial bundle 350.23 kB.
- `git diff --check`: clean.
- `--platform-schema`: generated docs/platform-schema.sql offline, including encrypted key storage. It was not applied.

Added regressions cover actual API CSRF, visitor cookie flags and tampering, authenticated cross-user ownership, website ERP-tool rejection after hostile knowledge, in-flight and cached evidence revocation, unreadable-history recovery, fabricated public claims, final-text prompt leaks, retry scope and thinking fallback, email confirmation and frontend verification/session reset.

Test-first evidence is mixed and recorded honestly: final-text leak, question-local thinking fallback, and prior explicit remediation regressions were observed failing before their fixes. Some delegated tests were added after implementation or initially failed at compilation; a passing suite alone does not establish their red phase.

## Independent review
Final read-only security review by nbjewel_final_review: PASS for the reviewed local implementation and security scope, with no blocking findings remaining. Its two actionable findings (final public text guard order and question-local thinking fallback) were fixed and regression tested. Earlier identity review passed. Deployment acceptance remains pending the external checks below; no production-readiness claim is made.

## External verification and deployment gates
- Two SQL parity tests remain skipped because SQL is not configured. SQL bulk queries, gate concurrency, schema compatibility and actual SQL key persistence have not run against a real disposable database.
- Real browser cookie/CORS behavior, live Gemini, SMTP delivery and target proxy/TLS deployment are unverified.
- A SQL server, approved schema deployment, backend secrets and approved public content are needed to run the full platform. No fake authentication, real business policy or production data is supplied.
- Actual ERP role mapping, production ERP account provisioning and password recovery require the real application/security model. Future order/sales/write integrations stay disabled.
- Read `ai-gateway.md`, `identity-foundation.md`, `knowledge-platform.md`, `platform-key-protection.md` and `platform-deployment.md` before deployment. Provider-side spend limits are recommended because token reservations are heuristic.
- No secrets or .env files were read/changed, no SQL schema/data was applied, and no Docker state, remote branch, push, merge or deployment was changed.
