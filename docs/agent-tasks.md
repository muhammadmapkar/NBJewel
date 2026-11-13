<!-- Role: Ready-to-paste task prompts for other AI coding agents (Copilot, Antigravity) working on NB Jewel. -->
# Agent task prompts

Paste one prompt per agent session. Each task is self-contained. Do them in the order listed (safest first).

## Guardrails for every task

Add this block to the start of each prompt:

> Work in `/Users/muhammadnazirahmedmapkar/Documents/NBJewel`. Read `AGENTS.md`, `CLAUDE.md`, `AI-MEMORY-HANDOFF.md` first. Do not push, merge or deploy. Do not touch `.env` or any secret. Do not delete files or folders. Ask me before any Docker or SQL data step. Use sample data only. No em-dashes in docs. Report what you did and what you did not verify.

## Task 1: Independent review of commit 586f9c8 (read-only)

Suggested agent: Antigravity (Sonnet thinking).

> Review commit `586f9c8` in NBJewel. Cover three things: the `FindRoot` change in `SampleRepositoryFactory.cs`, the forced `PublishedVersionId` update in `KnowledgeService.cs`, and `PlatformSqlIntegrationTests.cs`. Look for flaky tests, a test database that is not isolated, and anything that could touch a non-local database. Report findings only, ranked by severity. Change no files.

Done when: you have a findings list with file and line references.

## Task 2: Fix the SQL container crash

Suggested agent: Copilot.

> The `nbjewel-sql-test` container exits with code 139 on Apple Silicon under amd64 emulation, and it has crashed during repeated test-database resets. Investigate the cause. Propose options such as enabling Rosetta in Docker Desktop, adding a retry or health wait to `/tmp/nbjewel-sql-verify.py`, or using an arm-native image. Write the proposal into `docs/run-guide.md`. Make no Docker changes until I approve.

Done when: `docs/run-guide.md` has a short proposal with the trade-offs of each option.

## Task 3: Browser cookie, CSRF, CORS and isolation test plan

Suggested agent: Copilot.

> Write a step-by-step local test plan, or Playwright tests, for cookies, CSRF, CORS and conversation isolation between users. Use sample data only and no real API keys. Mark any step that needs a key or a decision from Mr. Sakthivel as "blocked".

Done when: the plan or tests run locally against sample data, and blocked steps are listed.

## Not for agents

- Adding Gemini and SMTP keys to the backend environment (owner only).
- Real ERP role mapping and production provisioning (needs Mr. Sakthivel).
- Any push, merge or deployment.
