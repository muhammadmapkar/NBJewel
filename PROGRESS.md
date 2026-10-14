# PROGRESS (resume file). Say: "continue PROGRESS.md" in a new chat.
Rules: see CLAUDE.md and AGENTS.md. Grep first, quiet flags, replies max 8 lines, no em-dashes, no secrets.

## Decisions from Mr. Sakthivel (verbal, not yet in writing)
- Chatbot runs inside .NET. Python ai-service is removed.
- Gemini is allowed. No login for the demo.
- Net weight is computed (gross minus stone). Client value ignored.

## Done (branch demo-changes, nothing pushed)
- Legacy React prototype removed: 4a7705b.
- .NET chat core: 56fd3b7. SQL files: cecb668.
- Fail-fast (30 s per Gemini call, 45 s per question): 95a88d6.
- Phase 2 docs: da63aaf. Privacy, assumptions, copilot rules, CLAUDE.md: 42c6adc.
- ai-service deleted: 2b9d736.
- Agent file .claude/agents/nbjewel-lead.md exists.

## Checks (2026-10-14)
- dotnet build: 0 errors, 0 warnings. dotnet test: 149 passed, 2 skipped (SQL parity).
- Angular production build: passed.
- Live Gemini, Sample data: 5 of 5 passed, twice. Latest 1.4 to 8.1 s per answer.
- After deleting ai-service: one POST /api/chat returned 200 in about 4.5 s.

## Known gaps
- Sql mode unverified (no Docker). Do not demo it.
- No login, roles or permissions.
- Written approval from Mr. Sakthivel missing (questions doc, item 18).
- Gemini can be slow or return 503. Answer wording is not compared in the live check.
- Images and stock history endpoints, frontend unit tests, deployment and CI not built.

## Next
1. Ask the owner whether to push demo-changes. Do not push without asking.
2. With Docker: ./scripts/setup-db.sh, then the SQL parity tests. Get the written approvals.
