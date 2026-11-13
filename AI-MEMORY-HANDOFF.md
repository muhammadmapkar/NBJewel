<!-- Role: Provides a project-root continuation pointer for the ai-memory handoff. -->
# AI memory handoff

Resume in `/Users/muhammadnazirahmedmapkar/Documents/NBJewel`, the main project checkout. The completed implementation is committed locally and available directly in that folder. Do not create another worktree to resume this task.

Read `docs/unified-ai-progress.md` and `docs/unified-ai-review.md` first. Both assistants, scoped tools, owned conversations, approved knowledge retrieval, protected key storage and local security remediation are implemented. Independent review passed the local implementation scope.

Verified before integration: 303 backend tests passed, 0 failed, 2 skipped (SQL parity); 66 frontend tests passed; Angular production build passed. Real SQL persistence/concurrency, browser cookies/CORS, Gemini, SMTP delivery and deployment remain unverified.

The last pending question is approval to create/use a disposable local SQL container/database. No SQL or Docker change has been approved. No remote push, merge to main or deployment is authorized. Actual ERP role mapping and production provisioning still need the real application/security model.

The ai-memory handoff is scoped to workspace `default`, project `muhammadmapkar-nbjewel`, with this main checkout as its working directory. The shared ai-memory service remains responsible for the handoff lifecycle; this file is its project-local continuation pointer.
