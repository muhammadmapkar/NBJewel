# PROGRESS (resume file). Say: "continue PROGRESS.md" in a new chat.
Rules: grep first, quiet flags (dotnet build/test -v q), full files, replies max 8 lines, no em-dashes, no secrets, "assumed" on guesses, STOP after each phase and wait for "go". Edit files with the file edit tool only (no cat or heredoc).
## Earlier work (done)
- SQL Server path (docker-compose, scripts/setup-db.sh, SqlItemRepository, shared write rules, tests per provider): built, NOT run against a real server (no Docker here).
- Python tools verified GET-only; README rewritten; legacy-react-prototype removed (commit 4a7705b); docs/decision-ai-service.md written (7cfbc55).
## Current goal: chatbot moves from Python ai-service into .NET (Mr. Sakthivel: inside .NET, Gemini allowed, no login for demo)
- Phase 0 DONE: map reported.
- Phase 1 DONE (uncommitted): backend/NbJewel.Api/Chat/ = SystemPrompt, IChatDataReader (read-only), ILlmClient, GeminiLlmClient (REST, x-goog-api-key header, 1 retry, 60s timeout), ChatTools (8 tools), Guards (4), ChatOrchestrator (5 tool calls, 8 model calls, 6 history). ChatController calls the orchestrator, same JSON. Program.cs: no AI_SERVICE_URL, global error handler, DI.
- Tests: backend/NbJewel.Api.Tests/Chat/ (guards, tools, 8 parity tests vs controllers, orchestrator, read-only proof) + ChatControllerTests rewritten. dotnet test: 147 passed, 2 skipped, 0 failed. Build: 0 warnings.
- Behavior changes vs Python: tools ran and all not_found => "No data found." (refused=false), model text ignored; Python tests for that case changed. Stock is read per item, not in parallel. Thinking-rejected state is per client instance.
- Gemini REST shape is NOT verified against the real API (no key tested). Check on first live run.
## Next
- Phase 2 (wait for "go"): git rm -r ai-service (own commit), remove AI_SERVICE_URL and Python steps from README, docs/run-guide.md, .env.example, scripts; update decision doc (Option B), questions doc, Known gaps; write docs/demo-script.md (8 questions + 3 refusals); run demo if GEMINI_API_KEY set; run README Sample commands.
- Uncommitted: all Phase 1 files, PROGRESS.md, SQL files (left alone on purpose).
