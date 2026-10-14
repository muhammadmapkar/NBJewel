# PROGRESS (resume file). Say: "continue PROGRESS.md" in a new chat.
Rules: grep first, quiet flags (dotnet build/test -v q), no re-reads, replies max 8 lines, file edit tool only (no cat or heredoc), no em-dashes, no secrets, "assumed" on guesses, STOP after each phase and wait for "go".

## Decisions from Mr. Sakthivel
- Chatbot runs inside .NET. Python ai-service is to be removed.
- Gemini is allowed. Approval was verbal.
- No login for the demo.
- Net weight is computed (gross minus stone). Client value ignored.

## Done
- legacy-react-prototype deleted: 4a7705b.
- Docs commits: 0d60f69 (README, run guide, questions), 7cfbc55 (decision-ai-service.md).
- .NET chat core, Phase 1: 56fd3b7 (tools, guards, Gemini REST client, orchestrator, tests).
- SQL files (setup tool, schema, docker-compose, write rules): cecb668.
- Agent file .claude/agents/nbjewel-lead.md created.

## Tests
- dotnet test: 149 passed, 2 skipped (SQL parity, need a SQL Server). Build: 0 errors, 1 warning (CS8602 in ChatToolParityTests.cs line 102, not from the timeout work).

## Live Gemini smoke test: PARTIAL (not a pass)
- Request and response shape works: tool round trip and all four guards passed live.
- Failures were external: Gemini 503, slow calls (16 to 25 s), and one call ran 90 s with no reply.
- Best results: off-topic refusal and "No data found." passed in every run. Others passed only on some runs.
- Fail-fast work, uncommitted: GeminiLlmClient timing log (status, exception type, ms only), Gemini request timeout 30 s (Program.cs), 45 s per-question deadline in ChatController (QuestionDeadlineSeconds), 503 message "The assistant is busy. Please try again.", chat panel now shows the server message (fallback is the same text). The 90 s change was dropped.

## Live smoke test, rerun with 30 s / 45 s limits: PASS (1 run, 5 of 5, Sample data)
- On-topic 4.7 s, 3.8 s, 2.0 s (all 200, with sources). Off-topic refusal 1.4 s (refused). No-match "No data found." 2.3 s.
- No 503s and no retry with GEMINI_THINKING_LEVEL=minimal was needed. Model and level came from the env file (values not recorded here).
- One clean run only. Repeat once more before calling it repeatable (assumed needed before deleting ai-service).

## Not done
- ai-service NOT deleted. Delete only after a clean live pass.
- Sql mode unverified. No Docker on this machine yet.
- Phase 2 not started (README, run-guide, .env.example, scripts, decision doc, questions doc, demo script).

## Next
1. Done (uncommitted): Gemini timeout cap and clear error message. Waiting for "go" for the live run.
2. Done once (pass). Optionally repeat the live run. Commit the fail-fast work.
3. If clean, do Phase 2. The ai-service deletion goes in its own commit.
