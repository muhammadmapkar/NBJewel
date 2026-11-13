<!-- Role: Provides a project-root continuation pointer for the ai-memory handoff. -->
# AI memory handoff

Resume in `/Users/muhammadnazirahmedmapkar/Documents/NBJewel`, the main project checkout. The completed implementation is committed locally and available directly in that folder. Do not create another worktree to resume this task.

Read `docs/unified-ai-progress.md` and `docs/unified-ai-review.md` first. Both assistants, scoped tools, owned conversations, approved knowledge retrieval, protected key storage and local security remediation are implemented. Independent review passed the local implementation scope.

Verified before integration: 303 backend tests passed, 0 failed, 2 skipped (SQL parity); 66 frontend tests passed; Angular production build passed. Real SQL persistence/concurrency, browser cookies/CORS, Gemini, SMTP delivery and deployment remain unverified.

The user has now explicitly approved creating the disposable local Docker/SQL test environment and running sample-data tests. No remote push, merge to main or deployment is authorized. Actual ERP role mapping and production provisioning still need the real application/security model.

Latest verification on 2026-11-13:
- Backend with real SQL Server: 355 passed, 0 failed, 0 skipped (see "SQL verification continuation" below). The earlier run without SQL was 303 passed, 2 skipped because `NBJEWEL_SQL_CONNECTION` was unset.
- Frontend: 66 tests passed and the production Angular build passed.
- Uncommitted and awaiting review: the SQL fixes (`SampleRepositoryFactory.cs`, `KnowledgeService.cs`, `docs/item_catalog_schema.sql`), the new `PlatformSqlIntegrationTests.cs`, and this file. The pre-existing untracked jewelry image is preserved.
- Still unverified: real-browser cookies/CSRF/CORS/isolation, Gemini, SMTP, and proxy/TLS deployment.
- No production or non-local database was used. No `.env`, secret or production data was touched.

The ai-memory handoff is scoped to workspace `default`, project `muhammadmapkar-nbjewel`, with this main checkout as its working directory. The shared ai-memory service remains responsible for the handoff lifecycle; this file is its project-local continuation pointer.

## SQL verification continuation
- Docker Desktop is installed and running (engine 29.9.3, Apple Silicon, approximately 8 GB allocated). Its CLI is at `/Applications/Docker.app/Contents/Resources/bin/docker`, outside the current terminal PATH. Include that directory in child-process PATH so the credential helper can be found; no global settings change is needed.
- Approved target: a new isolated `nbjewel-sql-test` container, bound only to `127.0.0.1:15433`, using `mcr.microsoft.com/mssql/server:2022-latest` under amd64 emulation. No other containers were present and the port was free.
- SQL Server is running and answered `SELECT 1` at `127.0.0.1:15433` in the approved `nbjewel-sql-test` container. The image was downloaded and the container created with 4 GB memory limit, 2 CPUs, SQL Developer edition and only a localhost port binding. Temporary credentials were generated inside `/tmp/nbjewel-disposable-sql-runner.py` and are never printed or written to `.env`.
- The initial runner (`27712`) was stopped after real SQL exposed a source-root lookup bug. Use `python3 -u /tmp/nbjewel-sql-verify.py` to run the full SQL-enabled suite, or pass one VSTest filter argument. This helper verifies the disposable label and localhost-only port, reads only this container's test credential in memory, and writes redacted output to `/tmp/nbjewel-sql-verification.log`. It never reads `.env` or prints credentials.
- Added `backend/NbJewel.Api.Tests/PlatformSqlIntegrationTests.cs` for explicit local test database `NBJewelPlatformTest`: actual SQL schema application, protected key/identity/history restart, admission revision and idempotency constraints, knowledge publish/revoke concurrency, and bulk/scoped catalog parity across more than 2,100 IDs.
- Real SQL tests already passed protected key/identity/history restart and admission revision/idempotency constraints. Three observed failures are being fixed and retested: FindRoot selected copied output data instead of source schemas; a `sample-data` wildcard inside a T-SQL block comment accidentally opened a nested comment; repeated publication did not issue an UPDATE, skipping the SQL concurrency predicate. Root lookup and schema comment were corrected; publication now explicitly marks PublishedVersionId modified.
- Focused real SQL verification is now green: 5 passed, 0 failed, 0 skipped. This includes source-root regression, key/identity/history persistence, revision and uniqueness constraints, version publication/revocation concurrency, and bulk/scoped reads across 2,200 IDs. New fixes/tests are uncommitted; implementation commit de790b4 is unchanged.
- The first complete SQL-enabled run exited with 51 failures because SQL Server terminated with exit code 139 during repeated test-database resets; the resulting failures were connection-refused cascades, not assertion failures. After restarting the same isolated container, the focused platform suite passed 5/5, the remaining SQL-enabled tests passed 350/350, and the complete suite passed 355/355 with 0 skipped. No production or non-local database was used.
- The catalog test harness resets only its sample `NBJewelTest` database, and the platform tests use `NBJewelPlatformTest`, both on this isolated server. Rerunning the suite resets them again, so ask the user before any Docker or SQL data step.
- The user asked to save the handoff when the remaining limit reaches 3%. Account percentage is not exposed by available tools, so save checkpoints proactively rather than claiming that percentage can be monitored.
