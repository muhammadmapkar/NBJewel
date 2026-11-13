# PROGRESS (resume file). Say: "continue PROGRESS.md" in a new chat.
Rules: see CLAUDE.md and AGENTS.md. Grep first, quiet flags, replies max 8 lines, no em-dashes, no secrets.

## Decisions from Mr. Sakthivel (verbal, not yet in writing)
- Chatbot runs inside .NET. Python ai-service is removed.
- Gemini is allowed. No login for the demo.
- Net weight is computed (gross minus stone). Client value ignored.

## Done (branch demo-changes, nothing pushed)
- React prototype removed: 4a7705b. .NET chat core: 56fd3b7. SQL files: cecb668.
- Fail-fast (30 s per Gemini call, 45 s per question): 95a88d6.
- Phase 2 docs: da63aaf. Privacy, assumptions, copilot rules, CLAUDE.md: 42c6adc.
- ai-service deleted: 2b9d736. AGENTS.md stack line: 763cf4f. Run guide fix: a3499fa.
- Stock wording fix: 8c61626. Demo day checklist: 4dcaa14. Agent file nbjewel-lead exists.

## Checks (2026-10-14)
- dotnet build: 0 errors, 0 warnings. dotnet test: 149 passed, 2 skipped (SQL parity).
- Angular production build: passed.
- Live Gemini, Sample data: 5 of 5 passed twice (status, refusal, sources). Latest 1.4 to 8.1 s.
- Stricter content check, first run: 4 of 5. Stock answer listed the stores (4, 2, 1) but gave no total. Checks kept strict, no code or prompt change. Demo question reworded to "in each store".
- After deleting ai-service: one POST /api/chat returned 200 in about 4.5 s.

## Later verification (2026-11-13)
- Approved disposable SQL Server 2022 test container on Apple Silicon amd64 emulation: focused platform checks 5 passed, full SQL-enabled backend suite 355 passed, 0 failed, 0 skipped.
- The first full run had 51 connection-refused cascades after SQL Server exited with code 139 during repeated database resets. Docker reported `OOMKilled=false` and no container error. Restarting the same isolated container produced the clean result.
- This verifies the current code against the disposable environment, but does not establish stability of amd64 SQL Server emulation or production readiness. See `docs/run-guide.md` for the approval-gated options.

## Known gaps
- Apple Silicon SQL Server emulation can exit with code 139 during repeated resets. Do not change Docker or SQL settings without approval.
- No login, roles or permissions.
- Written approval from Mr. Sakthivel missing (questions doc, item 18).
- Gemini can be slow or return 503. Answer wording is not compared in the live check.
- Images and stock history endpoints, frontend unit tests, deployment and CI not built.

## Next
1. Ask the owner whether to push demo-changes. Do not push without asking.
2. Before another SQL run, choose an approved stability option from `docs/run-guide.md`.
